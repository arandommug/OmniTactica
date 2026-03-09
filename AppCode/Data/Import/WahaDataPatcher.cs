using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Text.Json;

namespace OmniTactica.AppCode.Data.Import
{
    public static class WahaDataPatcher
    {
        public static async Task ApplyPatchesAsync(SqliteConnection connection, SqliteTransaction transaction, Action<string>? log = null)
        {
            var msg = "Applying post-import patches...";
            Debug.WriteLine(msg);
            log?.Invoke(msg);

            string? json = null;
            string? source = null;

            var userPath = Path.Combine(FileSystem.AppDataDirectory, "patches.json");

            if (File.Exists(userPath))
            {
                try
                {
                    json = await File.ReadAllTextAsync(userPath);
                    source = $"User override: {userPath}";
                    Debug.WriteLine(source);
                    log?.Invoke(source);
                }
                catch (Exception ex)
                {
                    msg = $"User override failed: {ex.Message}";
                    Debug.WriteLine(msg);
                    log?.Invoke(msg);
                }
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                const string resourceName = "OmniTactica.Resources.Raw.patches.json";

                try
                {
                    var assembly = typeof(WahaDataPatcher).Assembly;

                    await using var stream = assembly.GetManifestResourceStream(resourceName);

                    if (stream == null)
                    {
                        msg = $"Embedded patch file missing: {resourceName}";
                        Debug.WriteLine(msg);
                        log?.Invoke(msg);
                        return;
                    }

                    using var reader = new StreamReader(stream);

                    json = await reader.ReadToEndAsync();
                    source = $"Embedded resource: {resourceName}";
                }
                catch (Exception ex)
                {
                    msg = $"Failed to load embedded patches: {ex.Message}";
                    Debug.WriteLine(msg);
                    log?.Invoke(msg);
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(json))
                return;

            msg = $"Patches loaded from: {source}";
            Debug.WriteLine(msg);
            log?.Invoke(msg);

            List<PatchOperation>? patches;

            try
            {
                patches = JsonSerializer.Deserialize<List<PatchOperation>>(json);

                if (patches == null || patches.Count == 0)
                    return;
            }
            catch (Exception ex)
            {
                msg = $"Patch JSON parse error: {ex.Message}";
                Debug.WriteLine(msg);
                log?.Invoke(msg);
                return;
            }

            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;

            foreach (var patch in patches)
            {
                try
                {
                    cmd.Parameters.Clear();

                    string? sql = patch.action?.ToLowerInvariant() switch
                    {
                        "delete" when !string.IsNullOrWhiteSpace(patch.where)
                            => $"DELETE FROM \"{patch.table}\" WHERE {patch.where} COLLATE NOCASE",


                        "update" when patch.values?.Count > 0 && !string.IsNullOrWhiteSpace(patch.where)
                            => BuildUpdateSql(cmd, patch),

                        "insert" when patch.values?.Count > 0
                            => BuildInsertSql(cmd, patch),

                        _ => null
                    };

                    if (sql == null)
                        continue;

                    cmd.CommandText = sql;

                    var rows = await cmd.ExecuteNonQueryAsync();

                    if (rows > 0)
                    {
                        var action = patch.action?.ToUpperInvariant() ?? "UNKNOWN";
                        var details = action switch
                        {
                            "DELETE" => $"Deleted {rows} row(s) from {patch.table}",
                            "UPDATE" => $"Updated {rows} row(s) in {patch.table} (columns: {string.Join(", ", patch.values?.Keys ?? (IEnumerable<string>)Array.Empty<string>())})",
                            "INSERT" => $"Inserted {rows} row(s) into {patch.table}",
                            _ => $"Modified {rows} row(s) in {patch.table}"
                        };

                        msg = $"  ✓ {details}";
                        Debug.WriteLine(msg);
                        log?.Invoke(msg);

                        msg = $"    Reason: {patch.reason}";
                        Debug.WriteLine(msg);
                        log?.Invoke(msg);

                        if (!string.IsNullOrWhiteSpace(patch.where))
                        {
                            msg = $"    WHERE: {patch.where}";
                            Debug.WriteLine(msg);
                            log?.Invoke(msg);
                        }
                    }
                }
                catch (Exception ex)
                {
                    msg = $"Patch failed for {patch.table}: {ex.Message}";
                    Debug.WriteLine(msg);
                    log?.Invoke(msg);
                }
            }

            msg = "Patching complete.";
            Debug.WriteLine(msg);
            log?.Invoke(msg);
        }

        private static string BuildUpdateSql(SqliteCommand cmd, PatchOperation patch)
        {
            var sets = new List<string>();

            foreach (var kv in patch.values!)
            {
                var p = $"@{kv.Key}";
                sets.Add($"\"{kv.Key}\" = {p}");
                cmd.Parameters.AddWithValue(p, kv.Value ?? DBNull.Value);
            }

            return $"UPDATE \"{patch.table}\" SET {string.Join(", ", sets)} WHERE {patch.where} COLLATE NOCASE";
        }

        private static string BuildInsertSql(SqliteCommand cmd, PatchOperation patch)
        {
            var cols = new List<string>();
            var vals = new List<string>();

            foreach (var kv in patch.values!)
            {
                var p = $"@{kv.Key}";
                cols.Add($"\"{kv.Key}\"");
                vals.Add(p);
                cmd.Parameters.AddWithValue(p, kv.Value ?? DBNull.Value);
            }

            return $"INSERT INTO \"{patch.table}\" ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)})";
        }

        private class PatchOperation
        {
            public string table { get; set; } = "";
            public string action { get; set; } = "";
            public string? where { get; set; }
            public Dictionary<string, object>? values { get; set; }
            public string? reason { get; set; }
        }
    }
}
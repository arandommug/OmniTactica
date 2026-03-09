using OmniTactica.AppCode.Data.Database;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace OmniTactica.AppCode.Data.Import
{
    internal class WahaDataImporter
    {
        private static readonly HttpClient Http = new HttpClient();

        private static readonly List<string> DataFileNames = new()
        {
            "Datasheets_stratagems.csv",
            "Stratagems.csv",
            "Datasheets_abilities.csv",
            "Datasheets.csv",
            "Datasheets_wargear.csv",
            "Datasheets_keywords.csv",
            "Enhancements.csv",
            "Datasheets_options.csv",
            "Datasheets_detachment_abilities.csv",
            "Detachment_abilities.csv",
            "Datasheets_enhancements.csv",
            "Abilities.csv",
            "Detachments.csv",
            "Datasheets_models.csv",
            "Datasheets_models_cost.csv",
            "Datasheets_leader.csv",
            "Source.csv",
            "Factions.csv"
        };

        private const string DefaultBaseUrl = "http://wahapedia.ru/wh40k10ed/";
        private const string DefaultDatabaseFileName = "wahapedia.db";

        public static async Task<bool> DataFetchAsync(string baseUrl = DefaultBaseUrl, string? targetDirectory = null)
        {
            targetDirectory ??= Path.Combine(GetDataDirectory(), "Raw");
            Directory.CreateDirectory(targetDirectory);

            var tasks = DataFileNames.Select(async file =>
            {
                var url = $"{baseUrl.TrimEnd('/')}/{Uri.EscapeDataString(file)}";
                var destinationPath = Path.Combine(targetDirectory, file);

                using var response = await Http.GetAsync(url).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    return false;

                await using var sourceStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                await using var destinationStream = File.Create(destinationPath);

                await sourceStream.CopyToAsync(destinationStream).ConfigureAwait(false);

                return true;
            });

            var results = await Task.WhenAll(tasks);
            return results.All(x => x);
        }

        private static string GetDataDirectory()
        {
            var appData = FileSystem.AppDataDirectory ?? string.Empty;

            if (appData.EndsWith(Path.DirectorySeparatorChar + "Data", StringComparison.OrdinalIgnoreCase) ||
                appData.EndsWith("Data", StringComparison.OrdinalIgnoreCase))
                return appData;

            return Path.Combine(appData, "Data");
        }

        public static string GetDatabasePath(string? databaseFileName = null)
        {
            databaseFileName ??= DefaultDatabaseFileName;
            return Path.Combine(GetDataDirectory(), databaseFileName);
        }

        public static async Task<bool> BuildDatabaseAsync(string? rawDirectory = null, string? dbPath = null, Action<string>? log = null)
        {
            rawDirectory ??= Path.Combine(GetDataDirectory(), "Raw");
            dbPath ??= Path.Combine(GetDataDirectory(), DefaultDatabaseFileName);

            if (!Directory.Exists(rawDirectory))
            {
                Debug.WriteLine("Raw directory does not exist: " + rawDirectory);
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dbPath) ?? GetDataDirectory());

            var connBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            };

            if (File.Exists(dbPath))
            {
                try { File.SetAttributes(dbPath, FileAttributes.Normal); } catch { }
            }

            using var connection = new SqliteConnection(connBuilder.ToString());
            await connection.OpenAsync().ConfigureAwait(false);

            using (var pCmd = connection.CreateCommand())
            {
                pCmd.CommandText = """
                PRAGMA journal_mode = OFF;
                PRAGMA synchronous = OFF;
                PRAGMA temp_store = MEMORY;
                PRAGMA cache_size = 100000;
                PRAGMA busy_timeout = 5000;
                """;

                await pCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            using var transaction = connection.BeginTransaction();

            var tableSchemas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Abilities", "CREATE TABLE IF NOT EXISTS \"Abilities\" ( \"id\" INTEGER, \"name\" TEXT, \"legend\" TEXT, \"faction_id\" TEXT, \"description\" TEXT, PRIMARY KEY(\"id\",\"faction_id\"))" },
                { "Datasheets", "CREATE TABLE IF NOT EXISTS \"Datasheets\" ( \"id\" INTEGER, \"name\" TEXT, \"faction_id\" TEXT, \"source_id\" INTEGER, \"legend\" TEXT, \"role\" TEXT, \"loadout\" TEXT, \"transport\" TEXT, \"virtual\" BOOLEAN, \"leader_head\" TEXT, \"leader_footer\" TEXT, \"damaged_w\" TEXT, \"damaged_description\" TEXT, PRIMARY KEY(\"id\"))" },
                { "Datasheets_abilities", "CREATE TABLE IF NOT EXISTS \"Datasheets_abilities\" ( \"datasheet_id\" INTEGER, \"line\" INTEGER, \"ability_id\" INTEGER, \"name\" TEXT, \"description\" TEXT, \"type\" TEXT, \"parameter\" TEXT, PRIMARY KEY(\"datasheet_id\",\"line\") )" },
                { "Datasheets_detachment_abilities", "CREATE TABLE IF NOT EXISTS \"Datasheets_detachment_abilities\" ( \"datasheet_id\" INTEGER, \"detachment_ability_id\" INTEGER, PRIMARY KEY(\"detachment_ability_id\",\"datasheet_id\") )" },
                { "Datasheets_enhancements", "CREATE TABLE IF NOT EXISTS \"Datasheets_enhancements\" ( \"datasheet_id\" INTEGER, \"enhancement_id\" INTEGER, PRIMARY KEY(\"datasheet_id\",\"enhancement_id\") )" },
                { "Datasheets_keywords", "CREATE TABLE IF NOT EXISTS \"Datasheets_keywords\" ( \"datasheet_id\" INTEGER, \"keyword\" TEXT, \"model\" TEXT, \"is_faction_keyword\" TEXT )" },
                { "Datasheets_leader", "CREATE TABLE IF NOT EXISTS \"Datasheets_leader\" ( \"leader_id\" INTEGER, \"attached_id\" INTEGER )" },
                { "Datasheets_models", "CREATE TABLE IF NOT EXISTS \"Datasheets_models\" ( \"datasheet_id\" INTEGER, \"line\" INTEGER, \"name\" TEXT, \"M\" TEXT, \"T\" TEXT, \"Sv\" TEXT, \"inv_sv\" TEXT, \"inv_sv_descr\" TEXT, \"W\" TEXT, \"Ld\" TEXT, \"OC\" TEXT, \"base_size\" TEXT, \"base_size_descr\" TEXT, PRIMARY KEY(\"datasheet_id\",\"line\") )" },
                { "Datasheets_models_cost", "CREATE TABLE IF NOT EXISTS \"Datasheets_models_cost\" ( \"datasheet_id\" INTEGER, \"line\" INTEGER, \"description\" TEXT, \"cost\" INTEGER, PRIMARY KEY(\"line\",\"datasheet_id\") )" },
                { "Datasheets_options", "CREATE TABLE IF NOT EXISTS \"Datasheets_options\" ( \"datasheet_id\" INTEGER, \"line\" INTEGER, \"button\" TEXT, \"description\" TEXT, PRIMARY KEY(\"line\",\"datasheet_id\") )" },
                { "Datasheets_stratagems", "CREATE TABLE IF NOT EXISTS \"Datasheets_stratagems\" ( \"datasheet_id\" INTEGER, \"stratagem_id\" INTEGER, PRIMARY KEY(\"stratagem_id\",\"datasheet_id\") )" },
                { "Datasheets_wargear", "CREATE TABLE IF NOT EXISTS \"Datasheets_wargear\" ( \"datasheet_id\" INTEGER, \"line\" INTEGER, \"line_in_wargear\" INTEGER, \"dice\" TEXT, \"name\" TEXT, \"description\" TEXT, \"range\" TEXT, \"type\" TEXT, \"A\" TEXT, \"BS_WS\" TEXT, \"S\" TEXT, \"AP\" TEXT, \"D\" TEXT )" },
                { "Detachment_abilities", "CREATE TABLE IF NOT EXISTS \"Detachment_abilities\" ( \"id\" INTEGER, \"faction_id\" TEXT, \"name\" TEXT, \"legend\" TEXT, \"description\" TEXT, \"detachment\" TEXT, \"detachment_id\" INTEGER, PRIMARY KEY(\"id\",\"faction_id\",\"detachment_id\") )" },
                { "Detachments", "CREATE TABLE IF NOT EXISTS \"Detachments\" ( \"id\" INTEGER, \"faction_id\" TEXT, \"name\" TEXT, \"legend\" TEXT, \"type\" TEXT, PRIMARY KEY(\"id\",\"faction_id\") )" },
                { "Enhancements", "CREATE TABLE IF NOT EXISTS \"Enhancements\" ( \"id\" INTEGER, \"faction_id\" TEXT, \"name\" TEXT, \"cost\" INTEGER, \"detachment\" TEXT, \"detachment_id\" INTEGER, \"legend\" TEXT, \"description\" TEXT, PRIMARY KEY(\"faction_id\",\"id\",\"detachment_id\") )" },
                { "Factions", "CREATE TABLE IF NOT EXISTS \"Factions\" ( \"id\" TEXT, \"name\" TEXT, PRIMARY KEY(\"id\") )" },
                { "Source", "CREATE TABLE IF NOT EXISTS \"Source\" ( \"id\" INTEGER, \"name\" TEXT, \"type\" TEXT, \"edition\" TEXT, \"version\" TEXT, \"errata_date\" TEXT, \"errata_link\" TEXT )" },
                { "Stratagems", "CREATE TABLE IF NOT EXISTS \"Stratagems\" ( \"id\" INTEGER, \"faction_id\" TEXT, \"name\" TEXT, \"type\" TEXT, \"cp_cost\" TEXT, \"legend\" TEXT, \"turn\" TEXT, \"phase\" TEXT, \"detachment\" TEXT, \"detachment_id\" INTEGER, \"description\" TEXT, PRIMARY KEY(\"faction_id\",\"detachment_id\",\"id\") )" }
            };

            foreach (var kv in tableSchemas)
            {
                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = kv.Value;
                createCmd.Transaction = transaction;
                await createCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            async Task<bool> InsertCsvIntoTableAsync(string filePath, string tableName)
            {
                using var sr = new StreamReader(filePath, Encoding.UTF8);

                var headerLine = await sr.ReadLineAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(headerLine))
                    return false;

                var headers = ParseCsvLine(headerLine).Select(h => h.Trim()).ToList();

                var headerMap = headers
                    .Select((h, i) => new { h, i })
                    .ToDictionary(x => x.h, x => x.i, StringComparer.OrdinalIgnoreCase);

                var createSql = tableSchemas[tableName];

                var matches = Regex.Matches(createSql, "\"([^\"]+)\"\\s+([A-Za-z]+)", RegexOptions.IgnoreCase);

                var columnNames = new List<string>();
                var columnTypes = new List<string>();

                foreach (Match m in matches)
                {
                    columnNames.Add(m.Groups[1].Value);
                    columnTypes.Add(m.Groups[2].Value);
                }

                var paramNames = Enumerable.Range(0, columnNames.Count).Select(i => $"@p{i}").ToArray();

                var insertSql = $"INSERT OR IGNORE INTO \"{tableName}\" ({string.Join(", ", columnNames.Select(c => $"\"{c}\""))}) VALUES ({string.Join(", ", paramNames)});";

                using var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = insertSql;
                insertCmd.Transaction = transaction;

                for (int i = 0; i < paramNames.Length; i++)
                    insertCmd.Parameters.Add(new SqliteParameter(paramNames[i], DBNull.Value));

                await insertCmd.PrepareAsync().ConfigureAwait(false);

                string? line;

                while ((line = await sr.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var fields = ParseCsvLine(line);

                    for (int i = 0; i < columnNames.Count; i++)
                    {
                        var col = columnNames[i];

                        headerMap.TryGetValue(col, out var hIndex);

                        string? value = null;

                        if (hIndex >= 0 && hIndex < fields.Count)
                            value = fields[hIndex];

                        var colType = columnTypes[i];

                        if (colType.Contains("INTEGER", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(value, out var iv))
                                insertCmd.Parameters[i].Value = iv;
                            else
                                insertCmd.Parameters[i].Value = DBNull.Value;
                        }
                        else if (colType.Contains("BOOLEAN", StringComparison.OrdinalIgnoreCase) || col.Equals("virtual", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrWhiteSpace(value) && (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase)))
                                insertCmd.Parameters[i].Value = 1;
                            else if (!string.IsNullOrWhiteSpace(value) && (value == "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase)))
                                insertCmd.Parameters[i].Value = 0;
                            else
                                insertCmd.Parameters[i].Value = DBNull.Value;
                        }
                        else
                        {
                            insertCmd.Parameters[i].Value = value ?? string.Empty;
                        }
                    }

                    await insertCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                return true;
            }

            foreach (var file in DataFileNames)
            {
                var filePath = Path.Combine(rawDirectory, file);

                if (!File.Exists(filePath))
                    continue;

                var tableName = Path.GetFileNameWithoutExtension(file);

                if (tableSchemas.ContainsKey(tableName))
                {
                    try
                    {
                        await InsertCsvIntoTableAsync(filePath, tableName).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to import {filePath}: {ex.Message}");
                    }
                }

                TryDeleteFile(filePath);
            }

            await WahaDataPatcher.ApplyPatchesAsync(connection, transaction, log).ConfigureAwait(false);

            var indexes = new[]
            {
                "CREATE INDEX IF NOT EXISTS idx_datasheets_faction ON Datasheets(faction_id)",
                "CREATE INDEX IF NOT EXISTS idx_datasheets_keywords_datasheet ON Datasheets_keywords(datasheet_id)",
                "CREATE INDEX IF NOT EXISTS idx_datasheets_abilities_datasheet ON Datasheets_abilities(datasheet_id)",
                "CREATE INDEX IF NOT EXISTS idx_wargear_datasheet ON Datasheets_wargear(datasheet_id)",
                "CREATE INDEX IF NOT EXISTS idx_abilities_faction ON Abilities(faction_id)"
            };

            foreach (var sql in indexes)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = sql;
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            transaction.Commit();

            await connection.CloseAsync().ConfigureAwait(false);

            return true;
        }

        private static List<string> ParseCsvLine(string line)
        {
            return line.Split('|').ToList();
        }

        public static async Task<bool> RebuildDatabaseAsync(Action<string>? log = null)
        {
            string dataDir = GetDataDirectory();
            string rawDirectory = Path.Combine(dataDir, "Raw");
            string finalDbPath = GetDatabasePath();
            string tempDbPath = Path.Combine(dataDir, $"temp_{Guid.NewGuid():N}.db");
            bool isRebuild = File.Exists(finalDbPath);

            Directory.CreateDirectory(rawDirectory);
            Directory.CreateDirectory(dataDir);

            var msg = isRebuild ? "=== Rebuilding Existing Database ===" : "=== Building New Database ===";
            Debug.WriteLine(msg);
            log?.Invoke(msg);

            // Step 1: Download CSV files
            msg = "[1/5] Downloading Wahapedia data from web...";
            Debug.WriteLine(msg);
            log?.Invoke(msg);

            if (!await DataFetchAsync(DefaultBaseUrl, rawDirectory))
            {
                msg = "  ✗ ERROR: Data download failed.";
                Debug.WriteLine(msg);
                log?.Invoke(msg);
                return false;
            }

            msg = "  ✓ Download complete.";
            Debug.WriteLine(msg);
            log?.Invoke(msg);

            // Step 2: Build database to temporary file
            msg = "[2/5] Building database from CSV files...";
            Debug.WriteLine(msg);
            log?.Invoke(msg);

            msg = $"  Temp location: {Path.GetFileName(tempDbPath)}";
            Debug.WriteLine(msg);
            log?.Invoke(msg);

            if (!await BuildDatabaseAsync(rawDirectory, tempDbPath, log))
            {
                msg = "  ✗ ERROR: Database build failed.";
                Debug.WriteLine(msg);
                log?.Invoke(msg);
                TryDeleteFile(tempDbPath);
                return false;
            }

            msg = "  ✓ Database built successfully.";
            Debug.WriteLine(msg);
            log?.Invoke(msg);

            // CRITICAL: Clear connection pools for the temp database we just built
            msg = "[3/5] Releasing all database connections...";
            Debug.WriteLine(msg);
            log?.Invoke(msg);

            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(500);

            msg = "  ✓ Connections released.";
            Debug.WriteLine(msg);
            log?.Invoke(msg);

            // Step 3: Delete old database and related files
            if (isRebuild)
            {
                msg = "[4/5] Removing old database files...";
                Debug.WriteLine(msg);
                log?.Invoke(msg);

                // Clear ALL SQLite connection pools to release file handles
                SqliteConnection.ClearAllPools();

                // Force GC to release any remaining handles
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // Give OS time to release file handles
                await Task.Delay(1000);

                if (!TryDeleteFile(finalDbPath))
                {
                    msg = "  ✗ ERROR: Cannot delete old database - file is in use.";
                    Debug.WriteLine(msg);
                    log?.Invoke(msg);

                    msg = "  Please close all connections or restart the application.";
                    Debug.WriteLine(msg);
                    log?.Invoke(msg);

                    TryDeleteFile(tempDbPath);
                    return false;
                }

                TryDeleteFile(finalDbPath + "-wal");
                TryDeleteFile(finalDbPath + "-shm");

                msg = "  ✓ Old files removed.";
                Debug.WriteLine(msg);
                log?.Invoke(msg);
            }
            else
            {
                msg = "[4/5] No existing database to remove.";
                Debug.WriteLine(msg);
                log?.Invoke(msg);
            }

            // Step 4: Move temp database to final location
            msg = "[5/5] Installing new database...";
            Debug.WriteLine(msg);
            log?.Invoke(msg);

            try
            {
                File.Move(tempDbPath, finalDbPath);
                msg = $"  ✓ Database installed at: {Path.GetFileName(finalDbPath)}";
                Debug.WriteLine(msg);
                log?.Invoke(msg);
            }
            catch (Exception ex)
            {
                msg = $"  ✗ ERROR: Failed to install database: {ex.Message}";
                Debug.WriteLine(msg);
                log?.Invoke(msg);
                TryDeleteFile(tempDbPath);
                return false;
            }

            // Step 5: Enable WAL mode
            msg = "Configuring database (enabling WAL mode)...";
            Debug.WriteLine(msg);
            log?.Invoke(msg);

            try
            {
                await using var connection = WahaSQLiteService.CreateWriteConnection(finalDbPath);
                await connection.OpenAsync();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
                await cmd.ExecuteNonQueryAsync();

                msg = "  ✓ WAL mode enabled.";
                Debug.WriteLine(msg);
                log?.Invoke(msg);
            }
            catch (Exception ex)
            {
                msg = $"  ⚠ WARNING: Could not enable WAL mode: {ex.Message}";
                Debug.WriteLine(msg);
                log?.Invoke(msg);
            }

            msg = isRebuild ? "=== Database Rebuild Complete ===" : "=== Database Build Complete ===";
            Debug.WriteLine(msg);
            log?.Invoke(msg);

            return true;
        }

        private static bool TryDeleteFile(string path)
        {
            if (!File.Exists(path))
                return true;

            try
            {
                File.Delete(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string CleanHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return "";

            html = WebUtility.HtmlDecode(html);

            html = html.Replace("\"\"\"", "\"");

            html = Regex.Replace(html, @"data-tooltip-content=""[^""]*""", "");
            html = Regex.Replace(html, @"data-tooltip-anchor=""[^""]*""", "");

            html = Regex.Replace(html, @"tooltip\d+", "");

            html = Regex.Replace(html, @"<script[\s\S]*?</script>", "", RegexOptions.IgnoreCase);

            html = Regex.Replace(html, @"<a[^>]*>(.*?)</a>", "$1", RegexOptions.IgnoreCase);

            html = html.Replace("font-family: 'ConduitITC', arial;", "");

            html = html.Replace("<tbody><tbody>", "<tbody>");
            html = html.Replace("</tbody></tbody>", "</tbody>");

            html = html.Replace("max-width=\"480px\"", "");

            return html;
        }
    }
}
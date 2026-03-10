using OmniTactica.AppCode.Data.Database;
using OmniTactica.AppCode.Data.Tables;
using OmniTactica.AppCode.Models.Core;
using OmniTactica.AppCode.Models.Rules;

namespace OmniTactica.AppCode.Repositories
{
    /// <summary>
    /// Repository for loading Faction data with associated game content.
    /// </summary>
    public class FactionRepository : TableBase
    {
        public FactionRepository(WahaSQLiteService db) : base(db) { }

        /// <summary>
        /// Gets all factions with basic info (no child collections).
        /// </summary>
        public async Task<List<Faction>> GetAllFactionsAsync()
        {
            const string sql = "SELECT id, name FROM Factions ORDER BY name";

            return await QueryListAsync(sql, r => new Faction
            {
                Id = S(r, "id"),
                Name = S(r, "name")
            });
        }

        /// <summary>
        /// Gets a single faction by ID with basic info.
        /// </summary>
        public async Task<Faction?> GetFactionAsync(string factionId)
        {
            const string sql = "SELECT id, name FROM Factions WHERE id = @id";

            return await QuerySingleAsync(sql, r => new Faction
            {
                Id = S(r, "id"),
                Name = S(r, "name")
            }, ("@id", factionId));
        }

        /// <summary>
        /// Gets a faction with all its abilities loaded.
        /// Optionally filters by selected keywords with AND/OR logic.
        /// </summary>
        public async Task<Faction?> GetFactionWithAbilitiesAsync(string factionId, List<string>? keywordFilters = null, bool useAndLogic = false)
        {
            var faction = await GetFactionAsync(factionId);
            if (faction == null)
                return null;

            const string abilitySql = """
                SELECT id, name, legend, faction_id, description 
                FROM Abilities 
                WHERE faction_id = @factionId
                ORDER BY name
                """;

            var abilities = await QueryListAsync(abilitySql, r => new Ability
            {
                Id = I(r, "id") ?? 0,
                Name = S(r, "name"),
                Legend = S(r, "legend"),
                FactionId = S(r, "faction_id"),
                Description = S(r, "description")
            }, ("@factionId", factionId));

            // Filter abilities based on keywords
            var filteredAbilities = abilities;

            if (keywordFilters != null && keywordFilters.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[FactionRepository] Filtering abilities with {keywordFilters.Count} keywords ({(useAndLogic ? "AND" : "OR")} logic)");

                filteredAbilities = abilities
                    .Where(a =>
                    {
                        var combinedText = $"{a.Legend} {a.Description}";

                        if (useAndLogic)
                        {
                            // AND logic: ability must mention ALL keywords
                            var matchesAll = keywordFilters.All(kw =>
                                combinedText.Contains(kw, StringComparison.OrdinalIgnoreCase));

                            if (matchesAll)
                                System.Diagnostics.Debug.WriteLine($"  [{a.Name}] matches ALL keywords -> INCLUDE");
                            else
                                System.Diagnostics.Debug.WriteLine($"  [{a.Name}] doesn't match all keywords -> EXCLUDE");

                            return matchesAll;
                        }
                        else
                        {
                            // OR logic: ability must mention ANY keyword
                            var matchesAny = keywordFilters.Any(kw =>
                                combinedText.Contains(kw, StringComparison.OrdinalIgnoreCase));

                            if (matchesAny)
                            {
                                var matched = keywordFilters.Where(kw => combinedText.Contains(kw, StringComparison.OrdinalIgnoreCase)).ToList();
                                System.Diagnostics.Debug.WriteLine($"  [{a.Name}] matches keywords: {string.Join(", ", matched)} -> INCLUDE");
                            }
                            else
                                System.Diagnostics.Debug.WriteLine($"  [{a.Name}] doesn't match any keyword -> EXCLUDE");

                            return matchesAny;
                        }
                    })
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"[FactionRepository] Result: {filteredAbilities.Count} abilities after filtering");
            }

            faction.Abilities = filteredAbilities;

            return faction;
        }

        /// <summary>
        /// Gets all keywords used in datasheets for a faction.
        /// Returns distinct list of keywords sorted alphabetically.
        /// </summary>
        public async Task<List<string>> GetAllKeywordsForFactionAsync(string factionId)
        {
            const string sql = """
                SELECT dk.keyword
                FROM Datasheets_keywords dk
                JOIN Datasheets d ON dk.datasheet_id = d.id
                WHERE dk.keyword != '' AND d.faction_id = @factionId
                GROUP BY dk.keyword
                HAVING COUNT(DISTINCT dk.datasheet_id) > 1
                ORDER BY dk.keyword;
                """;

            return await QueryListAsync(sql, r => S(r, "keyword"), ("@factionId", factionId));
        }

        /// <summary>
        /// Gets all keywords with their usage counts for a faction.
        /// Returns keywords grouped by frequency: common (>1 use) and unique (1 use).
        /// </summary>
        public async Task<(List<string> CommonKeywords, List<string> UniqueKeywords)> GetKeywordsGroupedByFrequencyAsync(string factionId)
        {
            const string sql = """
                SELECT dk.keyword, COUNT(DISTINCT dk.datasheet_id) as usage_count
                FROM Datasheets_keywords dk
                JOIN Datasheets d ON dk.datasheet_id = d.id
                WHERE dk.keyword != '' AND d.faction_id = @factionId
                GROUP BY dk.keyword
                ORDER BY usage_count DESC, dk.keyword
                """;

            await using var conn = _db.CreateConnection();
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@factionId", factionId);

            var commonKeywords = new List<string>();
            var uniqueKeywords = new List<string>();

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var keyword = S(reader, "keyword");
                var count = I(reader, "usage_count") ?? 0;

                if (count > 1)
                    commonKeywords.Add(keyword);
                else
                    uniqueKeywords.Add(keyword);
            }

            return (commonKeywords, uniqueKeywords);
        }
    }
}

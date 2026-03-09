using ListBuilder.AppCode.Data.Database;
using ListBuilder.AppCode.Data.Tables;
using ListBuilder.AppCode.Models.Core;
using ListBuilder.AppCode.Models.Rules;

namespace ListBuilder.AppCode.Repositories
{
    /// <summary>
    /// Repository for loading Detachment data.
    /// </summary>
    public class DetachmentRepository : TableBase
    {
        public DetachmentRepository(WahaSQLiteService db) : base(db) { }

        /// <summary>
        /// Gets all detachments for a specific faction.
        /// Optionally filters by keywords - only shows detachments whose abilities, stratagems, or enhancements mention the keywords.
        /// </summary>
        public async Task<List<Detachment>> GetByFactionAsync(string factionId, List<string>? keywordFilters = null, bool useAndLogic = false)
        {
            const string sql = """
                SELECT id, faction_id, name, legend, type 
                FROM Detachments 
                WHERE faction_id = @factionId
                ORDER BY name
                """;

            var tables = await QueryListAsync(sql, r => new DetachmentTable(
                Id: I(r, "id") ?? 0,
                FactionId: S(r, "faction_id"),
                Name: S(r, "name"),
                Legend: S(r, "legend"),
                Type: S(r, "type")
            ), ("@factionId", factionId));

            // Filter detachments based on keywords
            var filteredDetachments = tables;

            if (keywordFilters != null && keywordFilters.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[DetachmentRepository] Filtering {tables.Count} detachments with {keywordFilters.Count} keywords ({(useAndLogic ? "AND" : "OR")} logic)");

                var matchingDetachments = new List<DetachmentTable>();

                foreach (var detachment in tables)
                {
                    var hasMatch = await DetachmentMatchesKeywordsAsync(detachment.Id, keywordFilters, useAndLogic);

                    if (hasMatch)
                    {
                        System.Diagnostics.Debug.WriteLine($"  [{detachment.Name}] matches -> INCLUDE");
                        matchingDetachments.Add(detachment);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"  [{detachment.Name}] no match -> EXCLUDE");
                    }
                }

                filteredDetachments = matchingDetachments;
                System.Diagnostics.Debug.WriteLine($"[DetachmentRepository] Result: {filteredDetachments.Count} detachments after filtering");
            }

            return filteredDetachments.Select(MapFromTable).ToList();
        }

        /// <summary>
        /// Checks if a detachment's abilities, stratagems, or enhancements mention the specified keywords.
        /// </summary>
        private async Task<bool> DetachmentMatchesKeywordsAsync(int detachmentId, List<string> keywords, bool useAndLogic)
        {
            // Get all text content from detachment abilities, stratagems, and enhancements
            const string sql = """
                SELECT description FROM Detachment_abilities WHERE detachment_id = @id
                UNION ALL
                SELECT description FROM Stratagems WHERE detachment_id = @id
                UNION ALL
                SELECT description FROM Enhancements WHERE detachment_id = @id
                UNION ALL
                SELECT legend as description FROM Detachments WHERE id = @id
                UNION ALL
                SELECT legend as description FROM Detachment_abilities WHERE detachment_id = @id
                """;

            var textContents = await QueryListAsync(sql, r => S(r, "description"), ("@id", detachmentId));
            var combinedText = string.Join(" ", textContents);

            if (useAndLogic)
            {
                // AND logic: detachment must mention ALL keywords
                return keywords.All(kw => combinedText.Contains(kw, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                // OR logic: detachment must mention ANY keyword
                return keywords.Any(kw => combinedText.Contains(kw, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Gets a single detachment with all its content loaded (abilities, stratagems, enhancements).
        /// </summary>
        public async Task<Detachment?> GetDetachmentWithDetailsAsync(int detachmentId)
        {
            const string sql = """
                SELECT id, faction_id, name, legend, type 
                FROM Detachments 
                WHERE id = @id
                """;

            var table = await QuerySingleAsync(sql, r => new DetachmentTable(
                Id: I(r, "id") ?? 0,
                FactionId: S(r, "faction_id"),
                Name: S(r, "name"),
                Legend: S(r, "legend"),
                Type: S(r, "type")
            ), ("@id", detachmentId));

            if (table == null)
                return null;

            var detachment = MapFromTable(table);

            // Load detachment-specific abilities
            const string abilitySql = """
                SELECT id, faction_id, name, legend, description, detachment, detachment_id
                FROM Detachment_abilities
                WHERE detachment_id = @detachmentId
                ORDER BY name
                """;

            var abilityTables = await QueryListAsync(abilitySql, r => new DetachmentAbilityTable(
                Id: I(r, "id") ?? 0,
                FactionId: S(r, "faction_id"),
                Name: S(r, "name"),
                Legend: S(r, "legend"),
                Description: S(r, "description"),
                Detachment: S(r, "detachment"),
                DetachmentId: I(r, "detachment_id") ?? 0
            ), ("@detachmentId", detachmentId));

            detachment.Abilities = abilityTables.Select(MapDetachmentAbilityFromTable).ToList();

            // Load stratagems
            const string stratagemSql = """
                SELECT id, faction_id, name, type, cp_cost, legend, turn, phase, detachment, detachment_id, description
                FROM Stratagems
                WHERE detachment_id = @detachmentId
                ORDER BY name
                """;

            var stratagemTables = await QueryListAsync(stratagemSql, r => new StratagemTable(
                Id: I(r, "id") ?? 0,
                FactionId: S(r, "faction_id"),
                Name: S(r, "name"),
                Type: S(r, "type"),
                CpCost: S(r, "cp_cost"),
                Legend: S(r, "legend"),
                Turn: S(r, "turn"),
                Phase: S(r, "phase"),
                Detachment: S(r, "detachment"),
                DetachmentId: I(r, "detachment_id") ?? 0,
                Description: S(r, "description")
            ), ("@detachmentId", detachmentId));

            detachment.Stratagems = stratagemTables.Select(MapStratagemFromTable).ToList();

            // Load enhancements
            const string enhancementSql = """
                SELECT id, faction_id, name, cost, detachment, detachment_id, legend, description
                FROM Enhancements
                WHERE detachment_id = @detachmentId
                ORDER BY name
                """;

            var enhancementTables = await QueryListAsync(enhancementSql, r => new EnhancementTable(
                Id: I(r, "id") ?? 0,
                FactionId: S(r, "faction_id"),
                Name: S(r, "name"),
                Cost: I(r, "cost") ?? 0,
                Detachment: S(r, "detachment"),
                DetachmentId: I(r, "detachment_id") ?? 0,
                Legend: S(r, "legend"),
                Description: S(r, "description")
            ), ("@detachmentId", detachmentId));

            detachment.Enhancements = enhancementTables.Select(MapEnhancementFromTable).ToList();

            return detachment;
        }

        private static Detachment MapFromTable(DetachmentTable table) => new()
        {
            Id = table.Id,
            FactionId = table.FactionId,
            Name = table.Name,
            Legend = table.Legend,
            Type = table.Type
        };

        private static DetachmentAbility MapDetachmentAbilityFromTable(DetachmentAbilityTable table) => new()
        {
            Id = table.Id,
            FactionId = table.FactionId,
            Name = table.Name,
            Legend = table.Legend,
            Description = table.Description,
            Detachment = table.Detachment,
            DetachmentId = table.DetachmentId
        };

        private static Stratagem MapStratagemFromTable(StratagemTable table) => new()
        {
            Id = table.Id,
            FactionId = table.FactionId,
            Name = table.Name,
            Type = table.Type,
            CpCost = table.CpCost,
            Legend = table.Legend,
            Turn = table.Turn,
            Phase = table.Phase,
            Detachment = table.Detachment,
            DetachmentId = table.DetachmentId,
            Description = table.Description
        };

        private static Enhancement MapEnhancementFromTable(EnhancementTable table) => new()
        {
            Id = table.Id,
            FactionId = table.FactionId,
            Name = table.Name,
            Cost = table.Cost,
            Detachment = table.Detachment,
            DetachmentId = table.DetachmentId,
            Legend = table.Legend,
            Description = table.Description
        };
    }
}

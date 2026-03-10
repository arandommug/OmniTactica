using OmniTactica.AppCode.Data.Database;
using OmniTactica.AppCode.Data.Tables;
using OmniTactica.AppCode.Models.Core;
using OmniTactica.AppCode.Models.Rules;

namespace OmniTactica.AppCode.Repositories
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

            var detachments = await QueryListAsync(sql, r => new Detachment
            {
                Id = I(r, "id") ?? 0,
                FactionId = S(r, "faction_id"),
                Name = S(r, "name"),
                Legend = S(r, "legend"),
                Type = S(r, "type")
            }, ("@factionId", factionId));

            // Filter detachments based on keywords
            var filteredDetachments = detachments;

            if (keywordFilters != null && keywordFilters.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[DetachmentRepository] Filtering {detachments.Count} detachments with {keywordFilters.Count} keywords ({(useAndLogic ? "AND" : "OR")} logic)");

                var matchingDetachments = new List<Detachment>();

                foreach (var detachment in detachments)
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

            return filteredDetachments;
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

            var detachment = await QuerySingleAsync(sql, r => new Detachment
            {
                Id = I(r, "id") ?? 0,
                FactionId = S(r, "faction_id"),
                Name = S(r, "name"),
                Legend = S(r, "legend"),
                Type = S(r, "type")
            }, ("@id", detachmentId));

            if (detachment == null)
                return null;

            // Load detachment-specific abilities
            const string abilitySql = """
                SELECT id, faction_id, name, legend, description, detachment, detachment_id
                FROM Detachment_abilities
                WHERE detachment_id = @detachmentId
                ORDER BY name
                """;

            detachment.Abilities = await QueryListAsync(abilitySql, r => new DetachmentAbility
            {
                Id = I(r, "id") ?? 0,
                FactionId = S(r, "faction_id"),
                Name = S(r, "name"),
                Legend = S(r, "legend"),
                Description = S(r, "description"),
                Detachment = S(r, "detachment"),
                DetachmentId = I(r, "detachment_id") ?? 0
            }, ("@detachmentId", detachmentId));

            // Load stratagems
            const string stratagemSql = """
                SELECT id, faction_id, name, type, cp_cost, legend, turn, phase, detachment, detachment_id, description
                FROM Stratagems
                WHERE detachment_id = @detachmentId
                ORDER BY name
                """;

            detachment.Stratagems = await QueryListAsync(stratagemSql, r => new Stratagem
            {
                Id = I(r, "id") ?? 0,
                FactionId = S(r, "faction_id"),
                Name = S(r, "name"),
                Type = S(r, "type"),
                CpCost = S(r, "cp_cost"),
                Legend = S(r, "legend"),
                Turn = S(r, "turn"),
                Phase = S(r, "phase"),
                Detachment = S(r, "detachment"),
                DetachmentId = I(r, "detachment_id") ?? 0,
                Description = S(r, "description")
            }, ("@detachmentId", detachmentId));

            // Load enhancements
            const string enhancementSql = """
                SELECT id, faction_id, name, cost, detachment, detachment_id, legend, description
                FROM Enhancements
                WHERE detachment_id = @detachmentId
                ORDER BY name
                """;

            detachment.Enhancements = await QueryListAsync(enhancementSql, r => new Enhancement
            {
                Id = I(r, "id") ?? 0,
                FactionId = S(r, "faction_id"),
                Name = S(r, "name"),
                Cost = I(r, "cost") ?? 0,
                Detachment = S(r, "detachment"),
                DetachmentId = I(r, "detachment_id") ?? 0,
                Legend = S(r, "legend"),
                Description = S(r, "description")
            }, ("@detachmentId", detachmentId));

            return detachment;
        }
    }
}

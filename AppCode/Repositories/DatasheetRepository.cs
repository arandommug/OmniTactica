using OmniTactica.AppCode.Data.Database;
using OmniTactica.AppCode.Data.Tables;
using OmniTactica.AppCode.Models.Core;
using OmniTactica.AppCode.Models.Rules;
using OmniTactica.AppCode.Helpers;

namespace OmniTactica.AppCode.Repositories
{
    /// <summary>
    /// Repository for loading Datasheet data with associated game content.
    /// </summary>
    public class DatasheetRepository : TableBase
    {
        public DatasheetRepository(WahaSQLiteService db) : base(db) { }

            /// <summary>
            /// Gets basic datasheet info by ID.
            /// </summary>
            public async Task<Datasheet?> GetDatasheetByIdAsync(int datasheetId)
            {
                const string sql = "SELECT id, name, faction_id, role, legend FROM Datasheets WHERE id = @id";

                return await QuerySingleAsync(sql, r => new Datasheet
                {
                    Id = I(r, "id") ?? 0,
                    Name = S(r, "name"),
                    FactionId = S(r, "faction_id"),
                    Role = S(r, "role"),
                    Legend = S(r, "legend")
                }, ("@id", datasheetId));
            }

            /// <summary>
            /// Gets all datasheets for a faction (list view).
            /// Optionally filters by selected keywords with AND/OR logic.
            /// Supports both include and exclude keyword filters.
            /// </summary>
        public async Task<List<Datasheet>> GetDatasheetsByFactionAsync(
            string factionId,
            List<string>? includeKeywords = null,
            List<string>? excludeKeywords = null,
            bool useAndLogic = false)
        {
            var sql = @"
                SELECT DISTINCT
                    d.id,
                    d.name,
                    d.faction_id,
                    d.role,
                    d.legend
                FROM Datasheets d";

            var conditions = new List<string> { "d.faction_id = @factionId", "d.virtual = 0" };
            var parameters = new List<(string, object)> { ("@factionId", factionId) };

            // Handle include keywords
            if (includeKeywords != null && includeKeywords.Count > 0)
            {
                sql += " INNER JOIN Datasheets_keywords dk ON d.id = dk.datasheet_id";

                if (useAndLogic)
                {
                    // AND logic: must have ALL keywords
                    for (int i = 0; i < includeKeywords.Count; i++)
                    {
                        var paramName = $"@kw{i}";
                        conditions.Add($@"
                            EXISTS (
                                SELECT 1 FROM Datasheets_keywords dk{i}
                                WHERE dk{i}.datasheet_id = d.id
                                AND dk{i}.keyword = {paramName}
                            )");
                        parameters.Add((paramName, includeKeywords[i]));
                    }
                }
                else
                {
                    // OR logic: must have ANY keyword
                    var kwParams = includeKeywords.Select((_, i) => $"@kw{i}").ToList();
                    conditions.Add($"dk.keyword IN ({string.Join(", ", kwParams)})");
                    for (int i = 0; i < includeKeywords.Count; i++)
                    {
                        parameters.Add(($"@kw{i}", includeKeywords[i]));
                    }
                }
            }

            // Handle exclude keywords - must NOT have any of these
            if (excludeKeywords != null && excludeKeywords.Count > 0)
            {
                for (int i = 0; i < excludeKeywords.Count; i++)
                {
                    var paramName = $"@ex{i}";
                    conditions.Add($@"
                        NOT EXISTS (
                            SELECT 1 FROM Datasheets_keywords dkex{i}
                            WHERE dkex{i}.datasheet_id = d.id
                            AND dkex{i}.keyword = {paramName}
                        )");
                    parameters.Add((paramName, excludeKeywords[i]));
                }
            }

            sql += $" WHERE {string.Join(" AND ", conditions)} ORDER BY d.name";

            var datasheets = await QueryListAsync(sql, r => new Datasheet
            {
                Id = I(r, "id") ?? 0,
                Name = S(r, "name"),
                FactionId = S(r, "faction_id"),
                Role = S(r, "role"),
                Legend = S(r, "legend")
            }, parameters.ToArray());

            // Load keywords and costs for each datasheet
            foreach (var datasheet in datasheets)
            {
                datasheet.Keywords = await GetKeywordsForDatasheetAsync(datasheet.Id);
                datasheet.PointsCost = await GetBaseCostForDatasheetAsync(datasheet.Id);
            }

            return datasheets;
        }

        /// <summary>
        /// Gets a complete datasheet with all details.
        /// Optionally filters enhancements, stratagems, and abilities by detachment.
        /// </summary>
        public async Task<DatasheetDetail?> GetDatasheetDetailAsync(int datasheetId, int? detachmentId = null)
        {
            // Get basic datasheet info
            const string baseSql = @"
                SELECT
                    id, name, faction_id, legend, role, loadout, transport,
                    virtual, leader_head, leader_footer, damaged_w, damaged_description
                FROM Datasheets
                WHERE id = @id";

            var datasheet = await QuerySingleAsync(baseSql, r => new DatasheetDetail
            {
                Id = I(r, "id") ?? 0,
                Name = S(r, "name"),
                FactionId = S(r, "faction_id"),
                Legend = S(r, "legend"),
                Role = S(r, "role"),
                Loadout = S(r, "loadout"),
                Transport = S(r, "transport"),
                Virtual = I(r, "virtual") == 1,
                LeaderHead = S(r, "leader_head"),
                LeaderFooter = S(r, "leader_footer"),
                DamagedW = S(r, "damaged_w"),
                DamagedDescription = S(r, "damaged_description")
            }, ("@id", datasheetId));

            if (datasheet == null)
                return null;

            // Load keywords first as they're needed for filtering Core Wargear Stratagems
            await LoadKeywordsAsync(datasheet);

            // Load all other related data in parallel
            var tasksToAwait = new Task[]
            {
                LoadModelsAsync(datasheet),
                LoadWargearAsync(datasheet),
                LoadAbilitiesAsync(datasheet),
                LoadOptionsAsync(datasheet),
                LoadCostsAsync(datasheet),
                LoadLeaderRelationshipsAsync(datasheet),
                LoadDetachmentAbilitiesAsync(datasheet, detachmentId),
                LoadEnhancementsAsync(datasheet, detachmentId),
                LoadStratagemsAsync(datasheet, detachmentId)
            };

            await Task.WhenAll(tasksToAwait);

            return datasheet;
        }

        private async Task LoadModelsAsync(DatasheetDetail datasheet)
        {
            const string sql = @"
                SELECT line, name, M, T, Sv, inv_sv, inv_sv_descr, W, Ld, OC, base_size, base_size_descr
                FROM Datasheets_models
                WHERE datasheet_id = @id
                ORDER BY line";

            datasheet.Models = await QueryListAsync(sql, r => new DatasheetModel
            {
                Line = I(r, "line") ?? 0,
                Name = S(r, "name"),
                M = S(r, "M"),
                T = S(r, "T"),
                Sv = S(r, "Sv"),
                InvSv = S(r, "inv_sv"),
                InvSvDescr = S(r, "inv_sv_descr"),
                W = S(r, "W"),
                Ld = S(r, "Ld"),
                OC = S(r, "OC"),
                BaseSize = S(r, "base_size"),
                BaseSizeDescr = S(r, "base_size_descr")
            }, ("@id", datasheet.Id));
        }

        private async Task LoadWargearAsync(DatasheetDetail datasheet)
        {
            const string sql = @"
                SELECT line, line_in_wargear, dice, name, description, range, type, A, BS_WS, S, AP, D
                FROM Datasheets_wargear
                WHERE datasheet_id = @id
                ORDER BY line, line_in_wargear";

            datasheet.Wargear = await QueryListAsync(sql, r => new DatasheetWargear
            {
                Line = I(r, "line") ?? 0,
                LineInWargear = I(r, "line_in_wargear") ?? 0,
                Dice = S(r, "dice"),
                Name = S(r, "name"),
                Description = S(r, "description"),
                Range = S(r, "range"),
                Type = S(r, "type"),
                A = S(r, "A"),
                BsWs = S(r, "BS_WS"),
                S = S(r, "S"),
                AP = S(r, "AP"),
                D = S(r, "D")
            }, ("@id", datasheet.Id));
        }

        private async Task LoadAbilitiesAsync(DatasheetDetail datasheet)
        {
            const string sql = @"
                SELECT 
                    da.line, 
                    da.ability_id, 
                    CASE WHEN da.ability_id is null THEN da.name ELSE a.name END as name,
                    CASE WHEN da.ability_id is null THEN da.description ELSE a.description END as description,
                    da.type, 
                    da.parameter
                FROM Datasheets_abilities da
                LEFT JOIN Abilities a ON da.ability_id = a.id AND (a.faction_id = @factionId or a.faction_id = '')
                WHERE da.datasheet_id = @id
                ORDER BY da.line";

            datasheet.Abilities = await QueryListAsync(sql, r => new DatasheetAbility
            {
                Line = I(r, "line") ?? 0,
                AbilityId = I(r, "ability_id"),
                Name = S(r, "name"),
                Description = S(r, "description"),
                Type = S(r, "type"),
                Parameter = S(r, "parameter")
            }, ("@id", datasheet.Id), ("@factionId", datasheet.FactionId));
        }

        private async Task LoadOptionsAsync(DatasheetDetail datasheet)
        {
            const string sql = @"
                SELECT line, button, description
                FROM Datasheets_options
                WHERE datasheet_id = @id
                ORDER BY line";

            datasheet.Options = await QueryListAsync(sql, r => new DatasheetOption
            {
                Line = I(r, "line") ?? 0,
                Button = S(r, "button"),
                Description = S(r, "description")
            }, ("@id", datasheet.Id));
        }

        private async Task LoadCostsAsync(DatasheetDetail datasheet)
        {
            const string sql = @"
                SELECT line, description, cost
                FROM Datasheets_models_cost
                WHERE datasheet_id = @id
                ORDER BY line";

            datasheet.Costs = await QueryListAsync(sql, r => new DatasheetCost
            {
                Line = I(r, "line") ?? 0,
                Description = S(r, "description"),
                Cost = I(r, "cost") ?? 0
            }, ("@id", datasheet.Id));
        }

        private async Task LoadKeywordsAsync(DatasheetDetail datasheet)
        {
            const string sql = @"
                SELECT keyword, is_faction_keyword
                FROM Datasheets_keywords
                WHERE datasheet_id = @id";

            var keywords = await QueryListAsync(sql, r => new
            {
                Keyword = S(r, "keyword"),
                IsFactionKeyword = B(r, "is_faction_keyword")
            }, ("@id", datasheet.Id));

            datasheet.Keywords = keywords.Where(k => !k.IsFactionKeyword).Select(k => k.Keyword).ToList();
            datasheet.FactionKeywords = keywords.Where(k => k.IsFactionKeyword).Select(k => k.Keyword).ToList();
        }

        private async Task LoadLeaderRelationshipsAsync(DatasheetDetail datasheet)
        {
            // Units this one can lead
            const string canLeadSql = @"
                SELECT d.id, d.name
                FROM Datasheets_leader dl
                INNER JOIN Datasheets d ON d.id = dl.attached_id
                WHERE dl.leader_id = @id
                ORDER BY d.name";

            datasheet.CanLead = await QueryListAsync(canLeadSql, r => new DatasheetLeader
            {
                DatasheetId = I(r, "id") ?? 0,
                Name = S(r, "name")
            }, ("@id", datasheet.Id));

            // Units that can lead this one
            const string leadBySql = @"
                SELECT d.id, d.name
                FROM Datasheets_leader dl
                INNER JOIN Datasheets d ON d.id = dl.leader_id
                WHERE dl.attached_id = @id
                ORDER BY d.name";

            datasheet.LeadBy = await QueryListAsync(leadBySql, r => new DatasheetLeader
            {
                DatasheetId = I(r, "id") ?? 0,
                Name = S(r, "name")
            }, ("@id", datasheet.Id));
        }

        private async Task LoadDetachmentAbilitiesAsync(DatasheetDetail datasheet, int? detachmentId)
        {
            var sql = @"
                SELECT DISTINCT
                    da.id, da.name, da.legend, da.description, da.detachment_id
                FROM Datasheets_detachment_abilities dda
                INNER JOIN Detachment_abilities da ON da.id = dda.detachment_ability_id
                WHERE dda.datasheet_id = @id
                AND da.faction_id = @factionId";

            var parameters = new List<(string, object)>
            {
                ("@id", datasheet.Id),
                ("@factionId", datasheet.FactionId)
            };

            if (detachmentId.HasValue)
            {
                sql += " AND da.detachment_id = @detachmentId";
                parameters.Add(("@detachmentId", detachmentId.Value));
            }

            sql += " ORDER BY da.name";

            datasheet.DetachmentAbilities = await QueryListAsync(sql, r => new DetachmentAbility
            {
                Id = I(r, "id") ?? 0,
                Name = S(r, "name"),
                Legend = S(r, "legend"),
                Description = S(r, "description"),
                DetachmentId = I(r, "detachment_id") ?? 0
            }, parameters.ToArray());
        }

        private async Task LoadEnhancementsAsync(DatasheetDetail datasheet, int? detachmentId)
        {
            var sql = @"
                SELECT DISTINCT
                    e.id, e.name, e.cost, e.legend, e.description, e.detachment_id
                FROM Datasheets_enhancements de
                INNER JOIN Enhancements e ON e.id = de.enhancement_id
                WHERE de.datasheet_id = @id
                AND e.faction_id = @factionId";

            var parameters = new List<(string, object)>
            {
                ("@id", datasheet.Id),
                ("@factionId", datasheet.FactionId)
            };

            if (detachmentId.HasValue)
            {
                sql += " AND e.detachment_id = @detachmentId";
                parameters.Add(("@detachmentId", detachmentId.Value));
            }

            sql += " ORDER BY e.name";

            datasheet.Enhancements = await QueryListAsync(sql, r => new Enhancement
            {
                Id = I(r, "id") ?? 0,
                Name = S(r, "name"),
                Cost = I(r, "cost") ?? 0,
                Legend = S(r, "legend"),
                Description = S(r, "description"),
                DetachmentId = I(r, "detachment_id") ?? 0
            }, parameters.ToArray());
        }

        private async Task LoadStratagemsAsync(DatasheetDetail datasheet, int? detachmentId)
        {
            // First, load faction-specific stratagems
            var sql = @"
                SELECT DISTINCT
                    s.id, s.faction_id, s.name, s.type, s.cp_cost, s.legend, s.turn, s.phase, s.description, s.detachment, s.detachment_id
                FROM Datasheets_stratagems ds
                INNER JOIN Stratagems s ON s.id = ds.stratagem_id
                WHERE ds.datasheet_id = @id
                AND s.faction_id = @factionId";

            var parameters = new List<(string, object)>
            {
                ("@id", datasheet.Id),
                ("@factionId", datasheet.FactionId)
            };

            if (detachmentId.HasValue)
            {
                sql += " AND s.detachment_id = @detachmentId";
                parameters.Add(("@detachmentId", detachmentId.Value));
            }

            sql += " ORDER BY s.name";

            var factionStratagems = await QueryListAsync(sql, r => new Stratagem
            {
                Id = I(r, "id") ?? 0,
                FactionId = S(r, "faction_id"),
                Name = S(r, "name"),
                Type = S(r, "type"),
                CpCost = S(r, "cp_cost"),
                Legend = S(r, "legend"),
                Turn = S(r, "turn"),
                Phase = S(r, "phase"),
                Description = S(r, "description"),
                Detachment = S(r, "detachment"),
                DetachmentId = I(r, "detachment_id") ?? 0
            }, parameters.ToArray());

            // Second, load Core stratagems (no faction_id or detachment_id)
            // Note: Core stratagems don't have datasheet associations, so we query directly from Stratagems table
            var coreSql = @"
                SELECT DISTINCT
                    s.id, s.faction_id, s.name, s.type, s.cp_cost, s.legend, s.turn, s.phase, s.description, s.detachment, s.detachment_id
                FROM Stratagems s
                WHERE (s.faction_id IS NULL OR s.faction_id = '')
                AND s.type LIKE 'Core%'
                ORDER BY s.name";

            var coreStratagems = await QueryListAsync(coreSql, r => new Stratagem
            {
                Id = I(r, "id") ?? 0,
                FactionId = S(r, "faction_id"),
                Name = S(r, "name"),
                Type = S(r, "type"),
                CpCost = S(r, "cp_cost"),
                Legend = S(r, "legend"),
                Turn = S(r, "turn"),
                Phase = S(r, "phase"),
                Description = S(r, "description"),
                Detachment = S(r, "detachment"),
                DetachmentId = I(r, "detachment_id") ?? 0
            });

            // Filter Core Wargear Stratagems by unit keywords
            var allKeywords = datasheet.Keywords.Concat(datasheet.FactionKeywords).ToList();
            var filteredCoreStratagems = coreStratagems.Where(strat =>
            {
                // If it's a Core Wargear Stratagem, check if the unit has the matching keyword
                if (StratagemHelper.IsCoreWargearStratagem(strat))
                {
                    return StratagemHelper.CanUnitUseCoreWargearStratagem(strat, allKeywords);
                }
                // All other Core stratagems are always available
                return true;
            }).ToList();

            // Combine faction-specific and filtered Core stratagems
            datasheet.Stratagems = factionStratagems.Concat(filteredCoreStratagems).ToList();
        }

        private async Task<List<string>> GetKeywordsForDatasheetAsync(int datasheetId)
        {
            const string sql = @"
                SELECT keyword
                FROM Datasheets_keywords
                WHERE datasheet_id = @id AND is_faction_keyword != 'true'";

            return await QueryListAsync(sql, r => S(r, "keyword"), ("@id", datasheetId));
        }

        private async Task<int?> GetBaseCostForDatasheetAsync(int datasheetId)
        {
            const string sql = @"
                SELECT cost
                FROM Datasheets_models_cost
                WHERE datasheet_id = @id
                ORDER BY line
                LIMIT 1";

            return await QuerySingleAsync(sql, r => I(r, "cost"), ("@id", datasheetId));
        }
    }
}

using OmniTactica.AppCode.Models.Core;
using OmniTactica.AppCode.Repositories;
using OmniTactica.AppCode.Utilities;
using System.Text.RegularExpressions;

namespace OmniTactica.AppCode.Services
{
    /// <summary>
    /// Service for managing versus combat state with comprehensive unit and modifier management.
    /// </summary>
    public class VersusService
    {
        private readonly DatasheetRepository _datasheetRepo;
        private VersusContext _context = new();

        public event Action? OnContextChanged;

        public VersusService(DatasheetRepository datasheetRepo)
        {
            _datasheetRepo = datasheetRepo;
        }

        public VersusContext GetContext() => _context;

        public void SetContext(VersusContext context)
        {
            _context = context;
            OnContextChanged?.Invoke();
        }

        // ===== Unit Management =====

        /// <summary>
        /// Adds an attacking unit by loading its datasheet and populating models/weapons from defaults.
        /// </summary>
        public async Task<CombatUnit> AddAttackingUnitAsync(int datasheetId)
        {
            var datasheet = await _datasheetRepo.GetDatasheetDetailAsync(datasheetId);
            if (datasheet == null)
                throw new InvalidOperationException($"Datasheet {datasheetId} not found");

            var unit = await CreateCombatUnitFromDatasheet(datasheet);
            _context.AttackingUnits.Add(unit);
            OnContextChanged?.Invoke();
            return unit;
        }

        /// <summary>
        /// Adds a defending unit by loading its datasheet and populating models/weapons from defaults.
        /// </summary>
        public async Task<CombatUnit> AddDefendingUnitAsync(int datasheetId)
        {
            var datasheet = await _datasheetRepo.GetDatasheetDetailAsync(datasheetId);
            if (datasheet == null)
                throw new InvalidOperationException($"Datasheet {datasheetId} not found");

            var unit = await CreateCombatUnitFromDatasheet(datasheet);
            _context.DefendingUnits.Add(unit);
            OnContextChanged?.Invoke();
            return unit;
        }

        /// <summary>
        /// Creates a CombatUnit from a DatasheetDetail by parsing unit composition and loadout.
        /// </summary>
        private async Task<CombatUnit> CreateCombatUnitFromDatasheet(DatasheetDetail datasheet)
        {
            var unit = new CombatUnit
            {
                DatasheetId = datasheet.Id,
                DatasheetName = datasheet.Name,
                FactionId = datasheet.FactionId,
                DatasheetDetail = datasheet
            };

            // Parse unit composition to determine model types and quantities
            var modelComposition = ParseUnitComposition(datasheet.UnitComposition);

            // Create combat models based on composition
            foreach (var (modelName, minQuantity, maxQuantity) in modelComposition)
            {
                // Find matching model profile - try exact match first, then partial match, then use datasheet name or first profile
                var modelProfile = datasheet.Models.FirstOrDefault(m =>
                    m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase));

                if (modelProfile == null)
                {
                    // Try partial match (e.g., "Kill Team Veterans" matches "KILL TEAM VETERANS")
                    modelProfile = datasheet.Models.FirstOrDefault(m =>
                        m.Name.Replace(" ", "").Contains(modelName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) ||
                        modelName.Replace(" ", "").Contains(m.Name.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
                }

                if (modelProfile == null)
                {
                    // Try matching with datasheet name (many units use datasheet name as the model profile)
                    modelProfile = datasheet.Models.FirstOrDefault(m =>
                        m.Name.Equals(datasheet.Name, StringComparison.OrdinalIgnoreCase));
                }

                if (modelProfile == null)
                {
                    // Use first model profile as fallback
                    modelProfile = datasheet.Models.FirstOrDefault();
                }

                if (modelProfile != null)
                {
                    // Create combat model using the profile stats but keeping the composition name
                    var combatModel = CreateCombatModelFromProfile(modelProfile, minQuantity, modelName);

                    // Parse loadout to assign weapons specific to this model type
                    var weaponsForModel = ParseLoadoutForModel(datasheet.Loadout, modelName, datasheet.Wargear);
                    foreach (var weapon in weaponsForModel)
                    {
                        // Scale weapon quantity by model count (e.g., 4 banshees each with 1 blade = quantity 4)
                        weapon.Quantity *= minQuantity;
                        combatModel.Weapons.Add(weapon);
                    }

                    unit.Models.Add(combatModel);
                }
            }

            return unit;
        }

        /// <summary>
        /// Creates a CombatModel from a DatasheetModel profile.
        /// </summary>
        private CombatModel CreateCombatModelFromProfile(DatasheetModel profile, int quantity, string? compositionName = null)
        {
            var model = new CombatModel
            {
                // Use composition name if provided (e.g., "Knight Master"), otherwise use profile name
                Name = compositionName ?? profile.Name,
                Quantity = quantity,
                M = ParseStatValue(profile.M),
                T = ParseStatValue(profile.T),
                Sv = ParseStatValue(profile.Sv),
                InvSv = ParseStatValue(profile.InvSv),
                W = ParseStatValue(profile.W),
                Ld = ParseStatValue(profile.Ld),
                OC = ParseStatValue(profile.OC),
                DatasheetModel = profile
            };

            model.MaxWounds = model.W;
            model.CurrentWounds = model.W;

            return model;
        }

        /// <summary>
        /// Creates a CombatWeapon from a DatasheetWargear profile.
        /// </summary>
        private CombatWeapon CreateCombatWeaponFromWargear(DatasheetWargear wargear)
        {
            var abilities = ParseWeaponAbilities(wargear.Description);

            var weapon = new CombatWeapon
            {
                Name = wargear.Name,
                Range = wargear.Range,
                Type = wargear.Type,
                A = wargear.A,
                BsWs = ParseStatValue(wargear.BsWs),
                S = ParseStatValue(wargear.S),
                AP = ParseStatValue(wargear.AP),
                D = wargear.D,
                DatasheetWargear = wargear,
                Abilities = abilities
            };

            // Convert weapon abilities to modifiers for the combat system
            CreateModifiersFromAbilities(weapon, abilities);

            return weapon;
        }

        /// <summary>
        /// Creates ConditionalModifier objects from weapon abilities.
        /// </summary>
        private void CreateModifiersFromAbilities(CombatWeapon weapon, WeaponAbilities abilities)
        {
            // Anti-X Y+ (e.g., Anti-Infantry 4+, Anti-Vehicle 4+) - Handle multiple Anti keywords
            foreach (var antiAbility in abilities.GetAntiAbilities())
            {
                // Extract keyword from the ability ID (e.g., "anti_vehicle" -> "vehicle")
                var keyword = antiAbility.Id.Replace("anti_", "").Replace("_", " ");
                var threshold = antiAbility.Value ?? 4; // Default to 4+ if not specified

                weapon.Modifiers.Add(new ConditionalModifier
                {
                    Name = antiAbility.DisplayName,
                    Condition = new ModifierCondition
                    {
                        Type = MapAntiKeywordToCondition(keyword),
                        Value = keyword
                    },
                    Effect = new ModifierEffect
                    {
                        Type = EffectType.CriticalWoundOn,
                        IntValue = threshold
                    },
                    IsActive = true
                });
            }

            // Devastating Wounds is handled directly in VersusCalculator via Abilities property
            // Sustained Hits is handled directly in VersusCalculator via Abilities property
            // Lethal Hits is handled directly in VersusCalculator via Abilities property
            // But we can still add them as display modifiers

            if (abilities.SustainedHits.HasValue && abilities.SustainedHits.Value > 0)
            {
                weapon.Modifiers.Add(new ConditionalModifier
                {
                    Name = $"Sustained Hits {abilities.SustainedHits.Value}",
                    Condition = new ModifierCondition { Type = ConditionType.Always },
                    Effect = new ModifierEffect
                    {
                        Type = EffectType.AddExtraHitsOnCrit,
                        IntValue = abilities.SustainedHits.Value
                    },
                    IsActive = true
                });
            }

            if (abilities.DevastatingWounds)
            {
                weapon.Modifiers.Add(new ConditionalModifier
                {
                    Name = "Devastating Wounds",
                    Condition = new ModifierCondition { Type = ConditionType.Always },
                    Effect = new ModifierEffect { Type = EffectType.ConvertToMortalWounds },
                    IsActive = true
                });
            }

            if (abilities.LethalHits)
            {
                weapon.Modifiers.Add(new ConditionalModifier
                {
                    Name = "Lethal Hits",
                    Condition = new ModifierCondition { Type = ConditionType.Always },
                    Effect = new ModifierEffect { Type = EffectType.AutoWoundOnCrit },
                    IsActive = true
                });
            }

            if (abilities.TwinLinked)
            {
                weapon.Modifiers.Add(new ConditionalModifier
                {
                    Name = "Twin-Linked",
                    Condition = new ModifierCondition { Type = ConditionType.Always },
                    Effect = new ModifierEffect { Type = EffectType.RerollWounds },
                    IsActive = true
                });
            }
        }

        /// <summary>
        /// Maps anti-keyword string to appropriate condition type.
        /// </summary>
        private ConditionType MapAntiKeywordToCondition(string keyword)
        {
            return keyword.ToLower() switch
            {
                "infantry" => ConditionType.TargetIsInfantry,
                "vehicle" => ConditionType.TargetIsVehicle,
                "monster" => ConditionType.TargetIsMonster,
                "character" => ConditionType.TargetIsCharacter,
                _ => ConditionType.TargetHasKeyword
            };
        }

        /// <summary>
        /// Parses unit composition text to extract model types and quantities.
        /// Examples: "1 Warboss", "4-9 Intercessors", "5-10 Assault Intercessors"
        /// </summary>
        private List<(string ModelName, int MinQuantity, int MaxQuantity)> ParseUnitComposition(List<DatasheetUnitComposition> composition)
        {
            var result = new List<(string, int, int)>();

            foreach (var line in composition)
            {
                var description = line.Description.Trim();

                // Match patterns like "1 Model", "5-10 Models", "1-3 Models"
                var match = Regex.Match(description, @"^(\d+)(?:-(\d+))?\s+(.+)$");

                if (match.Success)
                {
                    var minQty = int.Parse(match.Groups[1].Value);
                    var maxQty = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : minQty;
                    var modelName = match.Groups[3].Value.Trim();

                    result.Add((modelName, minQty, maxQty));
                }
            }

            return result;
        }

        /// <summary>
        /// Parses loadout text to assign weapons to models.
        /// Example: "The Knight Master is equipped with: great weapon of the Unforgiven. Every Deathwing Knight is equipped with: mace of absolution."
        /// </summary>
        private List<CombatWeapon> ParseLoadoutForModel(string loadoutText, string modelName, List<DatasheetWargear> allWargear)
        {
            var weapons = new List<CombatWeapon>();

            if (string.IsNullOrEmpty(loadoutText))
                return weapons;

            // Remove HTML tags
            loadoutText = HtmlUtility.StripHtml(loadoutText);

            // Split by sentence-like patterns (looking for "X is equipped with:" patterns)
            // Examples:
            // "The Knight Master is equipped with: weapon1; weapon2."
            // "Every Deathwing Knight is equipped with: weapon3."
            // "Every model is equipped with: weapon4."

            var loadoutLines = loadoutText.Split(new[] { '.', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            foreach (var line in loadoutLines)
            {
                // Check if this line applies to our model
                // Patterns to match:
                // "The [ModelName] is equipped with:"
                // "Every [ModelName] is equipped with:"
                // "This model is equipped with:" (applies to all)
                // "Every model is equipped with:" (applies to all)

                bool appliesToThisModel = false;
                string weaponsPart = "";

                // Check for "The [ModelName] is equipped with:"
                var theModelMatch = Regex.Match(line, @"^The (.+?) is equipped with:(.+)$", RegexOptions.IgnoreCase);
                if (theModelMatch.Success)
                {
                    var targetModel = theModelMatch.Groups[1].Value.Trim();
                    weaponsPart = theModelMatch.Groups[2].Value.Trim();
                    appliesToThisModel = targetModel.Equals(modelName, StringComparison.OrdinalIgnoreCase) ||
                                        modelName.Contains(targetModel, StringComparison.OrdinalIgnoreCase);
                }

                // Check for "Every [ModelName] is equipped with:"
                var everyModelMatch = Regex.Match(line, @"^Every (.+?) is equipped with:(.+)$", RegexOptions.IgnoreCase);
                if (everyModelMatch.Success)
                {
                    var targetModel = everyModelMatch.Groups[1].Value.Trim();
                    weaponsPart = everyModelMatch.Groups[2].Value.Trim();
                    // Match both singular and plural forms
                    // "Every Deathwing Knight" should match "Deathwing Knights" from composition
                    appliesToThisModel = 
                        targetModel.Equals(modelName, StringComparison.OrdinalIgnoreCase) ||
                        targetModel.TrimEnd('s').Equals(modelName.TrimEnd('s'), StringComparison.OrdinalIgnoreCase) ||
                        modelName.Contains(targetModel, StringComparison.OrdinalIgnoreCase) ||
                        targetModel.Contains(modelName, StringComparison.OrdinalIgnoreCase);
                }

                // Check for "This model is equipped with:" or "Every model is equipped with:"
                var genericModelMatch = Regex.Match(line, @"^(?:This model|Every model) is equipped with:(.+)$", RegexOptions.IgnoreCase);
                if (genericModelMatch.Success)
                {
                    weaponsPart = genericModelMatch.Groups[1].Value.Trim();
                    appliesToThisModel = true; // Applies to all models
                }

                if (appliesToThisModel && !string.IsNullOrEmpty(weaponsPart))
                {
                    // Parse weapons from this line
                    var weaponNames = weaponsPart.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();

                    foreach (var weaponName in weaponNames)
                    {
                        // Parse optional quantity prefix (e.g., "2 godhammer lascannons")
                        int quantity = 1;
                        string cleanedName = weaponName;
                        var qtyMatch = Regex.Match(weaponName, @"^(\d+)\s+(.+)$");
                        if (qtyMatch.Success)
                        {
                            quantity = int.Parse(qtyMatch.Groups[1].Value);
                            cleanedName = qtyMatch.Groups[2].Value.Trim();
                        }

                        // Try to match with the name as-is, then without trailing 's' for plurals
                        var wargear = allWargear.FirstOrDefault(w =>
                            w.Name.Equals(cleanedName, StringComparison.OrdinalIgnoreCase) ||
                            cleanedName.Contains(w.Name, StringComparison.OrdinalIgnoreCase));

                        if (wargear == null && cleanedName.EndsWith('s'))
                        {
                            var singular = cleanedName[..^1];
                            wargear = allWargear.FirstOrDefault(w =>
                                w.Name.Equals(singular, StringComparison.OrdinalIgnoreCase) ||
                                singular.Contains(w.Name, StringComparison.OrdinalIgnoreCase));
                        }

                        if (wargear != null)
                        {
                            var weapon = CreateCombatWeaponFromWargear(wargear);
                            weapon.Quantity = quantity;
                            weapons.Add(weapon);
                        }
                    }
                }
            }

            return weapons;
        }

        /// <summary>
        /// Parses weapon abilities from description text.
        /// </summary>
        private WeaponAbilities ParseWeaponAbilities(string description)
        {
            return WeaponAbilityParser.Parse(description);
        }

        /// <summary>
        /// Parses a stat value string to an integer (handles formats like "3+", "6\"", etc.)
        /// </summary>
        private int ParseStatValue(string value)
        {
            if (string.IsNullOrEmpty(value) || value == "-" || value == "N/A")
                return 0;

            // Remove common suffixes
            value = value.Replace("+", "").Replace("\"", "").Replace("'", "").Trim();

            // Handle quotes
            value = value.Replace("'", "").Replace("'", "");

            if (int.TryParse(value, out var result))
                return result;

            return 0;
        }

        public void RemoveAttackingUnit(string unitId)
        {
            _context.AttackingUnits.RemoveAll(u => u.Id == unitId);
            OnContextChanged?.Invoke();
        }

        public void RemoveDefendingUnit(string unitId)
        {
            _context.DefendingUnits.RemoveAll(u => u.Id == unitId);
            OnContextChanged?.Invoke();
        }

        public CombatUnit? GetUnit(string unitId)
        {
            return _context.AttackingUnits.FirstOrDefault(u => u.Id == unitId) ??
                   _context.DefendingUnits.FirstOrDefault(u => u.Id == unitId);
        }

        // ===== Model Management =====

        public void AddModelToUnit(string unitId, CombatModel model)
        {
            var unit = GetUnit(unitId);
            if (unit != null)
            {
                unit.Models.Add(model);
                OnContextChanged?.Invoke();
            }
        }

        public void RemoveModelFromUnit(string unitId, string modelId)
        {
            var unit = GetUnit(unitId);
            if (unit != null)
            {
                unit.Models.RemoveAll(m => m.Id == modelId);
                OnContextChanged?.Invoke();
            }
        }

        public void UpdateModel(string unitId, string modelId, CombatModel updatedModel)
        {
            var unit = GetUnit(unitId);
            if (unit != null)
            {
                var index = unit.Models.FindIndex(m => m.Id == modelId);
                if (index >= 0)
                {
                    updatedModel.Id = modelId;
                    unit.Models[index] = updatedModel;
                    OnContextChanged?.Invoke();
                }
            }
        }

        // ===== Weapon Management =====

        public void AddWeaponToModel(string unitId, string modelId, CombatWeapon weapon)
        {
            var unit = GetUnit(unitId);
            var model = unit?.Models.FirstOrDefault(m => m.Id == modelId);
            if (model != null)
            {
                model.Weapons.Add(weapon);
                OnContextChanged?.Invoke();
            }
        }

        public void RemoveWeaponFromModel(string unitId, string modelId, string weaponId)
        {
            var unit = GetUnit(unitId);
            var model = unit?.Models.FirstOrDefault(m => m.Id == modelId);
            if (model != null)
            {
                model.Weapons.RemoveAll(w => w.Id == weaponId);
                OnContextChanged?.Invoke();
            }
        }

        public void ToggleWeaponSelection(string unitId, string modelId, string weaponId)
        {
            var unit = GetUnit(unitId);
            var model = unit?.Models.FirstOrDefault(m => m.Id == modelId);
            var weapon = model?.Weapons.FirstOrDefault(w => w.Id == weaponId);
            if (weapon != null)
            {
                weapon.IsSelected = !weapon.IsSelected;
                OnContextChanged?.Invoke();
            }
        }

        public void UpdateWeapon(string unitId, string modelId, string weaponId, CombatWeapon updatedWeapon)
        {
            var unit = GetUnit(unitId);
            var model = unit?.Models.FirstOrDefault(m => m.Id == modelId);
            if (model != null)
            {
                var index = model.Weapons.FindIndex(w => w.Id == weaponId);
                if (index >= 0)
                {
                    updatedWeapon.Id = weaponId;
                    model.Weapons[index] = updatedWeapon;
                    OnContextChanged?.Invoke();
                }
            }
        }

        // ===== Modifier Management =====

        public void AddUnitModifier(string unitId, ConditionalModifier modifier)
        {
            var unit = GetUnit(unitId);
            if (unit != null)
            {
                unit.Modifiers.Add(modifier);
                OnContextChanged?.Invoke();
            }
        }

        public void RemoveUnitModifier(string unitId, string modifierId)
        {
            var unit = GetUnit(unitId);
            if (unit != null)
            {
                unit.Modifiers.RemoveAll(m => m.Id == modifierId);
                OnContextChanged?.Invoke();
            }
        }

        public void AddModelModifier(string unitId, string modelId, ConditionalModifier modifier)
        {
            var unit = GetUnit(unitId);
            var model = unit?.Models.FirstOrDefault(m => m.Id == modelId);
            if (model != null)
            {
                model.Modifiers.Add(modifier);
                OnContextChanged?.Invoke();
            }
        }

        public void RemoveModelModifier(string unitId, string modelId, string modifierId)
        {
            var unit = GetUnit(unitId);
            var model = unit?.Models.FirstOrDefault(m => m.Id == modelId);
            if (model != null)
            {
                model.Modifiers.RemoveAll(m => m.Id == modifierId);
                OnContextChanged?.Invoke();
            }
        }

        public void AddWeaponModifier(string unitId, string modelId, string weaponId, ConditionalModifier modifier)
        {
            var unit = GetUnit(unitId);
            var model = unit?.Models.FirstOrDefault(m => m.Id == modelId);
            var weapon = model?.Weapons.FirstOrDefault(w => w.Id == weaponId);
            if (weapon != null)
            {
                weapon.Modifiers.Add(modifier);
                OnContextChanged?.Invoke();
            }
        }

        public void RemoveWeaponModifier(string unitId, string modelId, string weaponId, string modifierId)
        {
            var unit = GetUnit(unitId);
            var model = unit?.Models.FirstOrDefault(m => m.Id == modelId);
            var weapon = model?.Weapons.FirstOrDefault(w => w.Id == weaponId);
            if (weapon != null)
            {
                weapon.Modifiers.RemoveAll(m => m.Id == modifierId);
                OnContextChanged?.Invoke();
            }
        }

        // ===== Global Settings =====

        public void UpdateGlobalAttackerModifiers(GlobalModifiers modifiers)
        {
            _context.AttackerGlobalModifiers = modifiers;
            OnContextChanged?.Invoke();
        }

        public void UpdateGlobalDefenderModifiers(GlobalModifiers modifiers)
        {
            _context.DefenderGlobalModifiers = modifiers;
            OnContextChanged?.Invoke();
        }

        public void UpdateSimulationSettings(SimulationSettings settings)
        {
            _context.SimulationSettings = settings;
            OnContextChanged?.Invoke();
        }

        public void SwapAttackerDefender()
        {
            (_context.AttackingUnits, _context.DefendingUnits) = (_context.DefendingUnits, _context.AttackingUnits);
            (_context.AttackerGlobalModifiers, _context.DefenderGlobalModifiers) = (_context.DefenderGlobalModifiers, _context.AttackerGlobalModifiers);
            OnContextChanged?.Invoke();
        }

        public void ClearAll()
        {
            _context = new VersusContext();
            OnContextChanged?.Invoke();
        }
    }
}

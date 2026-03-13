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
                // Find matching model profile
                var modelProfile = datasheet.Models.FirstOrDefault(m =>
                    m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase) ||
                    m.Name.Equals(datasheet.Name, StringComparison.OrdinalIgnoreCase));

                if (modelProfile == null)
                {
                    // Use first model profile as fallback
                    modelProfile = datasheet.Models.FirstOrDefault();
                }

                if (modelProfile != null)
                {
                    var combatModel = CreateCombatModelFromProfile(modelProfile, minQuantity);

                    // Parse loadout to assign weapons
                    var weaponsForModel = ParseLoadoutForModel(datasheet.Loadout, modelName, datasheet.Wargear);
                    foreach (var weapon in weaponsForModel)
                    {
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
        private CombatModel CreateCombatModelFromProfile(DatasheetModel profile, int quantity)
        {
            var model = new CombatModel
            {
                Name = profile.Name,
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
                Abilities = ParseWeaponAbilities(wargear.Description)
            };

            return weapon;
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
        /// Example: "Every model is equipped with: bolt pistol; bolt rifle; close combat weapon."
        /// </summary>
        private List<CombatWeapon> ParseLoadoutForModel(string loadoutText, string modelName, List<DatasheetWargear> allWargear)
        {
            var weapons = new List<CombatWeapon>();

            if (string.IsNullOrEmpty(loadoutText))
                return weapons;

            // Remove HTML tags
            loadoutText = HtmlUtility.StripHtml(loadoutText);

            // Split by common delimiters
            var weaponNames = loadoutText.Split(new[] { ';', ',', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            foreach (var weaponName in weaponNames)
            {
                // Clean up weapon name
                var cleanName = weaponName
                    .Replace("Every model is equipped with:", "")
                    .Replace("This model is equipped with:", "")
                    .Replace("The ", "")
                    .Replace(" is equipped with:", "")
                    .Trim();

                if (string.IsNullOrEmpty(cleanName))
                    continue;

                // Find matching wargear
                var wargear = allWargear.FirstOrDefault(w =>
                    w.Name.Equals(cleanName, StringComparison.OrdinalIgnoreCase) ||
                    cleanName.Contains(w.Name, StringComparison.OrdinalIgnoreCase));

                if (wargear != null)
                {
                    weapons.Add(CreateCombatWeaponFromWargear(wargear));
                }
            }

            return weapons;
        }

        /// <summary>
        /// Parses weapon abilities from description text.
        /// </summary>
        private WeaponAbilities ParseWeaponAbilities(string description)
        {
            if (string.IsNullOrEmpty(description))
                return new WeaponAbilities();

            var abilities = new WeaponAbilities();
            var lower = description.ToLower();

            // Parse abilities
            abilities.Assault = lower.Contains("assault");
            abilities.Blast = lower.Contains("blast");
            abilities.DevastatingWounds = lower.Contains("devastating wounds");
            abilities.Hazardous = lower.Contains("hazardous");
            abilities.Heavy = lower.Contains("heavy");
            abilities.IgnoresCover = lower.Contains("ignores cover");
            abilities.IndirectFire = lower.Contains("indirect fire");
            abilities.LethalHits = lower.Contains("lethal hits");
            abilities.Pistol = lower.Contains("pistol");
            abilities.Precision = lower.Contains("precision");
            abilities.Torrent = lower.Contains("torrent");
            abilities.TwinLinked = lower.Contains("twin-linked");

            // Parse with values
            var rapidFireMatch = Regex.Match(description, @"rapid fire (\d+)", RegexOptions.IgnoreCase);
            if (rapidFireMatch.Success)
                abilities.RapidFire = int.Parse(rapidFireMatch.Groups[1].Value);

            var sustainedHitsMatch = Regex.Match(description, @"sustained hits (\d+)", RegexOptions.IgnoreCase);
            if (sustainedHitsMatch.Success)
                abilities.SustainedHits = int.Parse(sustainedHitsMatch.Groups[1].Value);

            var meltaMatch = Regex.Match(description, @"melta (\d+)", RegexOptions.IgnoreCase);
            if (meltaMatch.Success)
                abilities.Melta = int.Parse(meltaMatch.Groups[1].Value);

            // Parse Anti- keywords
            var antiInfantryMatch = Regex.Match(description, @"anti-infantry (\d+)\+", RegexOptions.IgnoreCase);
            if (antiInfantryMatch.Success)
            {
                abilities.AntiInfantry = true;
                abilities.AntiInfantryValue = int.Parse(antiInfantryMatch.Groups[1].Value);
            }

            var antiVehicleMatch = Regex.Match(description, @"anti-vehicle (\d+)\+", RegexOptions.IgnoreCase);
            if (antiVehicleMatch.Success)
            {
                abilities.AntiVehicle = true;
                abilities.AntiVehicleValue = int.Parse(antiVehicleMatch.Groups[1].Value);
            }

            var antiMonsterMatch = Regex.Match(description, @"anti-monster (\d+)\+", RegexOptions.IgnoreCase);
            if (antiMonsterMatch.Success)
            {
                abilities.AntiMonster = true;
                abilities.AntiMonsterValue = int.Parse(antiMonsterMatch.Groups[1].Value);
            }

            return abilities;
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

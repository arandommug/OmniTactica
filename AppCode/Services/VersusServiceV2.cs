using OmniTactica.AppCode.Models.Core;
using OmniTactica.AppCode.Utilities;

namespace OmniTactica.AppCode.Services
{
    /// <summary>
    /// Service for managing enhanced versus combat state with multiple units.
    /// </summary>
    public class VersusServiceV2
    {
        private VersusContextV2 _context = new();

        public event Action? OnContextChanged;

        public VersusContextV2 GetContext() => _context;

        // ===== Unit Management =====

        public CombatUnit AddAttackingUnit(int datasheetId, string datasheetName, DatasheetDetail? datasheet = null)
        {
            var unit = new CombatUnit
            {
                DatasheetId = datasheetId,
                DatasheetName = datasheetName,
                Datasheet = datasheet,
                FiringPriority = _context.AttackingUnits.Count
            };

            _context.AttackingUnits.Add(unit);
            OnContextChanged?.Invoke();
            return unit;
        }

        public CombatUnit AddDefendingUnit(int datasheetId, string datasheetName, DatasheetDetail? datasheet = null)
        {
            var unit = new CombatUnit
            {
                DatasheetId = datasheetId,
                DatasheetName = datasheetName,
                Datasheet = datasheet
            };

            _context.DefendingUnits.Add(unit);
            OnContextChanged?.Invoke();
            return unit;
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

        public CombatModel AddModelToUnit(string unitId, DatasheetModel modelProfile, int count = 1)
        {
            var unit = GetUnit(unitId);
            if (unit == null) throw new InvalidOperationException("Unit not found");

            CombatModel? firstModel = null;

            for (int i = 0; i < count; i++)
            {
                var wounds = ParseValue(modelProfile.W);
                var model = new CombatModel
                {
                    ModelProfile = modelProfile,
                    CurrentWounds = wounds,
                    MaxWounds = wounds
                };

                unit.Models.Add(model);
                firstModel ??= model;
            }

            OnContextChanged?.Invoke();
            return firstModel!;
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

        // ===== Weapon Management =====

        public CombatWeapon AddWeaponToModel(string unitId, string modelId, DatasheetWargear weaponProfile)
        {
            var unit = GetUnit(unitId);
            var model = unit?.Models.FirstOrDefault(m => m.Id == modelId);
            if (model == null) throw new InvalidOperationException("Model not found");

            var weapon = new CombatWeapon
            {
                WeaponProfile = weaponProfile,
                IsSelected = true,
                ParsedAbilities = WeaponAbilityParser.Parse(weaponProfile.Description),
                FiringPriority = model.Weapons.Count
            };

            // Apply automatic modifiers from abilities
            ApplyWeaponAbilityModifiers(weapon);

            model.Weapons.Add(weapon);
            OnContextChanged?.Invoke();
            return weapon;
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

        // ===== Modifier Management =====

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

        public void UpdateUnitModifiers(string unitId, UnitModifiers modifiers)
        {
            var unit = GetUnit(unitId);
            if (unit != null)
            {
                unit.UnitModifiers = modifiers;
                OnContextChanged?.Invoke();
            }
        }

        public void UpdateModelModifiers(string unitId, string modelId, ModelModifiers modifiers)
        {
            var unit = GetUnit(unitId);
            var model = unit?.Models.FirstOrDefault(m => m.Id == modelId);

            if (model != null)
            {
                model.ModelModifiers = modifiers;
                OnContextChanged?.Invoke();
            }
        }

        public void UpdateWeaponModifiers(string unitId, string modelId, string weaponId, WeaponModifiers modifiers)
        {
            var unit = GetUnit(unitId);
            var model = unit?.Models.FirstOrDefault(m => m.Id == modelId);
            var weapon = model?.Weapons.FirstOrDefault(w => w.Id == weaponId);

            if (weapon != null)
            {
                weapon.WeaponModifiers = modifiers;
                OnContextChanged?.Invoke();
            }
        }

        // ===== Priority Management =====

        public void UpdateUnitFiringPriority(string unitId, int priority)
        {
            var unit = GetUnit(unitId);
            if (unit != null)
            {
                unit.FiringPriority = priority;
                OnContextChanged?.Invoke();
            }
        }

        public void UpdateWeaponFiringPriority(string unitId, string modelId, string weaponId, int priority)
        {
            var unit = GetUnit(unitId);
            var model = unit?.Models.FirstOrDefault(m => m.Id == modelId);
            var weapon = model?.Weapons.FirstOrDefault(w => w.Id == weaponId);

            if (weapon != null)
            {
                weapon.FiringPriority = priority;
                OnContextChanged?.Invoke();
            }
        }

        // ===== Configuration =====

        public void UpdateFiringOrder(FiringOrderConfig config)
        {
            _context.FiringOrder = config;
            OnContextChanged?.Invoke();
        }

        // ===== Utility =====

        public void ClearAll()
        {
            _context = new VersusContextV2();
            OnContextChanged?.Invoke();
        }

        public void ClearAttackers()
        {
            _context.AttackingUnits.Clear();
            OnContextChanged?.Invoke();
        }

        public void ClearDefenders()
        {
            _context.DefendingUnits.Clear();
            OnContextChanged?.Invoke();
        }

        public bool CanCalculate()
        {
            return _context.AttackingUnits.Any(u => u.Models.Any(m => m.Weapons.Any(w => w.IsSelected))) &&
                   _context.DefendingUnits.Any(u => u.Models.Any());
        }

        private void ApplyWeaponAbilityModifiers(CombatWeapon weapon)
        {
            var abilities = weapon.ParsedAbilities;

            // Twin-linked: Re-roll wounds
            if (abilities.TwinLinked == 1)
            {
                weapon.WeaponModifiers.TwinLinked = true;
            }

            // Torrent: Auto-hits
            if (abilities.Torrent == 1)
            {
                weapon.WeaponModifiers.Torrent = true;
            }

            // Ignores Cover
            if (abilities.Ignores)
            {
                weapon.WeaponModifiers.IgnoreCover = true;
            }

            // Other abilities can be added here as needed
        }

        private int ParseValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;

            value = value.Replace("+", "").Replace("″", "").Replace("\"", "").Trim();

            if (int.TryParse(value, out var result))
                return result;

            // For dice notation, return average
            if (value.Contains('D') || value.Contains('d'))
            {
                var parts = value.Split(new[] { 'D', 'd' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 1)
                {
                    if (int.TryParse(parts[0], out var dice))
                        return (dice + 1) / 2;
                }
                else if (parts.Length == 2)
                {
                    var numDice = int.TryParse(parts[0], out var nd) ? nd : 1;
                    var diceSize = int.TryParse(parts[1], out var ds) ? ds : 6;
                    return (int)Math.Round(numDice * (diceSize + 1) / 2.0);
                }

                return 3;
            }

            return 0;
        }
    }
}

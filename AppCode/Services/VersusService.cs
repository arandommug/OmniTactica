using OmniTactica.AppCode.Models.Core;
using OmniTactica.AppCode.Utilities;

namespace OmniTactica.AppCode.Services
{
    /// <summary>
    /// State service for managing versus combat calculations.
    /// Maintains attacker/defender context across navigation.
    /// </summary>
    public class VersusService
    {
        private VersusContext _context = new();

        public event Action? OnContextChanged;

        public VersusContext GetContext() => _context;

        public AttackerContext? GetAttacker() => _context.Attacker;

        public DefenderContext? GetDefender() => _context.Defender;

        public void SetAttacker(int datasheetId, string datasheetName)
        {
            _context.Attacker = new AttackerContext
            {
                DatasheetId = datasheetId,
                DatasheetName = datasheetName
            };
            OnContextChanged?.Invoke();
        }

        public void SetDefender(int datasheetId, string datasheetName)
        {
            _context.Defender = new DefenderContext
            {
                DatasheetId = datasheetId,
                DatasheetName = datasheetName
            };
            OnContextChanged?.Invoke();
        }

        public void SetAttackerWeapon(DatasheetWargear weapon, int line, int modelCount = 1)
        {
            if (_context.Attacker != null)
            {
                _context.Attacker.SelectedWeapon = weapon;
                _context.Attacker.SelectedWeaponLine = line;
                _context.Attacker.ModelsWithWeapon = modelCount;
                _context.Attacker.WeaponAbilities = WeaponAbilityParser.Parse(weapon.Description);
                OnContextChanged?.Invoke();
            }
        }

        public void SetAttackerModelCount(int modelCount)
        {
            if (_context.Attacker != null)
            {
                _context.Attacker.ModelsWithWeapon = Math.Max(1, modelCount);
                OnContextChanged?.Invoke();
            }
        }

        public void SetDefenderModel(DatasheetModel model)
        {
            if (_context.Defender != null)
            {
                _context.Defender.SelectedModel = model;
                OnContextChanged?.Invoke();
            }
        }

        public void UpdateAttackerModifiers(AttackerModifiers modifiers)
        {
            if (_context.Attacker != null)
            {
                _context.Attacker.Modifiers = modifiers;
                OnContextChanged?.Invoke();
            }
        }

        public void UpdateDefenderModifiers(DefenderModifiers modifiers)
        {
            if (_context.Defender != null)
            {
                _context.Defender.Modifiers = modifiers;
                OnContextChanged?.Invoke();
            }
        }

        public void SwapAttackerDefender()
        {
            if (_context.Defender != null && _context.Attacker != null)
            {
                var newAttacker = new AttackerContext
                {
                    DatasheetId = _context.Defender.DatasheetId,
                    DatasheetName = _context.Defender.DatasheetName
                };

                var newDefender = new DefenderContext
                {
                    DatasheetId = _context.Attacker.DatasheetId,
                    DatasheetName = _context.Attacker.DatasheetName
                };

                _context.Attacker = newAttacker;
                _context.Defender = newDefender;
            }

            OnContextChanged?.Invoke();
        }

        public void ClearAttacker()
        {
            _context.Attacker = null;
            OnContextChanged?.Invoke();
        }

        public void ClearDefender()
        {
            _context.Defender = null;
            OnContextChanged?.Invoke();
        }

        public void ClearAll()
        {
            _context = new VersusContext();
            OnContextChanged?.Invoke();
        }

        public bool HasAttacker() => _context.Attacker != null;

        public bool HasDefender() => _context.Defender != null;

        public bool CanCalculate() => 
            HasAttacker() && 
            HasDefender() && 
            _context.Attacker?.SelectedWeapon != null;
    }
}

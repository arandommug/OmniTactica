namespace OmniTactica.AppCode.Models.Core
{
    /// <summary>
    /// Enhanced context for multi-unit versus combat calculations.
    /// Supports multiple attackers, defenders, individual modifiers, and firing order.
    /// </summary>
    public class VersusContextV2
    {
        public List<CombatUnit> AttackingUnits { get; set; } = new();
        public List<CombatUnit> DefendingUnits { get; set; } = new();
        public GlobalModifiers AttackerGlobalModifiers { get; set; } = new();
        public GlobalModifiers DefenderGlobalModifiers { get; set; } = new();
        public FiringOrderConfig FiringOrder { get; set; } = new();
    }

    /// <summary>
    /// Represents a combat unit with multiple models and weapons.
    /// </summary>
    public class CombatUnit
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int DatasheetId { get; set; }
        public string DatasheetName { get; set; } = string.Empty;
        public List<CombatModel> Models { get; set; } = new();
        public UnitModifiers UnitModifiers { get; set; } = new();
        public int FiringPriority { get; set; } = 0; // For ordering
        public DatasheetDetail? Datasheet { get; set; } // Cached datasheet data
    }

    /// <summary>
    /// Represents an individual model within a unit.
    /// </summary>
    public class CombatModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DatasheetModel? ModelProfile { get; set; }
        public int CurrentWounds { get; set; }
        public int MaxWounds { get; set; }
        public List<CombatWeapon> Weapons { get; set; } = new();
        public ModelModifiers ModelModifiers { get; set; } = new();
        public bool IsDestroyed => CurrentWounds <= 0;
    }

    /// <summary>
    /// Represents a weapon equipped on a model.
    /// </summary>
    public class CombatWeapon
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DatasheetWargear? WeaponProfile { get; set; }
        public bool IsSelected { get; set; } = false; // Whether to use in combat
        public int FiringPriority { get; set; } = 0;
        public WeaponModifiers WeaponModifiers { get; set; } = new();
        public WeaponAbilities ParsedAbilities { get; set; } = new();
    }

    /// <summary>
    /// Global modifiers applied to all units on one side.
    /// </summary>
    public class GlobalModifiers
    {
        public int HitModifier { get; set; } = 0;
        public int WoundModifier { get; set; } = 0;
        public int SaveModifier { get; set; } = 0;
        public bool Cover { get; set; }
        public bool IgnoreCover { get; set; }
    }

    /// <summary>
    /// Modifiers for an entire unit.
    /// </summary>
    public class UnitModifiers
    {
        public int HitModifier { get; set; } = 0;
        public int WoundModifier { get; set; } = 0;
        public int SaveModifier { get; set; } = 0;
        public int InvulnerableSaveModifier { get; set; } = 0;
        public int ToughnessModifier { get; set; } = 0;
        public bool RerollHits { get; set; }
        public bool RerollOnes { get; set; }
        public bool RerollWounds { get; set; }
        public bool FeelNoPain { get; set; }
        public int FeelNoPainValue { get; set; } = 6;
        public bool Cover { get; set; }
        public int DamageReduction { get; set; } = 0;
        public bool HalveDamage { get; set; }
    }

    /// <summary>
    /// Modifiers for an individual model.
    /// </summary>
    public class ModelModifiers
    {
        public int HitModifier { get; set; } = 0;
        public int WoundModifier { get; set; } = 0;
        public int SaveModifier { get; set; } = 0;
        public int InvulnerableSaveModifier { get; set; } = 0;
        public int ToughnessModifier { get; set; } = 0;
        public bool FeelNoPain { get; set; }
        public int FeelNoPainValue { get; set; } = 6;
        public int DamageReduction { get; set; } = 0;
        public bool HalveDamage { get; set; }
    }

    /// <summary>
    /// Modifiers for an individual weapon.
    /// </summary>
    public class WeaponModifiers
    {
        public int HitModifier { get; set; } = 0;
        public int WoundModifier { get; set; } = 0;
        public int APModifier { get; set; } = 0;
        public int DamageModifier { get; set; } = 0;
        public int ExtraAttacks { get; set; } = 0;
        public bool RerollHits { get; set; }
        public bool RerollOnes { get; set; }
        public bool RerollWounds { get; set; }
        public int SustainedHits { get; set; } = 0;
        public int LethalHits { get; set; } = 0;
        public int DevastatingWounds { get; set; } = 0;
        public int CriticalHit { get; set; } = 6;
        public int CriticalWound { get; set; } = 6;
        public bool IgnoreInvulnerable { get; set; }
        public bool IgnoreCover { get; set; }
        public bool TwinLinked { get; set; } // Auto-applied from abilities
        public bool Torrent { get; set; } // Auto-applied from abilities
    }

    /// <summary>
    /// Configuration for firing and wound allocation order.
    /// </summary>
    public class FiringOrderConfig
    {
        public FiringOrderType OrderType { get; set; } = FiringOrderType.ByUnit;
        public WoundAllocationMethod AllocationMethod { get; set; } = WoundAllocationMethod.EvenDistribution;
    }

    public enum FiringOrderType
    {
        ByUnit,          // Fire all weapons from one unit before next
        ByPriority,      // Fire by assigned priority numbers
        Simultaneous     // All attacks resolved simultaneously
    }

    public enum WoundAllocationMethod
    {
        EvenDistribution,    // Spread wounds evenly
        TargetWeakest,       // Target already-wounded models first
        TargetStrongest      // Target full-health models first
    }

    /// <summary>
    /// Result of combat showing outcomes per unit/weapon.
    /// </summary>
    public class VersusResultV2
    {
        public double AverageTotalDamage { get; set; }
        public double AverageModelsKilled { get; set; }
        public int MedianDamage { get; set; }
        public double WipeoutProbability { get; set; }
        public int SimulationCount { get; set; }

        public Dictionary<int, double> DamageDistribution { get; set; } = new();
        public Dictionary<string, UnitCombatStats> UnitStats { get; set; } = new(); // Key: Unit ID
        public Dictionary<string, WeaponCombatStats> WeaponStats { get; set; } = new(); // Key: Weapon ID
        public Dictionary<string, double> StageBreakdown { get; set; } = new();
    }

    /// <summary>
    /// Statistics for a single unit's combat performance.
    /// </summary>
    public class UnitCombatStats
    {
        public string UnitId { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public double AverageDamageDealt { get; set; }
        public double AverageModelsSurviving { get; set; }
        public int TotalModelsStarting { get; set; }
    }

    /// <summary>
    /// Statistics for a single weapon's combat performance.
    /// </summary>
    public class WeaponCombatStats
    {
        public string WeaponId { get; set; } = string.Empty;
        public string WeaponName { get; set; } = string.Empty;
        public double AverageHits { get; set; }
        public double AverageWounds { get; set; }
        public double AverageDamage { get; set; }
    }
}

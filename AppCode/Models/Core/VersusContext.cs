using OmniTactica.AppCode.Models.Rules;

namespace OmniTactica.AppCode.Models.Core
{
    /// <summary>
    /// Context for unit vs unit combat calculations with comprehensive modifier support.
    /// </summary>
    public class VersusContext
    {
        public List<CombatUnit> AttackingUnits { get; set; } = new();
        public List<CombatUnit> DefendingUnits { get; set; } = new();
        public GlobalModifiers AttackerGlobalModifiers { get; set; } = new();
        public GlobalModifiers DefenderGlobalModifiers { get; set; } = new();
        public SimulationSettings SimulationSettings { get; set; } = new();
    }

    /// <summary>
    /// Represents a combat unit with models and weapons.
    /// </summary>
    public class CombatUnit
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int DatasheetId { get; set; }
        public string DatasheetName { get; set; } = string.Empty;
        public string FactionId { get; set; } = string.Empty;
        public List<CombatModel> Models { get; set; } = new();
        public List<ConditionalModifier> Modifiers { get; set; } = new();
        public DatasheetDetail? DatasheetDetail { get; set; }
    }

    /// <summary>
    /// Represents an individual model within a unit.
    /// </summary>
    public class CombatModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;

        // Stats
        public int M { get; set; }
        public int T { get; set; }
        public int Sv { get; set; }
        public int InvSv { get; set; }
        public int W { get; set; }
        public int Ld { get; set; }
        public int OC { get; set; }

        // Current state
        public int CurrentWounds { get; set; }
        public int MaxWounds { get; set; }
        public bool IsDestroyed => CurrentWounds <= 0;

        // Equipment
        public List<CombatWeapon> Weapons { get; set; } = new();
        public List<ConditionalModifier> Modifiers { get; set; } = new();

        // Reference to original datasheet model
        public DatasheetModel? DatasheetModel { get; set; }
    }

    /// <summary>
    /// Represents a weapon equipped on a model.
    /// </summary>
    public class CombatWeapon
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = true;

        // Weapon stats
        public string Range { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string A { get; set; } = string.Empty;
        public int BsWs { get; set; }
        public int S { get; set; }
        public int AP { get; set; }
        public string D { get; set; } = string.Empty;

        // Parsed abilities
        public WeaponAbilities Abilities { get; set; } = new();
        public List<ConditionalModifier> Modifiers { get; set; } = new();

        // Reference to original datasheet weapon
        public DatasheetWargear? DatasheetWargear { get; set; }
    }

    /// <summary>
    /// Weapon abilities parsed from description.
    /// </summary>
    public class WeaponAbilities
    {
        public bool AntiInfantry { get; set; }
        public int? AntiInfantryValue { get; set; }
        public bool AntiVehicle { get; set; }
        public int? AntiVehicleValue { get; set; }
        public bool AntiMonster { get; set; }
        public int? AntiMonsterValue { get; set; }
        public bool Assault { get; set; }
        public bool Blast { get; set; }
        public bool DevastatingWounds { get; set; }
        public bool Hazardous { get; set; }
        public bool Heavy { get; set; }
        public bool IgnoresCover { get; set; }
        public bool IndirectFire { get; set; }
        public bool LethalHits { get; set; }
        public int? Melta { get; set; }
        public bool Pistol { get; set; }
        public bool Precision { get; set; }
        public int? RapidFire { get; set; }
        public int? SustainedHits { get; set; }
        public bool Torrent { get; set; }
        public bool TwinLinked { get; set; }
    }

    /// <summary>
    /// Conditional modifier with trigger and effect.
    /// </summary>
    public class ConditionalModifier
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public ModifierCondition Condition { get; set; } = new();
        public ModifierEffect Effect { get; set; } = new();
        public bool IsActive { get; set; } = true;
        public bool IsCustom { get; set; } = false;
    }

    /// <summary>
    /// Condition that triggers a modifier.
    /// </summary>
    public class ModifierCondition
    {
        public ConditionType Type { get; set; }
        public string? Value { get; set; }
    }

    /// <summary>
    /// Effect applied when condition is met.
    /// </summary>
    public class ModifierEffect
    {
        public EffectType Type { get; set; }
        public int? IntValue { get; set; }
        public string? StringValue { get; set; }
    }

    /// <summary>
    /// Types of conditions for modifiers.
    /// </summary>
    public enum ConditionType
    {
        Always,
        UnitCharged,
        TargetWithinHalfRange,
        TargetWithinEngagementRange,
        UnitRemainedStationary,
        TargetIsInfantry,
        TargetIsVehicle,
        TargetIsMonster,
        TargetIsCharacter,
        TargetUnitSize5Plus,
        TargetUnitSize10Plus,
        CriticalHit,
        CriticalWound,
        CustomCondition
    }

    /// <summary>
    /// Types of effects for modifiers.
    /// </summary>
    public enum EffectType
    {
        AddHitModifier,
        AddWoundModifier,
        AddSaveModifier,
        AddAPModifier,
        AddDamageModifier,
        AddAttacks,
        AddExtraHitsOnCrit,
        AutoWoundOnCrit,
        ConvertToMortalWounds,
        RerollHits,
        RerollWounds,
        RerollOnes,
        IgnoreInvulnerable,
        IgnoreCover,
        HalveDamage,
        ReduceDamage,
        FeelNoPain,
        CriticalHitOn,
        CriticalWoundOn,
        CustomEffect
    }

    /// <summary>
    /// Global modifiers applied to all units on one side.
    /// </summary>
    public class GlobalModifiers
    {
        public int HitModifier { get; set; } = 0;
        public int WoundModifier { get; set; } = 0;
        public int SaveModifier { get; set; } = 0;
        public bool Cover { get; set; } = false;
        public bool IgnoreCover { get; set; } = false;
        public List<ConditionalModifier> Modifiers { get; set; } = new();
    }

    /// <summary>
    /// Settings for the simulation.
    /// </summary>
    public class SimulationSettings
    {
        public int Iterations { get; set; } = 10000;
        public WoundAllocationMethod WoundAllocation { get; set; } = WoundAllocationMethod.EvenDistribution;
        public bool EnableDetailedLogging { get; set; } = true;
        public bool AttackerCharged { get; set; } = false;
        public int? RangeToTarget { get; set; }
    }

    /// <summary>
    /// Method for allocating wounds to models.
    /// </summary>
    public enum WoundAllocationMethod
    {
        EvenDistribution,
        TargetWeakest,
        TargetStrongest,
        RandomAllocation
    }

    /// <summary>
    /// Result of combat simulation.
    /// </summary>
    public class VersusResult
    {
        public int SimulationCount { get; set; }
        public double AverageDamage { get; set; }
        public double MedianDamage { get; set; }
        public double MinDamage { get; set; }
        public double MaxDamage { get; set; }
        public double StandardDeviation { get; set; }
        public double AverageModelsKilled { get; set; }
        public double WipeoutProbability { get; set; }

        // Distribution
        public Dictionary<int, double> DamageDistribution { get; set; } = new();

        // Per-unit stats
        public Dictionary<string, UnitCombatStats> UnitStats { get; set; } = new();

        // Per-weapon stats
        public Dictionary<string, WeaponCombatStats> WeaponStats { get; set; } = new();

        // Stage breakdown
        public StageStatistics Stages { get; set; } = new();

        // Sample combat log from one simulation
        public List<CombatLogEntry> SampleCombatLog { get; set; } = new();
    }

    /// <summary>
    /// Statistics for a single unit's combat performance.
    /// </summary>
    public class UnitCombatStats
    {
        public string UnitId { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public double AverageDamageDealt { get; set; }
        public double AverageModelsKilled { get; set; }
        public int TotalModelsStarting { get; set; }
        public double AverageSurvivingModels { get; set; }
        public Dictionary<string, double> WeaponContributions { get; set; } = new();
    }

    /// <summary>
    /// Statistics for a single weapon's combat performance.
    /// </summary>
    public class WeaponCombatStats
    {
        public string WeaponId { get; set; } = string.Empty;
        public string WeaponName { get; set; } = string.Empty;
        public double AverageAttacks { get; set; }
        public double AverageHits { get; set; }
        public double AverageWounds { get; set; }
        public double AverageUnsavedWounds { get; set; }
        public double AverageDamage { get; set; }
        public double HitRate { get; set; }
        public double WoundRate { get; set; }
        public double SaveFailRate { get; set; }
    }

    /// <summary>
    /// Statistics for each stage of combat.
    /// </summary>
    public class StageStatistics
    {
        public double TotalAttacks { get; set; }
        public double TotalHits { get; set; }
        public double CriticalHits { get; set; }
        public double TotalWounds { get; set; }
        public double CriticalWounds { get; set; }
        public double MortalWounds { get; set; }
        public double FailedSaves { get; set; }
        public double SuccessfulSaves { get; set; }
        public double InvulnerableSaves { get; set; }
        public double FeelNoPainSaves { get; set; }
        public double TotalDamageDealt { get; set; }
        public double TotalDamagePrevented { get; set; }
    }

    /// <summary>
    /// Entry in the combat log.
    /// </summary>
    public class CombatLogEntry
    {
        public int Step { get; set; }
        public string Phase { get; set; } = string.Empty;
        public string AttackerUnit { get; set; } = string.Empty;
        public string AttackerModel { get; set; } = string.Empty;
        public string WeaponName { get; set; } = string.Empty;
        public string DefenderUnit { get; set; } = string.Empty;
        public string DefenderModel { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, object> Details { get; set; } = new();
    }
}

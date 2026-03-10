namespace OmniTactica.AppCode.Models.Core
{
    /// <summary>
    /// Represents the complete state for a mathhammer combat calculation.
    /// </summary>
    public class MathhammerContext
    {
        public AttackerContext? Attacker { get; set; }
        public DefenderContext? Defender { get; set; }
    }

    /// <summary>
    /// Represents the attacking unit in a combat scenario.
    /// </summary>
    public class AttackerContext
    {
        public int DatasheetId { get; set; }
        public string DatasheetName { get; set; } = string.Empty;
        public int? SelectedWeaponLine { get; set; }
        public DatasheetWargear? SelectedWeapon { get; set; }
        public int ModelsWithWeapon { get; set; } = 1;
        public WeaponAbilities WeaponAbilities { get; set; } = new();

        public AttackerModifiers Modifiers { get; set; } = new();
    }

    /// <summary>
    /// Parsed weapon abilities from weapon description.
    /// </summary>
    public class WeaponAbilities
    {
        public int? RapidFire { get; set; }
        public bool Heavy { get; set; }
        public bool Assault { get; set; }
        public bool Pistol { get; set; }
        public int? Blast { get; set; }
        public int? TwinLinked { get; set; }
        public int? Torrent { get; set; }
        public bool Melta { get; set; }
        public int? MeltaRange { get; set; }
        public bool Ignores { get; set; }
        public bool AntiKeyword { get; set; }
        public string? AntiKeywordValue { get; set; }
    }

    /// <summary>
    /// Represents the defending unit in a combat scenario.
    /// </summary>
    public class DefenderContext
    {
        public int DatasheetId { get; set; }
        public string DatasheetName { get; set; } = string.Empty;
        public DatasheetModel? SelectedModel { get; set; }
        
        public DefenderModifiers Modifiers { get; set; } = new();
    }

    /// <summary>
    /// Combat modifiers for the attacker.
    /// </summary>
    public class AttackerModifiers
    {
        public int HitModifier { get; set; } = 0;
        public int WoundModifier { get; set; } = 0;
        public bool RerollHits { get; set; }
        public bool RerollOnes { get; set; }
        public bool RerollWounds { get; set; }
        public int SustainedHits { get; set; } = 0;
        public int LethalHits { get; set; } = 0;
        public int DevastatingWounds { get; set; } = 0;
        public int CriticalHit { get; set; } = 6;
        public int CriticalWound { get; set; } = 6;
        public int ExtraAttacks { get; set; } = 0;
        public int ExtraShots { get; set; } = 0;
        public bool IgnoreInvulnerable { get; set; }
        public bool IgnoreCover { get; set; }
    }

    /// <summary>
    /// Combat modifiers for the defender.
    /// </summary>
    public class DefenderModifiers
    {
        public int ArmorSaveModifier { get; set; } = 0;
        public int InvulnerableSaveModifier { get; set; } = 0;
        public int ToughnessModifier { get; set; } = 0;
        public bool FeelNoPain { get; set; }
        public int FeelNoPainValue { get; set; } = 6;
        public bool Cover { get; set; }
        public bool Stealth { get; set; }
        public int DamageReduction { get; set; } = 0;
        public bool HalveDamage { get; set; }
    }
}

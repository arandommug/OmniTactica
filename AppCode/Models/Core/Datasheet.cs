using OmniTactica.AppCode.Models.Rules;

namespace OmniTactica.AppCode.Models.Core
{
    /// <summary>
    /// Represents a unit datasheet (basic info for list view).
    /// </summary>
    public class Datasheet
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FactionId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Legend { get; set; } = string.Empty;
        public List<string> Keywords { get; set; } = new();
        public int? PointsCost { get; set; }
    }

    /// <summary>
    /// Represents a complete unit datasheet with all details.
    /// </summary>
    public class DatasheetDetail
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FactionId { get; set; } = string.Empty;
        public string Legend { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Loadout { get; set; } = string.Empty;
        public string Transport { get; set; } = string.Empty;
        public bool Virtual { get; set; }
        public string LeaderHead { get; set; } = string.Empty;
        public string LeaderFooter { get; set; } = string.Empty;
        public string DamagedW { get; set; } = string.Empty;
        public string DamagedDescription { get; set; } = string.Empty;

        // Related collections
        public List<DatasheetModel> Models { get; set; } = new();
        public List<DatasheetWargear> Wargear { get; set; } = new();
        public List<DatasheetAbility> Abilities { get; set; } = new();
        public List<DatasheetOption> Options { get; set; } = new();
        public List<DatasheetCost> Costs { get; set; } = new();
        public List<string> Keywords { get; set; } = new();
        public List<string> FactionKeywords { get; set; } = new();

        // Leader relationships
        public List<DatasheetLeader> CanLead { get; set; } = new();
        public List<DatasheetLeader> LeadBy { get; set; } = new();

        // Detachment-related (filtered by selected detachment)
        public List<DetachmentAbility> DetachmentAbilities { get; set; } = new();
        public List<Enhancement> Enhancements { get; set; } = new();
        public List<Stratagem> Stratagems { get; set; } = new();
    }

    /// <summary>
    /// Represents a model in a datasheet (stat line).
    /// </summary>
    public class DatasheetModel
    {
        public int Line { get; set; }
        public string Name { get; set; } = string.Empty;
        public string M { get; set; } = string.Empty;
        public string T { get; set; } = string.Empty;
        public string Sv { get; set; } = string.Empty;
        public string InvSv { get; set; } = string.Empty;
        public string InvSvDescr { get; set; } = string.Empty;
        public string W { get; set; } = string.Empty;
        public string Ld { get; set; } = string.Empty;
        public string OC { get; set; } = string.Empty;
        public string BaseSize { get; set; } = string.Empty;
        public string BaseSizeDescr { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a weapon or wargear item.
    /// </summary>
    public class DatasheetWargear
    {
        public int Line { get; set; }
        public int LineInWargear { get; set; }
        public string Dice { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Range { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string A { get; set; } = string.Empty;
        public string BsWs { get; set; } = string.Empty;
        public string S { get; set; } = string.Empty;
        public string AP { get; set; } = string.Empty;
        public string D { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a unit-specific ability.
    /// </summary>
    public class DatasheetAbility
    {
        public int Line { get; set; }
        public int? AbilityId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Parameter { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a wargear option.
    /// </summary>
    public class DatasheetOption
    {
        public int Line { get; set; }
        public string Button { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a point cost entry.
    /// </summary>
    public class DatasheetCost
    {
        public int Line { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Cost { get; set; }
    }

    /// <summary>
    /// Represents a leader relationship.
    /// </summary>
    public class DatasheetLeader
    {
        public int DatasheetId { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}

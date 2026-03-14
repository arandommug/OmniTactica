using OmniTactica.AppCode.Models.Rules;

namespace OmniTactica.AppCode.Models.Core
{
    public class ArmyListCollection
    {
        public string ActiveListId { get; set; } = string.Empty;
        public List<ArmyList> Lists { get; set; } = new();
    }

    public class ArmyList
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "New Army List";
        public string FactionId { get; set; } = string.Empty;
        public string FactionName { get; set; } = string.Empty;
        public int? DetachmentId { get; set; }
        public string DetachmentName { get; set; } = string.Empty;
        public int PointsLimit { get; set; } = 2000;
        public List<ArmyListUnit> Units { get; set; } = new();
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

        public int TotalPoints => Units.Sum(unit => unit.TotalPoints);
        public int RemainingPoints => PointsLimit - TotalPoints;
    }

    public class ArmyListUnit
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int DatasheetId { get; set; }
        public string DatasheetName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public List<string> Keywords { get; set; } = new();
        public List<string> Composition { get; set; } = new();
        public List<ArmyListUnitModel> Models { get; set; } = new();
        public List<ArmyListCostOption> CostOptions { get; set; } = new();
        public int? SelectedCostLine { get; set; }
        public string SelectedCostDescription { get; set; } = "Base unit";
        public int SelectedPoints { get; set; }
        public bool CanTakeEnhancement { get; set; }
        public int? EnhancementId { get; set; }
        public string EnhancementName { get; set; } = string.Empty;
        public int EnhancementCost { get; set; }
        public string Notes { get; set; } = string.Empty;

        public int TotalPoints => SelectedPoints + EnhancementCost;
        public bool HasEnhancement => EnhancementId.HasValue;
        public int TotalModels => Models.Sum(model => model.Quantity);
    }

    public class ArmyListUnitModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public int MinQuantity { get; set; } = 1;
        public int MaxQuantity { get; set; } = 1;
        public string M { get; set; } = string.Empty;
        public string T { get; set; } = string.Empty;
        public string Sv { get; set; } = string.Empty;
        public string InvSv { get; set; } = string.Empty;
        public string W { get; set; } = string.Empty;
        public string Ld { get; set; } = string.Empty;
        public string OC { get; set; } = string.Empty;
        public string BaseSize { get; set; } = string.Empty;
        public List<ArmyListUnitWeapon> Weapons { get; set; } = new();
    }

    public class ArmyListUnitWeapon
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = true;
        public int Quantity { get; set; } = 1;
        public string Range { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string A { get; set; } = string.Empty;
        public string BsWs { get; set; } = string.Empty;
        public string S { get; set; } = string.Empty;
        public string AP { get; set; } = string.Empty;
        public string D { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class ArmyListCostOption
    {
        public int Line { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Cost { get; set; }
    }
}

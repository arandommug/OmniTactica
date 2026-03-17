using OmniTactica.AppCode.Models.Core;

namespace OmniTactica.AppCode.Helpers
{
    public static class StatlineDisplayFactory
    {
        public static List<UnitStatlineDisplayItem> CreateDatasheetStatlines(IEnumerable<DatasheetModel> models)
        {
            return models
                .GroupBy(model => new
                {
                    model.M,
                    model.T,
                    model.Sv,
                    model.W,
                    model.BaseSize,
                    model.BaseSizeDescr,
                    model.Ld,
                    model.OC,
                    model.InvSv,
                    model.InvSvDescr
                })
                .Select(group => new UnitStatlineDisplayItem
                {
                    Name = string.Join(", ", group.Select(model => model.Name)),
                    M = group.Key.M,
                    T = group.Key.T,
                    Sv = group.Key.Sv,
                    W = group.Key.W,
                    BaseSize = group.Key.BaseSize,
                    BaseSizeDescr = group.Key.BaseSizeDescr,
                    Ld = group.Key.Ld,
                    OC = group.Key.OC,
                    InvSv = group.Key.InvSv,
                    InvSvDescr = group.Key.InvSvDescr
                })
                .ToList();
        }

        public static List<UnitStatlineDisplayItem> CreateArmyListStatlines(IEnumerable<ArmyListUnitModel> models)
        {
            return models
                .Select(model => new UnitStatlineDisplayItem
                {
                    Name = model.Name,
                    Quantity = model.Quantity,
                    M = model.M,
                    T = model.T,
                    Sv = model.Sv,
                    InvSv = model.InvSv,
                    W = model.W,
                    Ld = model.Ld,
                    OC = model.OC,
                    BaseSize = model.BaseSize
                })
                .ToList();
        }

        public static List<WeaponStatlineDisplayItem> CreateDatasheetWeapons(IEnumerable<DatasheetWargear> weapons)
        {
            return weapons
                .Select(weapon => new WeaponStatlineDisplayItem
                {
                    Name = weapon.Name,
                    Type = weapon.Type,
                    Range = weapon.Range,
                    A = weapon.A,
                    BsWs = weapon.BsWs,
                    S = weapon.S,
                    AP = weapon.AP,
                    D = weapon.D,
                    Description = weapon.Description
                })
                .ToList();
        }

        public static List<WeaponStatlineDisplayItem> CreateArmyListWeapons(IEnumerable<ArmyListUnitModel> models)
        {
            return models
                .SelectMany(model => model.Weapons
                    .Where(weapon => weapon.IsSelected)
                    .Select(weapon => new WeaponStatlineDisplayItem
                    {
                        Name = weapon.Name,
                        Subtitle = model.Name,
                        Quantity = weapon.Quantity,
                        Type = weapon.Type,
                        Range = weapon.Range,
                        A = weapon.A,
                        BsWs = weapon.BsWs,
                        S = weapon.S,
                        AP = weapon.AP,
                        D = weapon.D,
                        Description = weapon.Description
                    }))
                .ToList();
        }
    }
}

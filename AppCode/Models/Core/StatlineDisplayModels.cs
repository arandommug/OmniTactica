namespace OmniTactica.AppCode.Models.Core
{
    public sealed class UnitStatlineDisplayItem
    {
        public string Name { get; set; } = string.Empty;
        public int? Quantity { get; set; }
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

    public sealed class WeaponStatlineDisplayItem
    {
        public string Name { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public int? Quantity { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Range { get; set; } = string.Empty;
        public string A { get; set; } = string.Empty;
        public string BsWs { get; set; } = string.Empty;
        public string S { get; set; } = string.Empty;
        public string AP { get; set; } = string.Empty;
        public string D { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}

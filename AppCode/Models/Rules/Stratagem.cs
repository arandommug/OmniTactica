namespace OmniTactica.AppCode.Models.Rules
{
    /// <summary>
    /// Represents a tactical stratagem that can be used during battle.
    /// </summary>
    public class Stratagem
    {
        public int Id { get; set; }
        public string FactionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string CpCost { get; set; } = string.Empty;
        public string Legend { get; set; } = string.Empty;
        public string Turn { get; set; } = string.Empty;
        public string Phase { get; set; } = string.Empty;
        public string Detachment { get; set; } = string.Empty;
        public int DetachmentId { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}

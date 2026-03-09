namespace OmniTactica.AppCode.Models.Rules
{
    /// <summary>
    /// Represents a character enhancement/relic.
    /// </summary>
    public class Enhancement
    {
        public int Id { get; set; }
        public string FactionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Cost { get; set; }
        public string Detachment { get; set; } = string.Empty;
        public int DetachmentId { get; set; }
        public string Legend { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}

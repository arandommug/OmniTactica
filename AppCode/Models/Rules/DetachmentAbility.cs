namespace OmniTactica.AppCode.Models.Rules
{
    /// <summary>
    /// Represents a detachment-specific ability.
    /// Different from faction abilities - these are tied to specific detachments.
    /// </summary>
    public class DetachmentAbility
    {
        public int Id { get; set; }
        public string FactionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Legend { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Detachment { get; set; } = string.Empty;
        public int DetachmentId { get; set; }
    }
}

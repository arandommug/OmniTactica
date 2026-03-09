using ListBuilder.AppCode.Models.Rules;

namespace ListBuilder.AppCode.Models.Core
{
    /// <summary>
    /// Represents a detachment with its associated stratagems, enhancements, and abilities.
    /// </summary>
    public class Detachment
    {
        public int Id { get; set; }
        public string FactionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Legend { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;

        public List<DetachmentAbility> Abilities { get; set; } = new();
        public List<Stratagem> Stratagems { get; set; } = new();
        public List<Enhancement> Enhancements { get; set; } = new();
    }
}

namespace ListBuilder.AppCode.Models.Rules
{
    /// <summary>
    /// Represents a faction or detachment ability.
    /// </summary>
    public class Ability
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Legend { get; set; } = string.Empty;
        public string FactionId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}

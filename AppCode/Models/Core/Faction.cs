using OmniTactica.AppCode.Models.Rules;

namespace OmniTactica.AppCode.Models.Core
{
    /// <summary>
    /// Represents a Warhammer 40K faction with its associated game data.
    /// </summary>
    public class Faction
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public List<Detachment> Detachments { get; set; } = new();
        public List<Ability> Abilities { get; set; } = new();
    }
}

using System.Text.Json;

namespace OmniTactica.AppCode.Models.Core
{
    /// <summary>
    /// Represents a rule definition from the versus-rules.json file.
    /// </summary>
    public class AbilityRuleDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Trigger { get; set; } = string.Empty;
        public JsonElement Condition { get; set; }
        public JsonElement Effect { get; set; }
    }

    /// <summary>
    /// Container for the rules JSON structure.
    /// </summary>
    public class AbilityRulesContainer
    {
        public List<AbilityRuleDefinition> Rules { get; set; } = new();
    }
}

using OmniTactica.AppCode.Models.Core;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OmniTactica.AppCode.Services
{
    /// <summary>
    /// Service for loading and matching weapon abilities from versus-rules.json.
    /// </summary>
    public class AbilityRulesService
    {
        private List<AbilityRuleDefinition> _rules = new();
        private bool _initialized = false;

        /// <summary>
        /// Loads ability rules from the JSON file.
        /// </summary>
        public async Task InitializeAsync(string jsonPath = "wwwroot/data/versus-rules.json")
        {
            if (_initialized) return;

            try
            {
                var jsonContent = await File.ReadAllTextAsync(jsonPath);
                var container = JsonSerializer.Deserialize<AbilityRulesContainer>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (container != null)
                {
                    _rules = container.Rules;
                }

                _initialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load ability rules: {ex.Message}");
                _rules = new List<AbilityRuleDefinition>();
            }
        }

        /// <summary>
        /// Parses weapon description text into a list of WeaponAbility objects.
        /// </summary>
        public List<WeaponAbility> ParseAbilities(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return new List<WeaponAbility>();

            var abilities = new List<WeaponAbility>();
            var lower = description.ToLowerInvariant();

            // Parse abilities with numeric values
            ParseRapidFire(description, abilities);
            ParseMelta(description, abilities);
            ParseSustainedHits(description, abilities);
            ParseAntiKeywords(description, abilities);

            // Parse boolean abilities
            if (lower.Contains("assault")) abilities.Add(CreateAbility("assault"));
            if (lower.Contains("blast")) abilities.Add(CreateAbility("blast"));
            if (lower.Contains("devastating wounds")) abilities.Add(CreateAbility("devastating_wounds"));
            if (lower.Contains("hazardous")) abilities.Add(CreateAbility("hazardous"));
            if (lower.Contains("heavy")) abilities.Add(CreateAbility("heavy"));
            if (lower.Contains("ignores cover")) abilities.Add(CreateAbility("ignores_cover"));
            if (lower.Contains("indirect fire")) abilities.Add(CreateAbility("indirect_fire"));
            if (lower.Contains("lethal hits")) abilities.Add(CreateAbility("lethal_hits"));
            if (lower.Contains("pistol")) abilities.Add(CreateAbility("pistol_shoot_in_melee"));
            if (lower.Contains("precision")) abilities.Add(CreateAbility("precision"));
            if (lower.Contains("torrent")) abilities.Add(CreateAbility("torrent"));
            if (lower.Contains("twin-linked")) abilities.Add(CreateAbility("twin_linked"));

            return abilities;
        }

        private void ParseRapidFire(string description, List<WeaponAbility> abilities)
        {
            var match = Regex.Match(description, @"rapid fire (\d+)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var value))
            {
                abilities.Add(CreateAbility("rapid_fire", value));
            }
        }

        private void ParseMelta(string description, List<WeaponAbility> abilities)
        {
            var match = Regex.Match(description, @"melta (\d+)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var value))
            {
                abilities.Add(CreateAbility("melta", value));
            }
            else if (description.Contains("melta", StringComparison.OrdinalIgnoreCase))
            {
                abilities.Add(CreateAbility("melta", 6)); // Default melta range
            }
        }

        private void ParseSustainedHits(string description, List<WeaponAbility> abilities)
        {
            var match = Regex.Match(description, @"sustained hits (\d+)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var value))
            {
                abilities.Add(CreateAbility("sustained_hits", value));
            }
            else if (description.Contains("sustained hits", StringComparison.OrdinalIgnoreCase))
            {
                abilities.Add(CreateAbility("sustained_hits", 1));
            }
        }

        private void ParseAntiKeywords(string description, List<WeaponAbility> abilities)
        {
            var matches = Regex.Matches(description, @"anti-(\w+(?:\s+\w+)*)\s+(\d+)\+", RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                var keyword = match.Groups[1].Value;
                if (int.TryParse(match.Groups[2].Value, out var threshold))
                {
                    // Try to match to a standard anti keyword rule
                    var ruleId = $"anti_{keyword.ToLowerInvariant().Replace(" ", "_")}";
                    var rule = _rules.FirstOrDefault(r => r.Id == ruleId);

                    if (rule != null)
                    {
                        abilities.Add(new WeaponAbility
                        {
                            Id = rule.Id,
                            Name = rule.Name,
                            Value = threshold,
                            Parameter = threshold.ToString() + "+",
                            IsCustom = false
                        });
                    }
                    else
                    {
                        // Custom anti keyword
                        abilities.Add(new WeaponAbility
                        {
                            Id = $"anti_{keyword.ToLowerInvariant().Replace(" ", "_")}",
                            Name = $"Anti-{keyword}",
                            Value = threshold,
                            Parameter = threshold.ToString() + "+",
                            IsCustom = true
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Creates a WeaponAbility from a rule ID, matching against loaded rules.
        /// </summary>
        private WeaponAbility CreateAbility(string ruleId, int? value = null)
        {
            var rule = _rules.FirstOrDefault(r => r.Id == ruleId);

            if (rule != null)
            {
                return new WeaponAbility
                {
                    Id = rule.Id,
                    Name = rule.Name,
                    Value = value,
                    IsCustom = false
                };
            }

            // Fallback for abilities not in the JSON (shouldn't happen if JSON is complete)
            return new WeaponAbility
            {
                Id = ruleId,
                Name = FormatAbilityName(ruleId),
                Value = value,
                IsCustom = true
            };
        }

        /// <summary>
        /// Gets the rule definition for a given ability ID.
        /// </summary>
        public AbilityRuleDefinition? GetRule(string abilityId)
        {
            return _rules.FirstOrDefault(r => r.Id == abilityId);
        }

        /// <summary>
        /// Formats a rule ID into a readable name.
        /// </summary>
        private static string FormatAbilityName(string ruleId)
        {
            // Convert underscore-separated IDs to Title Case
            return string.Join(" ", ruleId.Split('_')
                .Select(word => char.ToUpper(word[0]) + word.Substring(1)));
        }
    }
}

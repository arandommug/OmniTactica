using OmniTactica.AppCode.Models.Core;
using OmniTactica.AppCode.Services;
using System.Text.RegularExpressions;

namespace OmniTactica.AppCode.Utilities
{
    /// <summary>
    /// Parses weapon descriptions to extract abilities and modifiers.
    /// </summary>
    public static class WeaponAbilityParser
    {
        private static AbilityRulesService? _rulesService;

        /// <summary>
        /// Initializes the parser with the rules service.
        /// </summary>
        public static void Initialize(AbilityRulesService rulesService)
        {
            _rulesService = rulesService;
        }

        public static WeaponAbilities Parse(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return new WeaponAbilities();

            var abilities = new WeaponAbilities();

            // Use the rules service if available, otherwise fallback to legacy parsing
            if (_rulesService != null)
            {
                abilities.Abilities = _rulesService.ParseAbilities(description);
            }
            else
            {
                // Fallback to inline parsing if service not initialized
                abilities.Abilities = ParseAbilitiesFallback(description);
            }

            return abilities;
        }

        /// <summary>
        /// Fallback parser when AbilityRulesService is not available.
        /// </summary>
        private static List<WeaponAbility> ParseAbilitiesFallback(string description)
        {
            var abilities = new List<WeaponAbility>();
            var lower = description.ToLowerInvariant();

            // Parse abilities with numeric values
            var rapidFireMatch = Regex.Match(description, @"rapid fire (\d+)", RegexOptions.IgnoreCase);
            if (rapidFireMatch.Success && int.TryParse(rapidFireMatch.Groups[1].Value, out var rapidFire))
            {
                abilities.Add(new WeaponAbility { Id = "rapid_fire", Name = "Rapid Fire", Value = rapidFire });
            }

            var meltaMatch = Regex.Match(description, @"melta (\d+)", RegexOptions.IgnoreCase);
            if (meltaMatch.Success && int.TryParse(meltaMatch.Groups[1].Value, out var melta))
            {
                abilities.Add(new WeaponAbility { Id = "melta", Name = "Melta", Value = melta });
            }
            else if (lower.Contains("melta"))
            {
                abilities.Add(new WeaponAbility { Id = "melta", Name = "Melta", Value = 6 });
            }

            var sustainedHitsMatch = Regex.Match(description, @"sustained hits (\d+)", RegexOptions.IgnoreCase);
            if (sustainedHitsMatch.Success && int.TryParse(sustainedHitsMatch.Groups[1].Value, out var sustainedHits))
            {
                abilities.Add(new WeaponAbility { Id = "sustained_hits", Name = "Sustained Hits", Value = sustainedHits });
            }
            else if (lower.Contains("sustained hits"))
            {
                abilities.Add(new WeaponAbility { Id = "sustained_hits", Name = "Sustained Hits", Value = 1 });
            }

            // Parse Anti keywords - Find all matches
            var antiMatches = Regex.Matches(description, @"anti-(\w+(?:\s+\w+)*)\s+(\d+)\+", RegexOptions.IgnoreCase);
            foreach (Match antiMatch in antiMatches)
            {
                var keyword = antiMatch.Groups[1].Value;
                if (int.TryParse(antiMatch.Groups[2].Value, out var threshold))
                {
                    var ruleId = $"anti_{keyword.ToLowerInvariant().Replace(" ", "_")}";
                    abilities.Add(new WeaponAbility 
                    { 
                        Id = ruleId, 
                        Name = $"Anti-{keyword}", 
                        Value = threshold,
                        Parameter = threshold.ToString() + "+"
                    });
                }
            }

            // Parse boolean abilities
            if (lower.Contains("assault")) abilities.Add(new WeaponAbility { Id = "assault", Name = "Assault" });
            if (lower.Contains("blast")) abilities.Add(new WeaponAbility { Id = "blast", Name = "Blast" });
            if (lower.Contains("devastating wounds")) abilities.Add(new WeaponAbility { Id = "devastating_wounds", Name = "Devastating Wounds" });
            if (lower.Contains("hazardous")) abilities.Add(new WeaponAbility { Id = "hazardous", Name = "Hazardous" });
            if (lower.Contains("heavy")) abilities.Add(new WeaponAbility { Id = "heavy", Name = "Heavy" });
            if (lower.Contains("ignores cover")) abilities.Add(new WeaponAbility { Id = "ignores_cover", Name = "Ignores Cover" });
            if (lower.Contains("indirect fire")) abilities.Add(new WeaponAbility { Id = "indirect_fire", Name = "Indirect Fire" });
            if (lower.Contains("lethal hits")) abilities.Add(new WeaponAbility { Id = "lethal_hits", Name = "Lethal Hits" });
            if (lower.Contains("pistol")) abilities.Add(new WeaponAbility { Id = "pistol_shoot_in_melee", Name = "Pistol" });
            if (lower.Contains("precision")) abilities.Add(new WeaponAbility { Id = "precision", Name = "Precision" });
            if (lower.Contains("torrent")) abilities.Add(new WeaponAbility { Id = "torrent", Name = "Torrent" });
            if (lower.Contains("twin-linked")) abilities.Add(new WeaponAbility { Id = "twin_linked", Name = "Twin-Linked" });

            return abilities;
        }

    }
}

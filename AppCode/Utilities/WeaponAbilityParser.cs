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

        /// <summary>
        /// Parses attack value (e.g., "2D6", "12", "6+D6").
        /// </summary>
        public static (int fixedAttacks, int diceCount, int diceSides) ParseAttacks(string attacksValue)
        {
            if (string.IsNullOrWhiteSpace(attacksValue))
                return (0, 0, 0);

            attacksValue = attacksValue.Trim().ToUpperInvariant();

            // Handle "XDY" format (e.g., "2D6")
            var diceMatch = Regex.Match(attacksValue, @"(\d+)?D(\d+)");
            if (diceMatch.Success)
            {
                var diceCount = diceMatch.Groups[1].Success ? int.Parse(diceMatch.Groups[1].Value) : 1;
                var diceSides = int.Parse(diceMatch.Groups[2].Value);
                
                // Check for fixed addition (e.g., "6+D6" or "D6+3")
                var fixedPart = attacksValue.Replace(diceMatch.Value, "").Replace("+", "").Trim();
                var fixedAttacks = string.IsNullOrEmpty(fixedPart) ? 0 : (int.TryParse(fixedPart, out var f) ? f : 0);

                return (fixedAttacks, diceCount, diceSides);
            }

            // Handle fixed number
            if (int.TryParse(attacksValue, out var fixedNum))
            {
                return (fixedNum, 0, 0);
            }

            return (0, 0, 0);
        }

        /// <summary>
        /// Calculates expected attacks accounting for dice and Rapid Fire.
        /// </summary>
        public static double CalculateExpectedAttacks(DatasheetWargear weapon, int modelCount, WeaponAbilities abilities, bool isRapidFireRange = false)
        {
            var (fixedAttacks, diceCount, diceSides) = ParseAttacks(weapon.A);

            double expectedPerModel = fixedAttacks;
            if (diceCount > 0 && diceSides > 0)
            {
                expectedPerModel += diceCount * (diceSides + 1) / 2.0;
            }

            // Apply Rapid Fire if in range
            if (abilities.RapidFire.HasValue && isRapidFireRange)
            {
                expectedPerModel *= (1 + abilities.RapidFire.Value);
            }

            // Note: Twin-Linked doesn't double attacks, it provides reroll to wound
            {
                expectedPerModel *= 2;
            }

            return expectedPerModel * modelCount;
        }
    }
}

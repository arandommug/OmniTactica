using OmniTactica.AppCode.Models.Core;
using System.Text.RegularExpressions;

namespace OmniTactica.AppCode.Utilities
{
    /// <summary>
    /// Parses weapon descriptions to extract abilities and modifiers.
    /// </summary>
    public static class WeaponAbilityParser
    {
        public static WeaponAbilities Parse(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return new WeaponAbilities();

            var abilities = new WeaponAbilities();
            var lower = description.ToLowerInvariant();

            // Rapid Fire X
            var rapidFireMatch = Regex.Match(lower, @"rapid fire (\d+)");
            if (rapidFireMatch.Success && int.TryParse(rapidFireMatch.Groups[1].Value, out var rapidFire))
            {
                abilities.RapidFire = rapidFire;
            }

            // Heavy
            if (lower.Contains("heavy"))
            {
                abilities.Heavy = true;
            }

            // Assault
            if (lower.Contains("assault"))
            {
                abilities.Assault = true;
            }

            // Pistol
            if (lower.Contains("pistol"))
            {
                abilities.Pistol = true;
            }

            // Blast
            var blastMatch = Regex.Match(lower, @"blast");
            if (blastMatch.Success)
            {
                abilities.Blast = 1;
            }

            // Twin-Linked
            if (lower.Contains("twin-linked"))
            {
                abilities.TwinLinked = 1;
            }

            // Torrent
            if (lower.Contains("torrent"))
            {
                abilities.Torrent = 1;
            }

            // Melta X
            var meltaMatch = Regex.Match(lower, @"melta (\d+)");
            if (meltaMatch.Success && int.TryParse(meltaMatch.Groups[1].Value, out var meltaRange))
            {
                abilities.Melta = true;
                abilities.MeltaRange = meltaRange;
            }
            else if (lower.Contains("melta"))
            {
                abilities.Melta = true;
                abilities.MeltaRange = 6; // Default melta range
            }

            // Ignores Cover
            if (lower.Contains("ignores cover"))
            {
                abilities.Ignores = true;
            }

            // Anti-keyword X+
            var antiMatch = Regex.Match(lower, @"anti-([a-z\s]+) (\d+)\+");
            if (antiMatch.Success)
            {
                abilities.AntiKeyword = true;
                abilities.AntiKeywordValue = antiMatch.Groups[1].Value.Trim();
            }

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

            // Apply Twin-Linked (double attacks)
            if (abilities.TwinLinked.HasValue)
            {
                expectedPerModel *= 2;
            }

            return expectedPerModel * modelCount;
        }
    }
}

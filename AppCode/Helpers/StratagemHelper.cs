using OmniTactica.AppCode.Models.Rules;

namespace OmniTactica.AppCode.Helpers
{
    /// <summary>
    /// Helper class for organizing and categorizing stratagems.
    /// </summary>
    public static class StratagemHelper
    {
        /// <summary>
        /// Organizes stratagems by phase, placing them in the correct phase categories.
        /// Stratagems that can be used in multiple phases will appear in each applicable phase.
        /// </summary>
        /// <param name="stratagems">The list of stratagems to organize.</param>
        /// <returns>A dictionary with phase names as keys and lists of stratagems as values, ordered by phase sequence.</returns>
        public static Dictionary<string, List<Stratagem>> OrganizeStratagemsByPhase(List<Stratagem> stratagems)
        {
            var phaseOrder = new List<string>
            {
                "Any phase",
                "Command phase",
                "Movement phase",
                "Movement or Charge phase",
                "Shooting phase",
                "Shooting or Charge phase",
                "Shooting or Fight phase",
                "Charge phase",
                "Charge or Fight phase",
                "Fight phase",
                "Command or Fight phase"
            };

            var organized = new Dictionary<string, List<Stratagem>>();

            foreach (var strat in stratagems)
            {
                var phases = DeterminePhases(strat.Phase);

                foreach (var phase in phases)
                {
                    if (!organized.ContainsKey(phase))
                    {
                        organized[phase] = new List<Stratagem>();
                    }
                    organized[phase].Add(strat);
                }
            }

            // Return ordered by phase order
            return phaseOrder
                .Where(phase => organized.ContainsKey(phase))
                .ToDictionary(phase => phase, phase => organized[phase]);
        }

        /// <summary>
        /// Determines which phase categories a stratagem belongs to based on its phase text.
        /// A stratagem can belong to multiple phases if it can be used in multiple game phases.
        /// </summary>
        /// <param name="phase">The phase text from the stratagem.</param>
        /// <returns>A list of phase category names the stratagem belongs to.</returns>
        public static List<string> DeterminePhases(string phase)
        {
            if (string.IsNullOrWhiteSpace(phase))
            {
                return new List<string> { "Any phase" };
            }

            var normalizedPhase = phase.Trim().ToLowerInvariant();
            var phases = new List<string>();

            // Check for combined phases first
            if (normalizedPhase.Contains("shooting") && normalizedPhase.Contains("fight"))
            {
                phases.Add("Shooting or Fight phase");
            }
            else if (normalizedPhase.Contains("shooting") && normalizedPhase.Contains("charge"))
            {
                phases.Add("Shooting or Charge phase");
            }
            else if (normalizedPhase.Contains("movement") && normalizedPhase.Contains("charge"))
            {
                phases.Add("Movement or Charge phase");
            }
            else if (normalizedPhase.Contains("charge") && normalizedPhase.Contains("fight"))
            {
                phases.Add("Charge or Fight phase");
            }
            else if (normalizedPhase.Contains("command") && normalizedPhase.Contains("fight"))
            {
                phases.Add("Command or Fight phase");
            }
            // Check individual phases
            else if (normalizedPhase.Contains("command"))
            {
                phases.Add("Command phase");
            }
            else if (normalizedPhase.Contains("movement"))
            {
                phases.Add("Movement phase");
            }
            else if (normalizedPhase.Contains("shooting"))
            {
                phases.Add("Shooting phase");
            }
            else if (normalizedPhase.Contains("charge"))
            {
                phases.Add("Charge phase");
            }
            else if (normalizedPhase.Contains("fight"))
            {
                phases.Add("Fight phase");
            }
            else
            {
                phases.Add("Any phase");
            }

            return phases;
        }

        /// <summary>
        /// Gets the appropriate Bootstrap badge class based on when a stratagem can be used (Your turn, Either player's turn, Opponent's turn).
        /// </summary>
        /// <param name="turn">The turn text from the stratagem.</param>
        /// <returns>A Bootstrap CSS class for the badge color.</returns>
        public static string GetTurnBadgeClass(string turn)
        {
            if (string.IsNullOrWhiteSpace(turn))
            {
                return "bg-secondary";
            }

            var normalizedTurn = turn.Trim().ToLowerInvariant();

            if (normalizedTurn.Contains("your turn") || normalizedTurn.Contains("you"))
            {
                return "bg-primary";  // Blue - Your turn
            }
            else if (normalizedTurn.Contains("either") || normalizedTurn.Contains("both"))
            {
                return "bg-info";  // Light blue - Either player's turn
            }
            else if (normalizedTurn.Contains("opponent"))
            {
                return "bg-danger";  // Red - Opponent's turn
            }

            return "bg-secondary";
        }

        /// <summary>
        /// Checks if a stratagem is a Core stratagem (can be used regardless of faction or detachment).
        /// </summary>
        /// <param name="stratagem">The stratagem to check.</param>
        /// <returns>True if the stratagem is a Core stratagem; otherwise, false.</returns>
        public static bool IsCoreStratagem(Stratagem stratagem)
        {
            if (string.IsNullOrWhiteSpace(stratagem.Type))
            {
                return false;
            }

            return stratagem.Type.StartsWith("Core", StringComparison.OrdinalIgnoreCase) &&
                   string.IsNullOrWhiteSpace(stratagem.FactionId);
        }

        /// <summary>
        /// Checks if a stratagem is a Core Wargear Stratagem, which can only be used by units with matching keywords.
        /// </summary>
        /// <param name="stratagem">The stratagem to check.</param>
        /// <returns>True if the stratagem is a Core Wargear Stratagem; otherwise, false.</returns>
        public static bool IsCoreWargearStratagem(Stratagem stratagem)
        {
            if (string.IsNullOrWhiteSpace(stratagem.Type))
            {
                return false;
            }

            return stratagem.Type.Equals("Core – Wargear Stratagem", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if a unit can use a Core Wargear Stratagem based on matching keywords.
        /// </summary>
        /// <param name="stratagem">The stratagem to check.</param>
        /// <param name="unitKeywords">The keywords of the unit.</param>
        /// <returns>True if the unit can use the stratagem; otherwise, false.</returns>
        public static bool CanUnitUseCoreWargearStratagem(Stratagem stratagem, IEnumerable<string> unitKeywords)
        {
            if (!IsCoreWargearStratagem(stratagem))
            {
                return false;
            }

            // The unit must have a keyword matching the stratagem name
            var stratagemName = stratagem.Name.Trim().ToUpperInvariant();
            return unitKeywords.Any(kw => kw.Trim().ToUpperInvariant() == stratagemName);
        }
    }
}

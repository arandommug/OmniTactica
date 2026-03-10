using OmniTactica.AppCode.Models.Core;
using OmniTactica.AppCode.Utilities;

namespace OmniTactica.AppCode.Services
{
    /// <summary>
    /// Result of a versus calculation showing Monte Carlo simulation outcomes.
    /// </summary>
    public class VersusResult
    {
        public double AverageHits { get; set; }
        public double AverageWounds { get; set; }
        public double AverageDamage { get; set; }
        public double AverageModelsKilled { get; set; }
        public double MedianDamage { get; set; }
        public double KillProbability { get; set; }
        public int SimulationCount { get; set; }
        
        public Dictionary<int, double> DamageDistribution { get; set; } = new();
        public Dictionary<string, double> StageBreakdown { get; set; } = new();
    }

    /// <summary>
    /// Monte Carlo simulation engine for calculating versus combat probabilities.
    /// Implements proper 10th edition attack resolution pipeline.
    /// </summary>
    public static class VersusCalculator
    {
        private static readonly Random _random = new Random();
        private const int DEFAULT_SIMULATIONS = 100000;

        // TODO: Next improvements - make buttons fully clickable, support multiple weapons selected, add buffs/debuffs for weapons and defender

        public static VersusResult Calculate(VersusContext context, int simulations = DEFAULT_SIMULATIONS)
        {
            if (context.Attacker?.SelectedWeapon == null || context.Defender?.SelectedModel == null)
                return new VersusResult { SimulationCount = simulations };

            var results = new List<SimulationRun>();

            for (int i = 0; i < simulations; i++)
            {
                results.Add(RunSimulation(context));
            }

            return AggregateResults(results, context, simulations);
        }

        private static SimulationRun RunSimulation(VersusContext context)
        {
            var attacker = context.Attacker!;
            var defender = context.Defender!;
            var weapon = attacker.SelectedWeapon!;
            var model = defender.SelectedModel!;

            var run = new SimulationRun();

            // Step 1-3: Determine attacks
            run.Attacks = DetermineAttacks(weapon, attacker);

            // Step 4-9: Resolve hits and auto-wounds
            var hitResult = ResolveHits(run.Attacks, weapon, attacker);
            run.Hits = hitResult.Hits;
            run.AutoWounds = hitResult.AutoWounds;

            // Step 10-13: Resolve wounds
            run.Wounds = ResolveWounds(hitResult.Hits - hitResult.AutoWounds, weapon, model, attacker) + hitResult.AutoWounds;

            // Step 14-18: Resolve saves and damage
            var damageResult = ResolveSavesAndDamage(run.Wounds, weapon, model, attacker, defender);
            run.MortalWounds = damageResult.MortalWounds;
            run.NormalDamage = damageResult.NormalDamage;

            // Step 19-22: Apply FNP and calculate total damage
            run.TotalDamage = ApplyFeelNoPain(damageResult.MortalWounds + damageResult.NormalDamage, defender);

            // Step 23: Calculate models killed
            var woundsPerModel = ParseValue(model.W);
            run.ModelsKilled = woundsPerModel > 0 ? run.TotalDamage / (double)woundsPerModel : 0;

            return run;
        }

        private static int DetermineAttacks(DatasheetWargear weapon, AttackerContext attacker)
        {
            var baseAttacks = ParseValue(weapon.A);
            var totalAttacks = baseAttacks * attacker.ModelsWithWeapon;
            totalAttacks += attacker.Modifiers.ExtraAttacks + attacker.Modifiers.ExtraShots;

            // TODO: Apply Rapid Fire, Blast based on conditions
            // For now, return base attacks
            return Math.Max(1, totalAttacks);
        }

        private static HitResult ResolveHits(int attacks, DatasheetWargear weapon, AttackerContext attacker)
        {
            var result = new HitResult();
            var bs_ws = ParseValue(weapon.BsWs);
            if (bs_ws == 0) return result;

            var hitModifier = attacker.Modifiers.HitModifier;
            var targetRoll = Math.Clamp(bs_ws - hitModifier, 2, 6);

            for (int i = 0; i < attacks; i++)
            {
                var hitRoll = RollD6();
                var rerolled = false;

                // Apply rerolls
                if (attacker.Modifiers.RerollHits || (attacker.Modifiers.RerollOnes && hitRoll == 1))
                {
                    hitRoll = RollD6();
                    rerolled = true;
                }

                // Check if hit
                if (hitRoll >= targetRoll)
                {
                    result.Hits++;

                    // Check for critical hit (unmodified 6)
                    var isCritical = (!rerolled && hitRoll == 6) || (rerolled && hitRoll == 6 && attacker.Modifiers.CriticalHit == 6);
                    
                    if (isCritical)
                    {
                        // Sustained Hits
                        if (attacker.Modifiers.SustainedHits > 0)
                        {
                            result.Hits += attacker.Modifiers.SustainedHits;
                        }

                        // Lethal Hits - converts this hit to auto-wound
                        if (attacker.Modifiers.LethalHits > 0)
                        {
                            result.AutoWounds++;
                            result.Hits--; // Remove from normal hits
                        }
                    }
                }
            }

            return result;
        }

        private static int ResolveWounds(int hits, DatasheetWargear weapon, DatasheetModel model, AttackerContext attacker)
        {
            if (hits <= 0) return 0;

            var wounds = 0;
            var strength = ParseValue(weapon.S);
            var toughness = ParseValue(model.T) + attacker.Modifiers.WoundModifier;

            // Calculate wound target
            int woundTarget;
            if (strength >= toughness * 2)
                woundTarget = 2;
            else if (strength > toughness)
                woundTarget = 3;
            else if (strength == toughness)
                woundTarget = 4;
            else if (strength * 2 <= toughness)
                woundTarget = 6;
            else
                woundTarget = 5;

            woundTarget = Math.Clamp(woundTarget - attacker.Modifiers.WoundModifier, 2, 6);

            for (int i = 0; i < hits; i++)
            {
                var woundRoll = RollD6();

                // Apply wound rerolls
                if (attacker.Modifiers.RerollWounds)
                {
                    woundRoll = RollD6();
                }

                if (woundRoll >= woundTarget)
                {
                    wounds++;
                }
            }

            return wounds;
        }

        private static DamageResult ResolveSavesAndDamage(int wounds, DatasheetWargear weapon, 
            DatasheetModel model, AttackerContext attacker, DefenderContext defender)
        {
            var result = new DamageResult();
            if (wounds <= 0) return result;

            var weaponAP = ParseValue(weapon.AP);
            var defenderSave = ParseValue(model.Sv) + defender.Modifiers.ArmorSaveModifier;
            var defenderInvuln = ParseValue(model.InvSv);

            for (int i = 0; i < wounds; i++)
            {
                // Check for devastating wounds (critical wound conversion)
                var isCriticalWound = false; // TODO: Track critical wounds from wound rolls
                
                if (isCriticalWound && attacker.Modifiers.DevastatingWounds > 0)
                {
                    // Devastating wounds become mortal wounds
                    result.MortalWounds += RollDamage(weapon.D);
                }
                else
                {
                    // Calculate save
                    var modifiedSave = defenderSave - weaponAP;

                    if (defender.Modifiers.Cover && !attacker.Modifiers.IgnoreCover)
                    {
                        modifiedSave -= 1;
                    }

                    var effectiveSave = modifiedSave;

                    if (!attacker.Modifiers.IgnoreInvulnerable && defenderInvuln > 0)
                    {
                        effectiveSave = Math.Min(modifiedSave, defenderInvuln + defender.Modifiers.InvulnerableSaveModifier);
                    }

                    effectiveSave = Math.Clamp(effectiveSave, 2, 7);

                    // Roll save
                    var saveSucceeds = effectiveSave < 7 && RollD6() >= effectiveSave;

                    if (!saveSucceeds)
                    {
                        var damage = RollDamage(weapon.D);

                        // Apply damage reduction
                        if (defender.Modifiers.DamageReduction > 0)
                        {
                            damage = Math.Max(1, damage - defender.Modifiers.DamageReduction);
                        }

                        if (defender.Modifiers.HalveDamage)
                        {
                            damage = Math.Max(1, damage / 2);
                        }

                        result.NormalDamage += damage;
                    }
                }
            }

            return result;
        }

        private static int ApplyFeelNoPain(int totalDamage, DefenderContext defender)
        {
            if (!defender.Modifiers.FeelNoPain || totalDamage <= 0)
                return totalDamage;

            var fnpTarget = defender.Modifiers.FeelNoPainValue;
            var remainingDamage = 0;

            for (int i = 0; i < totalDamage; i++)
            {
                if (RollD6() < fnpTarget)
                {
                    remainingDamage++;
                }
            }

            return remainingDamage;
        }

        private static VersusResult AggregateResults(List<SimulationRun> results, VersusContext context, int simulations)
        {
            var totalDamages = results.Select(r => (int)Math.Floor(r.TotalDamage)).ToList();
            totalDamages.Sort();

            var result = new VersusResult
            {
                SimulationCount = simulations,
                AverageHits = results.Average(r => r.Hits),
                AverageWounds = results.Average(r => r.Wounds),
                AverageDamage = results.Average(r => r.TotalDamage),
                AverageModelsKilled = results.Average(r => r.ModelsKilled),
                MedianDamage = totalDamages[simulations / 2]
            };

            // Calculate damage distribution
            var damageGroups = totalDamages.GroupBy(d => d).OrderBy(g => g.Key);
            foreach (var group in damageGroups)
            {
                result.DamageDistribution[group.Key] = (double)group.Count() / simulations * 100;
            }

            // Calculate kill probability (assuming defending unit has wounds equal to model wounds)
            if (context.Defender?.SelectedModel != null)
            {
                var modelWounds = ParseValue(context.Defender.SelectedModel.W);
                if (modelWounds > 0)
                {
                    var killCount = totalDamages.Count(d => d >= modelWounds);
                    result.KillProbability = (double)killCount / simulations * 100;
                }
            }

            // Stage breakdown (averages)
            result.StageBreakdown["Attacks"] = results.Average(r => r.Attacks);
            result.StageBreakdown["Average Hits"] = result.AverageHits;
            result.StageBreakdown["Average Wounds"] = result.AverageWounds;
            result.StageBreakdown["Average Damage"] = result.AverageDamage;
            result.StageBreakdown["Median Damage"] = result.MedianDamage;
            result.StageBreakdown["Models Killed"] = result.AverageModelsKilled;

            return result;
        }

        private static int RollD6() => _random.Next(1, 7);

        private static int RollDamage(string damageString)
        {
            if (string.IsNullOrEmpty(damageString)) return 0;

            damageString = damageString.Trim();

            // Handle flat damage
            if (int.TryParse(damageString, out var flatDamage))
                return flatDamage;

            // Handle dice notation
            if (damageString.Contains('D') || damageString.Contains('d'))
            {
                var parts = damageString.ToUpper().Split('D');
                
                if (parts.Length == 1)
                {
                    // "D6" format
                    if (int.TryParse(parts[0], out var dice))
                        return RollDice(1, dice);
                    return RollD6();
                }
                else if (parts.Length == 2)
                {
                    // "2D6" or "D6+2" format
                    var numDice = string.IsNullOrEmpty(parts[0]) ? 1 : int.Parse(parts[0]);
                    var remaining = parts[1];

                    // Check for modifier like "D6+2"
                    if (remaining.Contains('+'))
                    {
                        var subParts = remaining.Split('+');
                        var diceSize = int.Parse(subParts[0]);
                        var modifier = int.Parse(subParts[1]);
                        return RollDice(numDice, diceSize) + modifier;
                    }
                    else
                    {
                        var diceSize = int.Parse(remaining);
                        return RollDice(numDice, diceSize);
                    }
                }
            }

            return 0;
        }

        private static int RollDice(int count, int size)
        {
            var total = 0;
            for (int i = 0; i < count; i++)
            {
                total += _random.Next(1, size + 1);
            }
            return total;
        }

        private static int ParseValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            
            value = value.Replace("+", "").Replace("″", "").Replace("\"", "").Trim();

            if (value.Contains('D') || value.Contains('d'))
            {
                // For attack characteristics, use average
                var parts = value.Split(new[] { 'D', 'd' }, StringSplitOptions.RemoveEmptyEntries);
                
                if (parts.Length == 1)
                {
                    if (int.TryParse(parts[0], out var dice))
                        return (dice + 1) / 2;
                }
                else if (parts.Length == 2)
                {
                    var numDice = int.TryParse(parts[0], out var nd) ? nd : 1;
                    var diceSize = int.TryParse(parts[1], out var ds) ? ds : 6;
                    return (int)Math.Round(numDice * (diceSize + 1) / 2.0);
                }

                return 3;
            }

            if (int.TryParse(value, out var result))
                return result;

            return 0;
        }

        private class SimulationRun
        {
            public int Attacks { get; set; }
            public int Hits { get; set; }
            public int AutoWounds { get; set; }
            public int Wounds { get; set; }
            public int MortalWounds { get; set; }
            public int NormalDamage { get; set; }
            public double TotalDamage { get; set; }
            public double ModelsKilled { get; set; }
        }

        private class HitResult
        {
            public int Hits { get; set; }
            public int AutoWounds { get; set; }
        }

        private class DamageResult
        {
            public int MortalWounds { get; set; }
            public int NormalDamage { get; set; }
        }
    }
}

using OmniTactica.AppCode.Models.Core;
using OmniTactica.AppCode.Utilities;

namespace OmniTactica.AppCode.Services
{
    /// <summary>
    /// Result of a mathhammer calculation showing expected outcomes.
    /// </summary>
    public class MathhammerResult
    {
        public double AverageHits { get; set; }
        public double AverageWounds { get; set; }
        public double AverageDamage { get; set; }
        public double AverageModelsKilled { get; set; }
        
        public Dictionary<string, double> StageBreakdown { get; set; } = new();
    }

    /// <summary>
    /// Engine for calculating mathhammer combat probabilities.
    /// </summary>
    public static class MathhammerCalculator
    {
        public static MathhammerResult Calculate(MathhammerContext context)
        {
            if (context.Attacker?.SelectedWeapon == null || context.Defender?.SelectedModel == null)
                return new MathhammerResult();

            var attacker = context.Attacker;
            var defender = context.Defender;
            var weapon = attacker.SelectedWeapon;
            var model = defender.SelectedModel;

            var result = new MathhammerResult();

            var attacks = ParseValue(weapon.A) + attacker.Modifiers.ExtraAttacks + attacker.Modifiers.ExtraShots;
            if (attacks <= 0) return result;

            var bs_ws = ParseValue(weapon.BsWs);
            if (bs_ws == 0) return result;

            var hitChance = CalculateHitChance(bs_ws, attacker.Modifiers);
            var averageHits = attacks * hitChance;

            if (attacker.Modifiers.SustainedHits > 0)
            {
                var critChance = (7.0 - attacker.Modifiers.CriticalHit) / 6.0;
                var sustainedExtraHits = attacks * critChance * attacker.Modifiers.SustainedHits;
                averageHits += sustainedExtraHits;
            }

            result.AverageHits = averageHits;
            result.StageBreakdown["Attacks"] = attacks;
            result.StageBreakdown["Hit Chance"] = hitChance * 100;
            result.StageBreakdown["Average Hits"] = averageHits;

            var weaponStrength = ParseValue(weapon.S);
            var defenderToughness = ParseValue(model.T) + defender.Modifiers.ToughnessModifier;

            var woundChance = CalculateWoundChance(weaponStrength, defenderToughness, attacker.Modifiers);
            var averageWounds = averageHits * woundChance;

            result.AverageWounds = averageWounds;
            result.StageBreakdown["Wound Chance"] = woundChance * 100;
            result.StageBreakdown["Average Wounds"] = averageWounds;

            var weaponAP = ParseValue(weapon.AP);
            var defenderSave = ParseValue(model.Sv) + defender.Modifiers.ArmorSaveModifier;
            var defenderInvuln = ParseValue(model.InvSv);

            double unsavedWounds;
            if (attacker.Modifiers.DevastatingWounds > 0)
            {
                var critWoundChance = (7.0 - attacker.Modifiers.CriticalWound) / 6.0;
                var devastatingWounds = averageHits * critWoundChance;
                var normalWounds = averageWounds - devastatingWounds;

                var normalSaveChance = CalculateSaveChance(defenderSave, defenderInvuln, weaponAP, defender.Modifiers, attacker.Modifiers);
                unsavedWounds = devastatingWounds + (normalWounds * (1 - normalSaveChance));
            }
            else
            {
                var saveChance = CalculateSaveChance(defenderSave, defenderInvuln, weaponAP, defender.Modifiers, attacker.Modifiers);
                unsavedWounds = averageWounds * (1 - saveChance);
            }

            result.StageBreakdown["Unsaved Wounds"] = unsavedWounds;

            var weaponDamage = ParseDamageValue(weapon.D);
            var totalDamage = unsavedWounds * weaponDamage;

            if (defender.Modifiers.DamageReduction > 0)
            {
                totalDamage = Math.Max(0, totalDamage - (unsavedWounds * defender.Modifiers.DamageReduction));
            }

            if (defender.Modifiers.HalveDamage)
            {
                totalDamage /= 2.0;
            }

            if (defender.Modifiers.FeelNoPain)
            {
                var fnpChance = (defender.Modifiers.FeelNoPainValue - 1) / 6.0;
                totalDamage *= fnpChance;
            }

            result.AverageDamage = totalDamage;
            result.StageBreakdown["Damage per Wound"] = weaponDamage;
            result.StageBreakdown["Total Damage"] = totalDamage;

            var woundsPerModel = ParseValue(model.W);
            if (woundsPerModel > 0)
            {
                result.AverageModelsKilled = totalDamage / woundsPerModel;
                result.StageBreakdown["Models Killed"] = result.AverageModelsKilled;
            }

            return result;
        }

        private static double CalculateHitChance(int bsWs, AttackerModifiers modifiers)
        {
            var targetRoll = bsWs - modifiers.HitModifier;
            targetRoll = Math.Clamp(targetRoll, 2, 6);

            var baseChance = (7.0 - targetRoll) / 6.0;

            if (modifiers.RerollHits)
            {
                return baseChance + ((1 - baseChance) * baseChance);
            }

            if (modifiers.RerollOnes)
            {
                return baseChance + (1.0 / 6.0 * baseChance);
            }

            return baseChance;
        }

        private static double CalculateWoundChance(int strength, int toughness, AttackerModifiers modifiers)
        {
            int targetRoll;

            if (strength >= toughness * 2)
                targetRoll = 2;
            else if (strength > toughness)
                targetRoll = 3;
            else if (strength == toughness)
                targetRoll = 4;
            else if (strength * 2 <= toughness)
                targetRoll = 6;
            else
                targetRoll = 5;

            targetRoll -= modifiers.WoundModifier;
            targetRoll = Math.Clamp(targetRoll, 2, 6);

            var baseChance = (7.0 - targetRoll) / 6.0;

            if (modifiers.LethalHits > 0)
            {
                var critChance = (7.0 - modifiers.CriticalHit) / 6.0;
                var normalChance = (1 - critChance) * baseChance;
                return critChance + normalChance;
            }

            if (modifiers.RerollWounds)
            {
                return baseChance + ((1 - baseChance) * baseChance);
            }

            return baseChance;
        }

        private static double CalculateSaveChance(int save, int invuln, int ap, DefenderModifiers defModifiers, AttackerModifiers attModifiers)
        {
            var modifiedSave = save - ap;

            if (defModifiers.Cover && !attModifiers.IgnoreCover)
            {
                modifiedSave -= 1;
            }

            var effectiveSave = modifiedSave;

            if (!attModifiers.IgnoreInvulnerable && invuln > 0)
            {
                effectiveSave = Math.Min(modifiedSave, invuln + defModifiers.InvulnerableSaveModifier);
            }

            effectiveSave = Math.Clamp(effectiveSave, 2, 7);

            if (effectiveSave >= 7) return 0;

            return (7.0 - effectiveSave) / 6.0;
        }

        private static int ParseValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            
            value = value.Replace("+", "").Replace("″", "").Replace("\"", "").Trim();

            if (value.Contains('D') || value.Contains('d'))
                return ParseDiceValue(value);

            if (int.TryParse(value, out var result))
                return result;

            return 0;
        }

        private static double ParseDamageValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;

            value = value.Trim();

            if (value.Contains('D') || value.Contains('d'))
            {
                var parts = value.Split(new[] { 'D', 'd' }, StringSplitOptions.RemoveEmptyEntries);
                
                if (parts.Length == 1)
                {
                    if (int.TryParse(parts[0], out var dice))
                        return (dice + 1) / 2.0;
                }
                else if (parts.Length == 2)
                {
                    var numDice = int.TryParse(parts[0], out var nd) ? nd : 1;
                    var diceSize = int.TryParse(parts[1], out var ds) ? ds : 6;
                    return numDice * (diceSize + 1) / 2.0;
                }

                return 3.5;
            }

            if (int.TryParse(value, out var result))
                return result;

            return 0;
        }

        private static int ParseDiceValue(string value)
        {
            var parts = value.Split(new[] { 'D', 'd' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length == 1)
            {
                if (int.TryParse(parts[0], out var dice))
                    return (int)Math.Round((dice + 1) / 2.0);
            }
            else if (parts.Length == 2)
            {
                var numDice = int.TryParse(parts[0], out var nd) ? nd : 1;
                var diceSize = int.TryParse(parts[1], out var ds) ? ds : 6;
                return (int)Math.Round(numDice * (diceSize + 1) / 2.0);
            }

            return 3;
        }
    }
}

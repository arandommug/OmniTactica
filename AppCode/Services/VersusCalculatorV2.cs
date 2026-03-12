using OmniTactica.AppCode.Models.Core;
using OmniTactica.AppCode.Utilities;

namespace OmniTactica.AppCode.Services
{
    /// <summary>
    /// Enhanced Monte Carlo combat calculator supporting multiple units, weapons, and granular modifiers.
    /// </summary>
    public static class VersusCalculatorV2
    {
        private static readonly Random _random = new Random();
        private const int DEFAULT_SIMULATIONS = 10000;

        public static VersusResultV2 Calculate(VersusContextV2 context, int simulations = DEFAULT_SIMULATIONS)
        {
            if (!context.AttackingUnits.Any() || !context.DefendingUnits.Any())
                return new VersusResultV2 { SimulationCount = simulations };

            var results = new List<SimulationRunV2>();

            for (int i = 0; i < simulations; i++)
            {
                results.Add(RunSimulation(context));
            }

            return AggregateResults(results, context, simulations);
        }

        private static SimulationRunV2 RunSimulation(VersusContextV2 context)
        {
            var run = new SimulationRunV2();

            // Clone defending units for this simulation
            var defendersState = CloneDefenders(context.DefendingUnits);

            // Get ordered attacking weapons
            var attackSequence = GetAttackSequence(context);

            foreach (var (unit, model, weapon) in attackSequence)
            {
                if (!defendersState.Any(u => u.Models.Any(m => !m.IsDestroyed)))
                    break; // All defenders destroyed

                var weaponResult = ResolveWeaponFire(unit, model, weapon, defendersState, context);
                run.WeaponResults.Add(weaponResult);
                run.TotalDamage += weaponResult.TotalDamage;
            }

            // Count models killed
            foreach (var defender in defendersState)
            {
                run.ModelsKilled += defender.Models.Count(m => m.IsDestroyed);
            }

            run.AllDefendersDestroyed = defendersState.All(u => u.Models.All(m => m.IsDestroyed));

            return run;
        }

        private static List<(CombatUnit unit, CombatModel model, CombatWeapon weapon)> GetAttackSequence(VersusContextV2 context)
        {
            var sequence = new List<(CombatUnit, CombatModel, CombatWeapon)>();

            foreach (var unit in context.AttackingUnits)
            {
                foreach (var model in unit.Models)
                {
                    foreach (var weapon in model.Weapons.Where(w => w.IsSelected))
                    {
                        sequence.Add((unit, model, weapon));
                    }
                }
            }

            // Apply ordering
            switch (context.FiringOrder.OrderType)
            {
                case FiringOrderType.ByUnit:
                    sequence = sequence.OrderBy(s => s.Item1.FiringPriority)
                                       .ThenBy(s => s.Item3.FiringPriority)
                                       .ToList();
                    break;
                case FiringOrderType.ByPriority:
                    sequence = sequence.OrderBy(s => s.Item3.FiringPriority)
                                       .ThenBy(s => s.Item1.FiringPriority)
                                       .ToList();
                    break;
                case FiringOrderType.Simultaneous:
                    // No ordering needed
                    break;
            }

            return sequence;
        }

        private static WeaponFireResult ResolveWeaponFire(
            CombatUnit attackingUnit,
            CombatModel attackingModel,
            CombatWeapon weapon,
            List<CombatUnit> defendersState,
            VersusContextV2 context)
        {
            var result = new WeaponFireResult
            {
                WeaponId = weapon.Id,
                WeaponName = weapon.WeaponProfile?.Name ?? "Unknown"
            };

            if (weapon.WeaponProfile == null)
                return result;

            // Step 1: Determine attacks
            result.Attacks = DetermineAttacks(weapon, attackingUnit, context);

            // Step 2: Resolve hits
            var hitResult = ResolveHits(result.Attacks, weapon, attackingUnit, attackingModel, context);
            result.Hits = hitResult.Hits;
            result.AutoWounds = hitResult.AutoWounds;

            // Step 3: Resolve wounds
            var totalWounds = ResolveWounds(hitResult.Hits - hitResult.AutoWounds, weapon, attackingUnit, attackingModel, defendersState, context) + hitResult.AutoWounds;
            result.Wounds = totalWounds;

            // Step 4: Apply wounds to defenders
            ApplyWoundsToDefenders(totalWounds, weapon, attackingUnit, defendersState, context, result);

            return result;
        }

        private static int DetermineAttacks(CombatWeapon weapon, CombatUnit unit, VersusContextV2 context)
        {
            if (weapon.WeaponProfile == null) return 0;

            var baseAttacks = ParseValue(weapon.WeaponProfile.A);

            // Apply weapon modifiers
            baseAttacks += weapon.WeaponModifiers.ExtraAttacks;

            // Apply weapon abilities
            if (weapon.ParsedAbilities.TwinLinked == 1 || weapon.WeaponModifiers.TwinLinked)
            {
                // Twin-linked: Re-roll wounds, not extra attacks
            }

            // TODO: Apply Rapid Fire, Blast based on unit size
            if (weapon.ParsedAbilities.RapidFire.HasValue)
            {
                // Could add logic for range/conditions
            }

            return Math.Max(1, baseAttacks);
        }

        private static HitResultV2 ResolveHits(
            int attacks,
            CombatWeapon weapon,
            CombatUnit unit,
            CombatModel model,
            VersusContextV2 context)
        {
            var result = new HitResultV2();
            if (weapon.WeaponProfile == null || attacks <= 0) return result;

            var bs_ws = ParseValue(weapon.WeaponProfile.BsWs);
            if (bs_ws == 0) return result;

            // Calculate total hit modifier
            var hitModifier = context.AttackerGlobalModifiers.HitModifier +
                            unit.UnitModifiers.HitModifier +
                            model.ModelModifiers.HitModifier +
                            weapon.WeaponModifiers.HitModifier;

            var targetRoll = Math.Clamp(bs_ws - hitModifier, 2, 6);

            // Check for Torrent (auto-hits)
            if (weapon.ParsedAbilities.Torrent == 1 || weapon.WeaponModifiers.Torrent)
            {
                result.Hits = attacks;
                return result;
            }

            // Resolve rerolls
            var rerollHits = unit.UnitModifiers.RerollHits || weapon.WeaponModifiers.RerollHits;
            var rerollOnes = unit.UnitModifiers.RerollOnes || weapon.WeaponModifiers.RerollOnes;

            for (int i = 0; i < attacks; i++)
            {
                var hitRoll = RollD6();
                var wasRerolled = false;

                // Apply rerolls
                if (rerollHits || (rerollOnes && hitRoll == 1))
                {
                    hitRoll = RollD6();
                    wasRerolled = true;
                }

                // Check if hit
                if (hitRoll >= targetRoll)
                {
                    result.Hits++;

                    // Check for critical hit (unmodified 6, or custom crit value)
                    var critValue = weapon.WeaponModifiers.CriticalHit;
                    var isCritical = (!wasRerolled && hitRoll >= critValue);

                    if (isCritical)
                    {
                        // Sustained Hits
                        if (weapon.WeaponModifiers.SustainedHits > 0 || weapon.ParsedAbilities.TwinLinked == 1)
                        {
                            result.Hits += weapon.WeaponModifiers.SustainedHits;
                        }

                        // Lethal Hits - converts this hit to auto-wound
                        if (weapon.WeaponModifiers.LethalHits > 0)
                        {
                            result.AutoWounds++;
                            result.Hits--; // Remove from normal hits
                        }
                    }
                }
            }

            return result;
        }

        private static int ResolveWounds(
            int hits,
            CombatWeapon weapon,
            CombatUnit unit,
            CombatModel model,
            List<CombatUnit> defenders,
            VersusContextV2 context)
        {
            if (hits <= 0 || weapon.WeaponProfile == null) return 0;

            var wounds = 0;
            var strength = ParseValue(weapon.WeaponProfile.S);

            // Get average toughness of defenders
            var avgToughness = CalculateAverageToughness(defenders, context);

            // Calculate wound target
            int woundTarget = CalculateWoundTarget(strength, avgToughness);

            // Apply modifiers
            var woundModifier = context.AttackerGlobalModifiers.WoundModifier +
                              unit.UnitModifiers.WoundModifier +
                              model.ModelModifiers.WoundModifier +
                              weapon.WeaponModifiers.WoundModifier;

            woundTarget = Math.Clamp(woundTarget - woundModifier, 2, 6);

            // Resolve rerolls
            var rerollWounds = unit.UnitModifiers.RerollWounds || weapon.WeaponModifiers.RerollWounds;
            var twinLinked = weapon.ParsedAbilities.TwinLinked == 1 || weapon.WeaponModifiers.TwinLinked;

            for (int i = 0; i < hits; i++)
            {
                var woundRoll = RollD6();
                var wasRerolled = false;

                // Twin-linked: reroll wounds
                if (twinLinked || rerollWounds)
                {
                    var rerollResult = RollD6();
                    if (rerollResult >= woundRoll)
                    {
                        woundRoll = rerollResult;
                        wasRerolled = true;
                    }
                }

                if (woundRoll >= woundTarget)
                {
                    wounds++;

                    // Check for critical wound (devastating wounds)
                    var critValue = weapon.WeaponModifiers.CriticalWound;
                    if (!wasRerolled && woundRoll >= critValue && weapon.WeaponModifiers.DevastatingWounds > 0)
                    {
                        // Mark as mortal wound (handled in damage step)
                    }
                }
            }

            return wounds;
        }

        private static void ApplyWoundsToDefenders(
            int wounds,
            CombatWeapon weapon,
            CombatUnit attackingUnit,
            List<CombatUnit> defendersState,
            VersusContextV2 context,
            WeaponFireResult result)
        {
            if (wounds <= 0 || weapon.WeaponProfile == null) return;

            // Get targets based on allocation method
            var targetModels = GetWoundTargets(defendersState, context.FiringOrder.AllocationMethod);

            foreach (var targetModel in targetModels)
            {
                if (wounds <= 0) break;

                // Resolve save for each wound
                for (int i = 0; i < wounds && !targetModel.IsDestroyed; i++)
                {
                    var damage = ResolveSingleWound(weapon, targetModel, attackingUnit, context);

                    targetModel.CurrentWounds -= damage;
                    result.TotalDamage += damage;

                    if (targetModel.CurrentWounds <= 0)
                    {
                        targetModel.CurrentWounds = 0;
                        result.ModelsKilled++;
                    }
                }
            }
        }

        private static int ResolveSingleWound(
            CombatWeapon weapon,
            CombatModel defender,
            CombatUnit attackingUnit,
            VersusContextV2 context)
        {
            if (weapon.WeaponProfile == null || defender.ModelProfile == null) return 0;

            var weaponAP = ParseValue(weapon.WeaponProfile.AP) + weapon.WeaponModifiers.APModifier;
            var defenderSave = ParseValue(defender.ModelProfile.Sv);
            var defenderInvuln = ParseValue(defender.ModelProfile.InvSv);

            // Calculate effective save
            var modifiedSave = defenderSave - weaponAP;

            // Apply cover
            if (context.DefenderGlobalModifiers.Cover && 
                !(context.AttackerGlobalModifiers.IgnoreCover || weapon.WeaponModifiers.IgnoreCover || weapon.ParsedAbilities.Ignores))
            {
                modifiedSave -= 1;
            }

            var effectiveSave = modifiedSave;

            // Apply invulnerable save
            if (!weapon.WeaponModifiers.IgnoreInvulnerable && defenderInvuln > 0)
            {
                effectiveSave = Math.Min(modifiedSave, defenderInvuln);
            }

            effectiveSave = Math.Clamp(effectiveSave, 2, 7);

            // Roll save
            var saveSucceeds = effectiveSave < 7 && RollD6() >= effectiveSave;

            if (saveSucceeds)
            {
                return 0; // No damage
            }

            // Calculate damage
            var damage = RollDamage(weapon.WeaponProfile.D) + weapon.WeaponModifiers.DamageModifier;

            // Apply damage reduction
            if (defender.ModelModifiers.DamageReduction > 0)
            {
                damage = Math.Max(1, damage - defender.ModelModifiers.DamageReduction);
            }

            if (defender.ModelModifiers.HalveDamage)
            {
                damage = Math.Max(1, damage / 2);
            }

            // Apply Feel No Pain
            if (defender.ModelModifiers.FeelNoPain)
            {
                damage = ApplyFeelNoPain(damage, defender.ModelModifiers.FeelNoPainValue);
            }

            return damage;
        }

        private static List<CombatModel> GetWoundTargets(List<CombatUnit> defenders, WoundAllocationMethod method)
        {
            var allModels = defenders.SelectMany(u => u.Models).Where(m => !m.IsDestroyed).ToList();

            return method switch
            {
                WoundAllocationMethod.TargetWeakest => allModels.OrderBy(m => m.CurrentWounds).ToList(),
                WoundAllocationMethod.TargetStrongest => allModels.OrderByDescending(m => m.CurrentWounds).ToList(),
                _ => allModels // Even distribution
            };
        }

        private static int CalculateAverageToughness(List<CombatUnit> defenders, VersusContextV2 context)
        {
            var livingModels = defenders.SelectMany(u => u.Models).Where(m => !m.IsDestroyed && m.ModelProfile != null).ToList();
            if (!livingModels.Any()) return 1;

            var avgT = (int)livingModels.Average(m => ParseValue(m.ModelProfile!.T));
            return avgT + context.DefenderGlobalModifiers.SaveModifier;
        }

        private static int CalculateWoundTarget(int strength, int toughness)
        {
            if (strength >= toughness * 2)
                return 2;
            else if (strength > toughness)
                return 3;
            else if (strength == toughness)
                return 4;
            else if (strength * 2 <= toughness)
                return 6;
            else
                return 5;
        }

        private static int ApplyFeelNoPain(int totalDamage, int fnpValue)
        {
            if (totalDamage <= 0) return 0;

            var remainingDamage = 0;
            for (int i = 0; i < totalDamage; i++)
            {
                if (RollD6() < fnpValue)
                {
                    remainingDamage++;
                }
            }

            return remainingDamage;
        }

        private static List<CombatUnit> CloneDefenders(List<CombatUnit> defenders)
        {
            var cloned = new List<CombatUnit>();

            foreach (var unit in defenders)
            {
                var clonedUnit = new CombatUnit
                {
                    Id = unit.Id,
                    DatasheetId = unit.DatasheetId,
                    DatasheetName = unit.DatasheetName,
                    UnitModifiers = unit.UnitModifiers,
                    FiringPriority = unit.FiringPriority
                };

                foreach (var model in unit.Models)
                {
                    clonedUnit.Models.Add(new CombatModel
                    {
                        Id = model.Id,
                        ModelProfile = model.ModelProfile,
                        CurrentWounds = model.CurrentWounds,
                        MaxWounds = model.MaxWounds,
                        ModelModifiers = model.ModelModifiers
                    });
                }

                cloned.Add(clonedUnit);
            }

            return cloned;
        }

        private static VersusResultV2 AggregateResults(List<SimulationRunV2> results, VersusContextV2 context, int simulations)
        {
            var totalDamages = results.Select(r => (int)Math.Floor(r.TotalDamage)).ToList();
            totalDamages.Sort();

            var result = new VersusResultV2
            {
                SimulationCount = simulations,
                AverageTotalDamage = results.Average(r => r.TotalDamage),
                AverageModelsKilled = results.Average(r => r.ModelsKilled),
                MedianDamage = totalDamages[simulations / 2],
                WipeoutProbability = (double)results.Count(r => r.AllDefendersDestroyed) / simulations * 100
            };

            // Calculate damage distribution
            var damageGroups = totalDamages.GroupBy(d => d).OrderBy(g => g.Key);
            foreach (var group in damageGroups)
            {
                result.DamageDistribution[group.Key] = (double)group.Count() / simulations * 100;
            }

            // Per-weapon stats
            foreach (var attackingUnit in context.AttackingUnits)
            {
                foreach (var model in attackingUnit.Models)
                {
                    foreach (var weapon in model.Weapons.Where(w => w.IsSelected))
                    {
                        var weaponResults = results.SelectMany(r => r.WeaponResults)
                                                  .Where(wr => wr.WeaponId == weapon.Id)
                                                  .ToList();

                        if (weaponResults.Any())
                        {
                            result.WeaponStats[weapon.Id] = new WeaponCombatStats
                            {
                                WeaponId = weapon.Id,
                                WeaponName = weapon.WeaponProfile?.Name ?? "Unknown",
                                AverageHits = weaponResults.Average(wr => wr.Hits),
                                AverageWounds = weaponResults.Average(wr => wr.Wounds),
                                AverageDamage = weaponResults.Average(wr => wr.TotalDamage)
                            };
                        }
                    }
                }
            }

            // Per-unit stats
            foreach (var unit in context.AttackingUnits)
            {
                var unitWeaponIds = unit.Models.SelectMany(m => m.Weapons.Where(w => w.IsSelected).Select(w => w.Id)).ToList();
                var unitDamage = results.Average(r => r.WeaponResults.Where(wr => unitWeaponIds.Contains(wr.WeaponId)).Sum(wr => wr.TotalDamage));

                result.UnitStats[unit.Id] = new UnitCombatStats
                {
                    UnitId = unit.Id,
                    UnitName = unit.DatasheetName,
                    AverageDamageDealt = unitDamage,
                    TotalModelsStarting = unit.Models.Count
                };
            }

            result.StageBreakdown["Total Attacks"] = results.Average(r => r.WeaponResults.Sum(wr => wr.Attacks));
            result.StageBreakdown["Total Hits"] = results.Average(r => r.WeaponResults.Sum(wr => wr.Hits));
            result.StageBreakdown["Total Wounds"] = results.Average(r => r.WeaponResults.Sum(wr => wr.Wounds));
            result.StageBreakdown["Total Damage"] = result.AverageTotalDamage;
            result.StageBreakdown["Models Killed"] = result.AverageModelsKilled;

            return result;
        }

        private static int RollD6() => _random.Next(1, 7);

        private static int RollDamage(string damageString)
        {
            if (string.IsNullOrEmpty(damageString)) return 0;

            damageString = damageString.Trim();

            if (int.TryParse(damageString, out var flatDamage))
                return flatDamage;

            if (damageString.Contains('D') || damageString.Contains('d'))
            {
                var parts = damageString.ToUpper().Split('D');

                if (parts.Length == 1)
                {
                    if (int.TryParse(parts[0], out var dice))
                        return RollDice(1, dice);
                    return RollD6();
                }
                else if (parts.Length == 2)
                {
                    var numDice = string.IsNullOrEmpty(parts[0]) ? 1 : int.Parse(parts[0]);
                    var remaining = parts[1];

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

        private class SimulationRunV2
        {
            public double TotalDamage { get; set; }
            public int ModelsKilled { get; set; }
            public bool AllDefendersDestroyed { get; set; }
            public List<WeaponFireResult> WeaponResults { get; set; } = new();
        }

        private class WeaponFireResult
        {
            public string WeaponId { get; set; } = string.Empty;
            public string WeaponName { get; set; } = string.Empty;
            public int Attacks { get; set; }
            public int Hits { get; set; }
            public int AutoWounds { get; set; }
            public int Wounds { get; set; }
            public double TotalDamage { get; set; }
            public int ModelsKilled { get; set; }
        }

        private class HitResultV2
        {
            public int Hits { get; set; }
            public int AutoWounds { get; set; }
        }
    }
}

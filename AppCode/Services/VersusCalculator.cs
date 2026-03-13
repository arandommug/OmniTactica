using OmniTactica.AppCode.Models.Core;
using System.Text.RegularExpressions;

namespace OmniTactica.AppCode.Services
{
    /// <summary>
    /// Monte Carlo combat calculator with comprehensive logging and statistics.
    /// </summary>
    public static class VersusCalculator
    {
        private static readonly Random _random = new Random();

        public static VersusResult Calculate(VersusContext context)
        {
            var settings = context.SimulationSettings;
            var simulations = new List<SimulationRun>();

            for (int i = 0; i < settings.Iterations; i++)
            {
                var run = RunSimulation(context, i == 0 && settings.EnableDetailedLogging);
                simulations.Add(run);
            }

            return AggregateResults(simulations, context);
        }

        private static SimulationRun RunSimulation(VersusContext context, bool enableLogging)
        {
            var run = new SimulationRun { Log = enableLogging ? new List<CombatLogEntry>() : null };
            var step = 0;

            // Clone defenders for this simulation
            var defenders = CloneUnits(context.DefendingUnits);

            // Process each attacking unit
            foreach (var attacker in context.AttackingUnits)
            {
                foreach (var model in attacker.Models)
                {
                    // Process each model instance
                    for (int modelInstance = 0; modelInstance < model.Quantity; modelInstance++)
                    {
                        foreach (var weapon in model.Weapons.Where(w => w.IsSelected))
                        {
                            if (!defenders.Any(u => u.Models.Any(m => !m.IsDestroyed)))
                                break;

                            var weaponResult = ResolveWeaponAttack(
                                attacker, model, weapon,
                                defenders,
                                context,
                                run, ref step);

                            run.TotalDamage += weaponResult.Damage;
                        }
                    }
                }
            }

            // Count casualties
            foreach (var defender in defenders)
            {
                run.ModelsKilled += defender.Models.Count(m => m.IsDestroyed);
            }

            run.AllDefendersDestroyed = defenders.All(u => u.Models.All(m => m.IsDestroyed));

            return run;
        }

        private static WeaponAttackResult ResolveWeaponAttack(
            CombatUnit attackerUnit,
            CombatModel attackerModel,
            CombatWeapon weapon,
            List<CombatUnit> defenders,
            VersusContext context,
            SimulationRun run,
            ref int step)
        {
            var result = new WeaponAttackResult
            {
                WeaponId = weapon.Id,
                WeaponName = weapon.Name
            };

            // Step 1: Determine number of attacks
            result.Attacks = DetermineAttacks(weapon, attackerUnit, attackerModel, defenders, context, run, ref step);
            run.Stages.TotalAttacks += result.Attacks;

            // Step 2: Resolve hit rolls
            var hitResult = ResolveHitRolls(weapon, attackerUnit, attackerModel, result.Attacks, context, run, ref step);
            result.Hits = hitResult.Hits;
            result.CriticalHits = hitResult.CriticalHits;
            result.AutoWounds = hitResult.AutoWounds;
            run.Stages.TotalHits += result.Hits;
            run.Stages.CriticalHits += result.CriticalHits;

            // Step 3: Resolve wound rolls
            var woundResult = ResolveWoundRolls(weapon, attackerUnit, attackerModel, hitResult, defenders, context, run, ref step);
            result.Wounds = woundResult.Wounds;
            result.CriticalWounds = woundResult.CriticalWounds;
            result.MortalWounds = woundResult.MortalWounds;
            run.Stages.TotalWounds += result.Wounds;
            run.Stages.CriticalWounds += result.CriticalWounds;
            run.Stages.MortalWounds += result.MortalWounds;

            // Step 4: Allocate and resolve saves
            var damageResult = AllocateWounds(weapon, attackerUnit, woundResult, defenders, context, run, ref step);
            result.Damage = damageResult.TotalDamage;
            result.UnsavedWounds = damageResult.UnsavedWounds;
            run.Stages.FailedSaves += damageResult.FailedSaves;
            run.Stages.SuccessfulSaves += damageResult.SuccessfulSaves;
            run.Stages.InvulnerableSaves += damageResult.InvulnerableSaves;
            run.Stages.FeelNoPainSaves += damageResult.FeelNoPainSaves;
            run.Stages.TotalDamageDealt += result.Damage;
            run.Stages.TotalDamagePrevented += damageResult.DamagePrevented;

            return result;
        }

        private static int DetermineAttacks(
            CombatWeapon weapon,
            CombatUnit attackerUnit,
            CombatModel attackerModel,
            List<CombatUnit> defenders,
            VersusContext context,
            SimulationRun run,
            ref int step)
        {
            var attacks = ParseDiceValue(weapon.A);

            // Apply Rapid Fire
            if (weapon.Abilities.RapidFire.HasValue && context.SimulationSettings.RangeToTarget.HasValue)
            {
                var weaponRange = ParseRangeValue(weapon.Range);
                var isWithinHalfRange = false;

                // RangeToTarget = -1 is a special value meaning "within half range"
                if (context.SimulationSettings.RangeToTarget.Value == -1)
                {
                    isWithinHalfRange = true;
                }
                else if (context.SimulationSettings.RangeToTarget.Value <= weaponRange / 2)
                {
                    isWithinHalfRange = true;
                }

                if (isWithinHalfRange)
                {
                    attacks += weapon.Abilities.RapidFire.Value;
                    Log(run, step++, "Attacks", attackerUnit.DatasheetName, attackerModel.Name, weapon.Name, "", "",
                        $"Rapid Fire: Added {weapon.Abilities.RapidFire.Value} attacks (within half range)",
                        new Dictionary<string, object> { ["attacks"] = attacks });
                }
            }

            // Apply Blast
            if (weapon.Abilities.Blast)
            {
                var totalDefenders = defenders.Sum(u => u.Models.Sum(m => m.Quantity));
                if (totalDefenders >= 10)
                {
                    attacks += 2;
                    Log(run, step++, "Attacks", attackerUnit.DatasheetName, attackerModel.Name, weapon.Name, "", "",
                        $"Blast: Added 2 attacks (10+ models)",
                        new Dictionary<string, object> { ["attacks"] = attacks });
                }
                else if (totalDefenders >= 5)
                {
                    attacks++;
                    Log(run, step++, "Attacks", attackerUnit.DatasheetName, attackerModel.Name, weapon.Name, "", "",
                        $"Blast: Added 1 attack (5+ models)",
                        new Dictionary<string, object> { ["attacks"] = attacks });
                }
            }

            // Apply modifiers from conditional modifiers
            foreach (var modifier in weapon.Modifiers.Where(m => m.IsActive))
            {
                if (EvaluateCondition(modifier.Condition, context, attackerUnit, weapon, defenders) &&
                    modifier.Effect.Type == EffectType.AddAttacks && modifier.Effect.IntValue.HasValue)
                {
                    attacks += modifier.Effect.IntValue.Value;
                    Log(run, step++, "Attacks", attackerUnit.DatasheetName, attackerModel.Name, weapon.Name, "", "",
                        $"{modifier.Name}: Added {modifier.Effect.IntValue.Value} attacks",
                        new Dictionary<string, object> { ["attacks"] = attacks });
                }
            }

            Log(run, step++, "Attacks", attackerUnit.DatasheetName, attackerModel.Name, weapon.Name, "", "",
                $"Total attacks: {attacks}",
                new Dictionary<string, object> { ["attacks"] = attacks });

            return Math.Max(1, attacks);
        }

        private static HitRollResult ResolveHitRolls(
            CombatWeapon weapon,
            CombatUnit attackerUnit,
            CombatModel attackerModel,
            int attacks,
            VersusContext context,
            SimulationRun run,
            ref int step)
        {
            var result = new HitRollResult();

            // Torrent auto-hits
            if (weapon.Abilities.Torrent)
            {
                result.Hits = attacks;
                Log(run, step++, "Hit", attackerUnit.DatasheetName, attackerModel.Name, weapon.Name, "", "",
                    $"Torrent: All {attacks} attacks auto-hit",
                    new Dictionary<string, object> { ["hits"] = attacks });
                return result;
            }

            var bsWs = weapon.BsWs;
            var hitModifier = context.AttackerGlobalModifiers.HitModifier;

            // Apply modifiers
            foreach (var modifier in weapon.Modifiers.Where(m => m.IsActive))
            {
                if (EvaluateCondition(modifier.Condition, context, attackerUnit, weapon, null) &&
                    modifier.Effect.Type == EffectType.AddHitModifier && modifier.Effect.IntValue.HasValue)
                {
                    hitModifier += modifier.Effect.IntValue.Value;
                }
            }

            var targetRoll = Math.Clamp(bsWs - hitModifier, 2, 6);

            // Check for reroll abilities
            var rerollAll = weapon.Modifiers.Any(m => m.IsActive && EvaluateCondition(m.Condition, context, attackerUnit, weapon, null) && m.Effect.Type == EffectType.RerollHits);
            var rerollOnes = weapon.Modifiers.Any(m => m.IsActive && EvaluateCondition(m.Condition, context, attackerUnit, weapon, null) && m.Effect.Type == EffectType.RerollOnes);

            for (int i = 0; i < attacks; i++)
            {
                var roll = RollD6();
                var unmodifiedRoll = roll; // Track unmodified roll for critical checks

                if ((rerollAll) || (rerollOnes && roll == 1))
                {
                    roll = RollD6();
                    unmodifiedRoll = roll; // After reroll, the reroll result is the "unmodified" value
                }

                if (roll >= targetRoll)
                {
                    result.Hits++;

                    // Check for critical hit (unmodified 6, rerolls count as unmodified)
                    if (unmodifiedRoll == 6)
                    {
                        result.CriticalHits++;

                        // Sustained Hits
                        if (weapon.Abilities.SustainedHits.HasValue)
                        {
                            result.Hits += weapon.Abilities.SustainedHits.Value;
                            Log(run, step++, "Hit", attackerUnit.DatasheetName, attackerModel.Name, weapon.Name, "", "",
                                $"Critical Hit: Sustained Hits added {weapon.Abilities.SustainedHits.Value} extra hits",
                                new Dictionary<string, object> { ["roll"] = unmodifiedRoll, ["extra_hits"] = weapon.Abilities.SustainedHits.Value });
                        }

                        // Lethal Hits
                        if (weapon.Abilities.LethalHits)
                        {
                            result.AutoWounds++;
                            result.Hits--;
                            Log(run, step++, "Hit", attackerUnit.DatasheetName, attackerModel.Name, weapon.Name, "", "",
                                "Critical Hit: Lethal Hits - auto-wound",
                                new Dictionary<string, object> { ["roll"] = unmodifiedRoll });
                        }
                    }
                }
            }

            Log(run, step++, "Hit", attackerUnit.DatasheetName, attackerModel.Name, weapon.Name, "", "",
                $"Hit rolls complete: {result.Hits} hits, {result.CriticalHits} critical hits, {result.AutoWounds} auto-wounds",
                new Dictionary<string, object> { ["hits"] = result.Hits, ["crits"] = result.CriticalHits, ["auto_wounds"] = result.AutoWounds });

            return result;
        }

        private static WoundRollResult ResolveWoundRolls(
            CombatWeapon weapon,
            CombatUnit attackerUnit,
            CombatModel attackerModel,
            HitRollResult hitResult,
            List<CombatUnit> defenders,
            VersusContext context,
            SimulationRun run,
            ref int step)
        {
            var result = new WoundRollResult { MortalWounds = hitResult.AutoWounds };

            var totalHits = hitResult.Hits;
            if (totalHits <= 0) return result;

            var strength = weapon.S;
            var averageToughness = CalculateAverageToughness(defenders);
            var woundTarget = CalculateWoundTarget(strength, averageToughness);

            var woundModifier = context.AttackerGlobalModifiers.WoundModifier;

            // Apply modifiers
            foreach (var modifier in weapon.Modifiers.Where(m => m.IsActive))
            {
                if (EvaluateCondition(modifier.Condition, context, attackerUnit, weapon, defenders) &&
                    modifier.Effect.Type == EffectType.AddWoundModifier && modifier.Effect.IntValue.HasValue)
                {
                    woundModifier += modifier.Effect.IntValue.Value;
                }
            }

            woundTarget = Math.Clamp(woundTarget - woundModifier, 2, 6);

            // Check for CriticalWoundOn modifiers (e.g., Anti-Infantry 3+)
            var criticalWoundThreshold = 6;
            foreach (var modifier in weapon.Modifiers.Where(m => m.IsActive))
            {
                if (EvaluateCondition(modifier.Condition, context, attackerUnit, weapon, defenders) &&
                    modifier.Effect.Type == EffectType.CriticalWoundOn && modifier.Effect.IntValue.HasValue)
                {
                    criticalWoundThreshold = Math.Min(criticalWoundThreshold, modifier.Effect.IntValue.Value);
                    Log(run, step++, "Wound", attackerUnit.DatasheetName, attackerModel.Name, weapon.Name, "", "",
                        $"{modifier.Name}: Critical wounds on {modifier.Effect.IntValue.Value}+",
                        new Dictionary<string, object> { ["crit_threshold"] = modifier.Effect.IntValue.Value });
                }
            }

            // Check for rerolls
            var rerollAll = weapon.Abilities.TwinLinked || weapon.Modifiers.Any(m => m.IsActive && EvaluateCondition(m.Condition, context, attackerUnit, weapon, defenders) && m.Effect.Type == EffectType.RerollWounds);
            var rerollOnes = weapon.Modifiers.Any(m => m.IsActive && EvaluateCondition(m.Condition, context, attackerUnit, weapon, defenders) && m.Effect.Type == EffectType.RerollOnes);

            for (int i = 0; i < totalHits; i++)
            {
                var roll = RollD6();
                var unmodifiedRoll = roll; // Track unmodified roll for critical checks

                if (rerollAll || (rerollOnes && roll == 1))
                {
                    var reroll = RollD6();
                    if (reroll >= roll || rerollAll)
                    {
                        roll = reroll;
                        unmodifiedRoll = reroll; // After reroll, the reroll result is the "unmodified" value
                    }
                }

                // Check for critical wound first (using unmodified roll)
                // Critical wounds always succeed, even if they wouldn't normally wound
                bool isCriticalWound = unmodifiedRoll >= criticalWoundThreshold;

                if (isCriticalWound || roll >= woundTarget)
                {
                    result.Wounds++;

                    if (isCriticalWound)
                    {
                        result.CriticalWounds++;

                        // Devastating Wounds
                        if (weapon.Abilities.DevastatingWounds)
                        {
                            result.MortalWounds++;
                            result.Wounds--;
                            Log(run, step++, "Wound", attackerUnit.DatasheetName, attackerModel.Name, weapon.Name, "", "",
                                "Critical Wound: Devastating Wounds - converted to mortal wound",
                                new Dictionary<string, object> { ["roll"] = unmodifiedRoll });
                        }
                    }
                }
            }

            Log(run, step++, "Wound", attackerUnit.DatasheetName, attackerModel.Name, weapon.Name, "", "",
                $"Wound rolls complete: {result.Wounds} wounds, {result.CriticalWounds} critical wounds, {result.MortalWounds} mortal wounds",
                new Dictionary<string, object> { ["wounds"] = result.Wounds, ["crits"] = result.CriticalWounds, ["mortal"] = result.MortalWounds });

            return result;
        }

        private static DamageResult AllocateWounds(
            CombatWeapon weapon,
            CombatUnit attackerUnit,
            WoundRollResult woundResult,
            List<CombatUnit> defenders,
            VersusContext context,
            SimulationRun run,
            ref int step)
        {
            var result = new DamageResult();
            var totalWounds = woundResult.Wounds + woundResult.MortalWounds;

            if (totalWounds <= 0) return result;

            // Get target models based on allocation method
            var targetModels = GetWoundAllocationTargets(defenders, context.SimulationSettings.WoundAllocation);

            foreach (var targetModel in targetModels)
            {
                if (totalWounds <= 0) break;
                if (targetModel.IsDestroyed) continue;

                // Allocate one wound at a time
                while (totalWounds > 0 && !targetModel.IsDestroyed)
                {
                    var isMortal = woundResult.MortalWounds > 0;
                    if (isMortal) woundResult.MortalWounds--;
                    else woundResult.Wounds--;

                    var damage = 0;

                    if (isMortal)
                    {
                        // Mortal wounds bypass saves
                        damage = RollDamage(weapon.D);
                        result.UnsavedWounds++;
                        Log(run, step++, "Save", attackerUnit.DatasheetName, "", weapon.Name,
                            defenders.First(u => u.Models.Contains(targetModel)).DatasheetName, targetModel.Name,
                            $"Mortal wound bypasses saves: {damage} damage",
                            new Dictionary<string, object> { ["damage"] = damage });
                    }
                    else
                    {
                        // Regular wound - resolve save
                        var saveResult = ResolveSave(weapon, targetModel, context, run, ref step, attackerUnit.DatasheetName);

                        if (saveResult.Saved)
                        {
                            result.SuccessfulSaves++;
                            if (saveResult.UsedInvulnerable) result.InvulnerableSaves++;
                        }
                        else
                        {
                            result.FailedSaves++;
                            damage = RollDamage(weapon.D);
                            result.UnsavedWounds++;

                            // Apply damage modifiers
                            foreach (var modifier in targetModel.Modifiers.Where(m => m.IsActive))
                            {
                                if (modifier.Effect.Type == EffectType.ReduceDamage && modifier.Effect.IntValue.HasValue)
                                {
                                    var reduction = modifier.Effect.IntValue.Value;
                                    damage = Math.Max(1, damage - reduction);
                                    result.DamagePrevented += reduction;
                                }
                                else if (modifier.Effect.Type == EffectType.HalveDamage)
                                {
                                    var halved = damage / 2;
                                    result.DamagePrevented += damage - Math.Max(1, halved);
                                    damage = Math.Max(1, halved);
                                }
                            }

                            // Apply Feel No Pain
                            if (targetModel.Modifiers.Any(m => m.IsActive && m.Effect.Type == EffectType.FeelNoPain))
                            {
                                var fnpRoll = RollD6();
                                var fnpValue = targetModel.Modifiers.First(m => m.IsActive && m.Effect.Type == EffectType.FeelNoPain).Effect.IntValue ?? 5;
                                if (fnpRoll >= fnpValue)
                                {
                                    result.FeelNoPainSaves++;
                                    result.DamagePrevented += damage;
                                    damage = 0;
                                    Log(run, step++, "FNP", attackerUnit.DatasheetName, "", weapon.Name,
                                        defenders.First(u => u.Models.Contains(targetModel)).DatasheetName, targetModel.Name,
                                        $"Feel No Pain passed (rolled {fnpRoll})",
                                        new Dictionary<string, object> { ["roll"] = fnpRoll, ["needed"] = fnpValue });
                                }
                            }
                        }
                    }

                    // Apply damage
                    if (damage > 0)
                    {
                        targetModel.CurrentWounds -= damage;
                        result.TotalDamage += damage;

                        var defenderUnit = defenders.First(u => u.Models.Contains(targetModel));
                        Log(run, step++, "Damage", attackerUnit.DatasheetName, "", weapon.Name,
                            defenderUnit.DatasheetName, targetModel.Name,
                            $"Dealt {damage} damage ({targetModel.CurrentWounds}/{targetModel.MaxWounds} wounds remaining)",
                            new Dictionary<string, object> { ["damage"] = damage, ["remaining"] = targetModel.CurrentWounds });

                        if (targetModel.IsDestroyed)
                        {
                            Log(run, step++, "Destroyed", attackerUnit.DatasheetName, "", weapon.Name,
                                defenderUnit.DatasheetName, targetModel.Name,
                                "Model destroyed!",
                                new Dictionary<string, object>());
                        }
                    }

                    totalWounds--;
                }
            }

            return result;
        }

        private static SaveResult ResolveSave(
            CombatWeapon weapon,
            CombatModel defender,
            VersusContext context,
            SimulationRun run,
            ref int step,
            string attackerName)
        {
            var result = new SaveResult();

            var ap = weapon.AP;
            var armorSave = defender.Sv;
            var invulnSave = defender.InvSv;

            // Apply cover
            if (context.DefenderGlobalModifiers.Cover && !weapon.Abilities.IgnoresCover && !context.AttackerGlobalModifiers.IgnoreCover)
            {
                armorSave -= 1;
            }

            var modifiedArmorSave = Math.Clamp(armorSave - ap, 2, 7);

            // Choose best save
            var effectiveSave = modifiedArmorSave;
            if (invulnSave > 0 && invulnSave < modifiedArmorSave)
            {
                effectiveSave = invulnSave;
                result.UsedInvulnerable = true;
            }

            if (effectiveSave >= 7)
            {
                result.Saved = false;
                return result;
            }

            var saveRoll = RollD6();
            result.Saved = saveRoll >= effectiveSave;

            Log(run, step++, "Save", attackerName, "", weapon.Name, "", defender.Name,
                $"Save roll: {saveRoll} vs {effectiveSave}+ ({(result.Saved ? "Passed" : "Failed")}, {(result.UsedInvulnerable ? "Invuln" : "Armor")})",
                new Dictionary<string, object> { ["roll"] = saveRoll, ["needed"] = effectiveSave, ["passed"] = result.Saved });

            return result;
        }

        // Helper methods

        private static List<CombatModel> GetWoundAllocationTargets(List<CombatUnit> defenders, WoundAllocationMethod method)
        {
            var allModels = new List<CombatModel>();
            foreach (var unit in defenders)
            {
                foreach (var model in unit.Models)
                {
                    for (int i = 0; i < model.Quantity; i++)
                    {
                        if (!model.IsDestroyed)
                            allModels.Add(model);
                    }
                }
            }

            return method switch
            {
                WoundAllocationMethod.TargetWeakest => allModels.OrderBy(m => m.CurrentWounds).ToList(),
                WoundAllocationMethod.TargetStrongest => allModels.OrderByDescending(m => m.CurrentWounds).ToList(),
                WoundAllocationMethod.RandomAllocation => allModels.OrderBy(_ => _random.Next()).ToList(),
                _ => allModels
            };
        }

        private static int CalculateAverageToughness(List<CombatUnit> defenders)
        {
            var livingModels = new List<CombatModel>();
            foreach (var unit in defenders)
            {
                foreach (var model in unit.Models)
                {
                    if (!model.IsDestroyed)
                    {
                        for (int i = 0; i < model.Quantity; i++)
                            livingModels.Add(model);
                    }
                }
            }

            if (!livingModels.Any()) return 1;
            return (int)Math.Round(livingModels.Average(m => m.T));
        }

        private static int CalculateWoundTarget(int strength, int toughness)
        {
            if (strength >= toughness * 2) return 2;
            if (strength > toughness) return 3;
            if (strength == toughness) return 4;
            if (strength * 2 <= toughness) return 6;
            return 5;
        }

        private static bool EvaluateCondition(ModifierCondition condition, VersusContext context, CombatUnit attacker, CombatWeapon weapon, List<CombatUnit>? defenders)
        {
            return condition.Type switch
            {
                ConditionType.Always => true,
                ConditionType.UnitCharged => context.SimulationSettings.AttackerCharged,
                ConditionType.TargetWithinHalfRange => IsWithinHalfRange(context, weapon),
                ConditionType.TargetUnitSize5Plus => defenders != null && defenders.Sum(u => u.Models.Sum(m => m.Quantity)) >= 5,
                ConditionType.TargetUnitSize10Plus => defenders != null && defenders.Sum(u => u.Models.Sum(m => m.Quantity)) >= 10,
                ConditionType.TargetHasKeyword => defenders != null && !string.IsNullOrEmpty(condition.Value) && defenders.Any(u => u.DatasheetDetail?.Keywords.Any(k => k.Equals(condition.Value, StringComparison.OrdinalIgnoreCase)) == true),
                ConditionType.TargetIsInfantry => defenders != null && defenders.Any(u => u.DatasheetDetail?.Keywords.Any(k => k.Equals("Infantry", StringComparison.OrdinalIgnoreCase)) == true),
                ConditionType.TargetIsVehicle => defenders != null && defenders.Any(u => u.DatasheetDetail?.Keywords.Any(k => k.Equals("Vehicle", StringComparison.OrdinalIgnoreCase)) == true),
                ConditionType.TargetIsMonster => defenders != null && defenders.Any(u => u.DatasheetDetail?.Keywords.Any(k => k.Equals("Monster", StringComparison.OrdinalIgnoreCase)) == true),
                _ => false
            };
        }

        private static bool IsWithinHalfRange(VersusContext context, CombatWeapon weapon)
        {
            if (!context.SimulationSettings.RangeToTarget.HasValue)
                return false;

            // -1 is special value for "within half range" quick toggle
            if (context.SimulationSettings.RangeToTarget.Value == -1)
                return true;

            var weaponRange = ParseRangeValue(weapon.Range);
            return context.SimulationSettings.RangeToTarget.Value <= weaponRange / 2;
        }

        private static List<CombatUnit> CloneUnits(List<CombatUnit> units)
        {
            var cloned = new List<CombatUnit>();

            foreach (var unit in units)
            {
                var clonedUnit = new CombatUnit
                {
                    Id = unit.Id,
                    DatasheetId = unit.DatasheetId,
                    DatasheetName = unit.DatasheetName,
                    FactionId = unit.FactionId,
                    DatasheetDetail = unit.DatasheetDetail // Preserve DatasheetDetail for keyword checks
                };

                foreach (var model in unit.Models)
                {
                    clonedUnit.Models.Add(new CombatModel
                    {
                        Id = model.Id,
                        Name = model.Name,
                        Quantity = model.Quantity,
                        M = model.M,
                        T = model.T,
                        Sv = model.Sv,
                        InvSv = model.InvSv,
                        W = model.W,
                        Ld = model.Ld,
                        OC = model.OC,
                        CurrentWounds = model.CurrentWounds,
                        MaxWounds = model.MaxWounds,
                        Modifiers = model.Modifiers.ToList()
                    });
                }

                cloned.Add(clonedUnit);
            }

            return cloned;
        }

        private static VersusResult AggregateResults(List<SimulationRun> simulations, VersusContext context)
        {
            var damages = simulations.Select(s => (int)s.TotalDamage).OrderBy(d => d).ToList();

            var result = new VersusResult
            {
                SimulationCount = simulations.Count,
                AverageDamage = simulations.Average(s => s.TotalDamage),
                MedianDamage = damages[damages.Count / 2],
                MinDamage = damages.First(),
                MaxDamage = damages.Last(),
                StandardDeviation = CalculateStandardDeviation(simulations.Select(s => s.TotalDamage).ToList()),
                AverageModelsKilled = simulations.Average(s => s.ModelsKilled),
                WipeoutProbability = simulations.Count(s => s.AllDefendersDestroyed) * 100.0 / simulations.Count
            };

            // Damage distribution
            var damageGroups = damages.GroupBy(d => d);
            foreach (var group in damageGroups)
            {
                result.DamageDistribution[group.Key] = group.Count() * 100.0 / simulations.Count;
            }

            // Stage statistics
            result.Stages = new StageStatistics
            {
                TotalAttacks = simulations.Average(s => s.Stages.TotalAttacks),
                TotalHits = simulations.Average(s => s.Stages.TotalHits),
                CriticalHits = simulations.Average(s => s.Stages.CriticalHits),
                TotalWounds = simulations.Average(s => s.Stages.TotalWounds),
                CriticalWounds = simulations.Average(s => s.Stages.CriticalWounds),
                MortalWounds = simulations.Average(s => s.Stages.MortalWounds),
                FailedSaves = simulations.Average(s => s.Stages.FailedSaves),
                SuccessfulSaves = simulations.Average(s => s.Stages.SuccessfulSaves),
                InvulnerableSaves = simulations.Average(s => s.Stages.InvulnerableSaves),
                FeelNoPainSaves = simulations.Average(s => s.Stages.FeelNoPainSaves),
                TotalDamageDealt = simulations.Average(s => s.Stages.TotalDamageDealt),
                TotalDamagePrevented = simulations.Average(s => s.Stages.TotalDamagePrevented)
            };

            // Sample log from first simulation
            if (simulations.Any() && simulations[0].Log != null)
            {
                result.SampleCombatLog = simulations[0].Log;
            }

            return result;
        }

        private static double CalculateStandardDeviation(List<double> values)
        {
            var avg = values.Average();
            var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumOfSquares / values.Count);
        }

        private static int RollD6() => _random.Next(1, 7);

        private static int RollDamage(string damageString)
        {
            if (string.IsNullOrEmpty(damageString)) return 0;

            damageString = damageString.Trim();

            if (int.TryParse(damageString, out var flatDamage))
                return flatDamage;

            var match = Regex.Match(damageString, @"(\d*)D(\d+)(?:\+(\d+))?", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var numDice = string.IsNullOrEmpty(match.Groups[1].Value) ? 1 : int.Parse(match.Groups[1].Value);
                var diceSize = int.Parse(match.Groups[2].Value);
                var modifier = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

                var total = 0;
                for (int i = 0; i < numDice; i++)
                {
                    total += _random.Next(1, diceSize + 1);
                }
                return total + modifier;
            }

            return 1;
        }

        private static int ParseDiceValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;

            value = value.Trim();

            if (int.TryParse(value, out var result))
                return result;

            var match = Regex.Match(value, @"(\d*)D(\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var numDice = string.IsNullOrEmpty(match.Groups[1].Value) ? 1 : int.Parse(match.Groups[1].Value);
                var diceSize = int.Parse(match.Groups[2].Value);

                var avg = numDice * (diceSize + 1) / 2;
                return avg;
            }

            return 0;
        }

        private static int ParseRangeValue(string range)
        {
            if (string.IsNullOrEmpty(range) || range.Equals("Melee", StringComparison.OrdinalIgnoreCase))
                return 0;

            range = range.Replace("\"", "").Replace("'", "").Trim();

            if (int.TryParse(range, out var result))
                return result;

            return 0;
        }

        private static void Log(SimulationRun run, int step, string phase, string attackerUnit, string attackerModel,
            string weapon, string defenderUnit, string defenderModel, string message, Dictionary<string, object> details)
        {
            if (run.Log == null) return;

            run.Log.Add(new CombatLogEntry
            {
                Step = step,
                Phase = phase,
                AttackerUnit = attackerUnit,
                AttackerModel = attackerModel,
                WeaponName = weapon,
                DefenderUnit = defenderUnit,
                DefenderModel = defenderModel,
                Message = message,
                Details = details
            });
        }

        // Internal classes for simulation

        private class SimulationRun
        {
            public double TotalDamage { get; set; }
            public int ModelsKilled { get; set; }
            public bool AllDefendersDestroyed { get; set; }
            public StageStatistics Stages { get; set; } = new();
            public List<CombatLogEntry>? Log { get; set; }
        }

        private class WeaponAttackResult
        {
            public string WeaponId { get; set; } = string.Empty;
            public string WeaponName { get; set; } = string.Empty;
            public int Attacks { get; set; }
            public int Hits { get; set; }
            public int CriticalHits { get; set; }
            public int AutoWounds { get; set; }
            public int Wounds { get; set; }
            public int CriticalWounds { get; set; }
            public int MortalWounds { get; set; }
            public int UnsavedWounds { get; set; }
            public double Damage { get; set; }
        }

        private class HitRollResult
        {
            public int Hits { get; set; }
            public int CriticalHits { get; set; }
            public int AutoWounds { get; set; }
        }

        private class WoundRollResult
        {
            public int Wounds { get; set; }
            public int CriticalWounds { get; set; }
            public int MortalWounds { get; set; }
        }

        private class DamageResult
        {
            public double TotalDamage { get; set; }
            public int UnsavedWounds { get; set; }
            public int FailedSaves { get; set; }
            public int SuccessfulSaves { get; set; }
            public int InvulnerableSaves { get; set; }
            public int FeelNoPainSaves { get; set; }
            public double DamagePrevented { get; set; }
        }

        private class SaveResult
        {
            public bool Saved { get; set; }
            public bool UsedInvulnerable { get; set; }
        }
    }
}

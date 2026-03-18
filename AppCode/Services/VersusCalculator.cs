using Microsoft.Extensions.ObjectPool;
using OmniTactica.AppCode.Models.Core;
using System.Text;
using System.Text.RegularExpressions;

namespace OmniTactica.AppCode.Services
{
    /// <summary>
    /// Monte Carlo combat calculator with comprehensive logging and statistics.
    /// </summary>
    public static class VersusCalculator
    {
        public static VersusResult Calculate(VersusContext context)
        {
            var settings = context.SimulationSettings;
            int iterations = settings.Iterations;

            var results = new SimulationRun[iterations]; 
            var attackInstances = BuildAttackInstances(context);

            Parallel.For(0, iterations, i =>
            {
                results[i] = RunSimulation(context, attackInstances, i == 0 && settings.EnableDetailedLogging);
            });

            return AggregateResults(results.ToList(), context);
        }

        private static SimulationRun RunSimulation(VersusContext context, List<AttackInstance> attackInstances, bool enableLogging)
        {
            var run = new SimulationRun { Log = enableLogging ? GetLog() : null };
            var step = 0;

            // Clone defenders for this simulation
            var defenders = CloneUnits(context.DefendingUnits);

            int defenderModelCount = 0;

            foreach (var unit in defenders)
                defenderModelCount += unit.Models.Count;

            var phaseMode = context.SimulationSettings.CombatPhase;
            var runShooting = phaseMode == CombatPhaseMode.ShootingOnly || phaseMode == CombatPhaseMode.Both;
            var runFight = phaseMode == CombatPhaseMode.FightOnly || phaseMode == CombatPhaseMode.Both;

            // Shooting Phase
            if (runShooting)
            {
                if (enableLogging && phaseMode == CombatPhaseMode.Both)
                {
                    LogPhaseHeader(run, ref step, "SHOOTING PHASE");
                }

                ProcessWeaponsForPhase(context, defenders, run, ref step, enableLogging, true, attackInstances, defenderModelCount);
            }

            // Fight Phase
            if (runFight)
            {
                if (enableLogging && phaseMode == CombatPhaseMode.Both)
                {
                    LogPhaseHeader(run, ref step, "FIGHT PHASE");
                }

                ProcessWeaponsForPhase(context, defenders, run, ref step, enableLogging, false, attackInstances, defenderModelCount);
            }

            // Count casualties
            foreach (var defender in defenders)
            {
                run.ModelsKilled += defender.Models.Count(m => m.IsDestroyed);
            }

            run.AllDefendersDestroyed = defenders.All(u => u.Models.All(m => m.IsDestroyed));

            return run;
        }

        private static void LogPhaseHeader(SimulationRun run, ref int step, string phaseName)
        {
            var log = run.Log;
            if (log == null) return;

            log.Add(new CombatLogEntry { Step = step++, Phase = "PhaseHeader", Message = "", Details = new Dictionary<string, object>() });
            log.Add(new CombatLogEntry { Step = step++, Phase = "PhaseHeader", Message = $"╔═══════════════════════════════════════════╗", Details = new Dictionary<string, object>() });
            log.Add(new CombatLogEntry { Step = step++, Phase = "PhaseHeader", Message = $"║  {phaseName}", Details = new Dictionary<string, object>() });
            log.Add(new CombatLogEntry { Step = step++, Phase = "PhaseHeader", Message = $"╚═══════════════════════════════════════════╝", Details = new Dictionary<string, object>() });
            log.Add(new CombatLogEntry { Step = step++, Phase = "PhaseHeader", Message = "", Details = new Dictionary<string, object>() });
        }

        private static void ProcessWeaponsForPhase(
            VersusContext context,
            List<CombatUnit> defenders,
            SimulationRun run,
            ref int step,
            bool enableLogging,
            bool isRanged,
            List<AttackInstance> attackInstances,
            int defenderModelCount)
        {
            foreach (var attack in attackInstances)
            {
                var weapon = attack.Weapon;

                if (IsMeleeWeapon(weapon) == isRanged)
                    continue;

                if (!HasLivingModels(defenders))
                    return;

                var result = ResolveWeaponAttack(
                    attack.Unit,
                    attack.Model,
                    weapon,
                    defenders,
                    context,
                    run,
                    ref step,
                    defenderModelCount,
                    attack.Instance);

                run.TotalDamage += result.Damage;
            }
        }

        private static bool HasLivingModels(List<CombatUnit> defenders)
        {
            foreach (var u in defenders)
                foreach (var m in u.Models)
                    if (!m.IsDestroyed)
                        return true;

            return false;
        }

        private static WeaponAttackResult ResolveWeaponAttack(
            CombatUnit attackerUnit,
            CombatModel attackerModel,
            CombatWeapon weapon,
            List<CombatUnit> defenders,
            VersusContext context,
            SimulationRun run,
            ref int step,
            int defenderModelCount,
            int modelInstance = 1)
        {
            var result = new WeaponAttackResult
            {
                WeaponId = weapon.Id,
                WeaponName = weapon.Name
            };

            // Create model display name with instance number if multiple weapons
            var modelDisplayName = weapon.Quantity > 1 
                ? $"{attackerModel.Name} #{modelInstance}"
                : attackerModel.Name;

            // Create attack sequence log for battle report format
            AttackSequenceLog? attackLog = null;

            if (run.Log != null)
            {
                attackLog = new AttackSequenceLog
                {
                    AttackerName = modelDisplayName,
                    WeaponName = weapon.Name,
                    IsMelee = IsMeleeWeapon(weapon)
                };
            }

            var modifierCache = BuildModifierCache(context, attackerUnit, attackerModel, weapon, defenders);

            // Collect weapon abilities
            CollectWeaponAbilities(weapon, attackLog);

            // Collect active modifiers
            CollectActiveModifiers(modifierCache.ActiveAttackerModifiers, attackLog);

            // Step 1: Determine number of attacks
            result.Attacks = DetermineAttacks(weapon, context, attackLog, defenderModelCount, modifierCache.ActiveAttackerModifiers);
            attackLog?.Attacks = result.Attacks;
            run.Stages.TotalAttacks += result.Attacks;

            // Step 2: Resolve hit rolls
            var hitResult = ResolveHitRolls(weapon, context, result.Attacks, attackLog, modifierCache);
            result.Hits = hitResult.Hits;
            result.CriticalHits = hitResult.CriticalHits;
            result.AutoWounds = hitResult.AutoWounds;
            attackLog?.Hits = hitResult.Hits;
            attackLog?.CriticalHits = hitResult.CriticalHits;
            attackLog?.AutoWounds = hitResult.AutoWounds;
            attackLog?.Misses = hitResult.Misses;
            run.Stages.TotalHits += result.Hits;
            run.Stages.CriticalHits += result.CriticalHits;

            // Step 3: Resolve wound rolls
            var woundResult = ResolveWoundRolls(weapon, defenders, context, hitResult, attackLog, modifierCache);
            result.Wounds = woundResult.Wounds;
            result.CriticalWounds = woundResult.CriticalWounds;
            result.MortalWounds = woundResult.MortalWounds + woundResult.DevastatingMortalWounds;
            attackLog?.Wounds = woundResult.Wounds;
            attackLog?.CriticalWounds = woundResult.CriticalWounds;
            attackLog?.MortalWounds = result.MortalWounds;
            attackLog?.FailedToWound = woundResult.FailedWounds;
            run.Stages.TotalWounds += result.Wounds;
            run.Stages.CriticalWounds += result.CriticalWounds;
            run.Stages.MortalWounds += result.MortalWounds;

            // Step 4: Allocate and resolve saves
            var damageResult = AllocateWounds(weapon, attackerUnit, attackerModel, woundResult, defenders, context, run, ref step, modelDisplayName, attackLog, modifierCache.ActiveAttackerModifiers);
            result.Damage = damageResult.TotalDamage;
            result.UnsavedWounds = damageResult.UnsavedWounds;
            attackLog?.TotalDamage = (int)damageResult.TotalDamage;
            run.Stages.FailedSaves += damageResult.FailedSaves;
            run.Stages.SuccessfulSaves += damageResult.SuccessfulSaves;
            run.Stages.InvulnerableSaves += damageResult.InvulnerableSaves;
            run.Stages.FeelNoPainSaves += damageResult.FeelNoPainSaves;
            run.Stages.TotalDamageDealt += result.Damage;
            run.Stages.TotalDamagePrevented += damageResult.DamagePrevented;

            // Output formatted attack block
            if (run.Log != null)
            {
                LogFormattedAttackBlock(attackLog, run, ref step);
            }

            return result;
        }

        /// <summary>
        /// Outputs a formatted attack block in battle report style.
        /// </summary>
        private static void LogFormattedAttackBlock(AttackSequenceLog? attackLog, SimulationRun run, ref int step)
        {
            var log = run.Log;
            if (log == null) return;

            // Header
            log.Add(new CombatLogEntry
            {
                Step = step++,
                Phase = "AttackHeader",
                Message = "",
                Details = new Dictionary<string, object>()
            });
            log.Add(new CombatLogEntry
            {
                Step = step++,
                Phase = "AttackHeader",
                Message = "═════════════════════════════════════════════",
                Details = new Dictionary<string, object>()
            });
            log.Add(new CombatLogEntry
            {
                Step = step++,
                Phase = "AttackHeader",
                Message = $"{attackLog?.AttackerName} — {attackLog?.WeaponName}",
                Details = new Dictionary<string, object>()
            });
            log.Add(new CombatLogEntry
            {
                Step = step++,
                Phase = "AttackHeader",
                Message = "═════════════════════════════════════════════",
                Details = new Dictionary<string, object>()
            });
            log.Add(new CombatLogEntry
            {
                Step = step++,
                Phase = "AttackHeader",
                Message = "",
                Details = new Dictionary<string, object>()
            });

            // Weapon Abilities and Modifiers
            if (attackLog?.WeaponAbilities.Count != 0 || attackLog.ActiveModifiers.Count != 0)
            {
                log.Add(new CombatLogEntry
                {
                    Step = step++,
                    Phase = "WeaponInfo",
                    Message = "Weapon Abilities & Active Modifiers:",
                    Details = new Dictionary<string, object>()
                });

                foreach (var ability in attackLog!.WeaponAbilities)
                {
                    log.Add(new CombatLogEntry
                    {
                        Step = step++,
                        Phase = "WeaponInfo",
                        Message = $"  [Ability] {ability}",
                        Details = new Dictionary<string, object>()
                    });
                }

                foreach (var modifier in attackLog.ActiveModifiers)
                {
                    log.Add(new CombatLogEntry
                    {
                        Step = step++,
                        Phase = "WeaponInfo",
                        Message = $"  [Modifier] {modifier}",
                        Details = new Dictionary<string, object>()
                    });
                }

                log.Add(new CombatLogEntry { Step = step++, Phase = "WeaponInfo", Message = "", Details = new Dictionary<string, object>() });
            }

            // Attacks
            log.Add(new CombatLogEntry
            {
                Step = step++,
                Phase = "Attacks",
                Message = $"Total attacks: {attackLog.Attacks}",
                Details = new Dictionary<string, object> { ["attacks"] = attackLog.Attacks }
            });

            foreach (var effect in attackLog.AttackEffects)
            {
                log.Add(new CombatLogEntry
                {
                    Step = step++,
                    Phase = "Attacks",
                    Message = $"  {effect}",
                    Details = new Dictionary<string, object>()
                });
            }

            log.Add(new CombatLogEntry { Step = step++, Phase = "Attacks", Message = "", Details = new Dictionary<string, object>() });

            // Hit Roll
            log.Add(new CombatLogEntry { Step = step++, Phase = "Hit", Message = "Hit Roll", Details = new Dictionary<string, object>() });
            log.Add(new CombatLogEntry { Step = step++, Phase = "Hit", Message = $"  ✓ Hits: {attackLog.Hits}", Details = new Dictionary<string, object>() });
            log.Add(new CombatLogEntry { Step = step++, Phase = "Hit", Message = $"  ⚡ Critical Hits: {attackLog.CriticalHits}", Details = new Dictionary<string, object>() });
            log.Add(new CombatLogEntry { Step = step++, Phase = "Hit", Message = $"  ✗ Misses: {attackLog.Misses}", Details = new Dictionary<string, object>() });

            if (attackLog.AutoWounds > 0)
            {
                log.Add(new CombatLogEntry { Step = step++, Phase = "Hit", Message = $"  💀 Auto-Wounds: {attackLog.AutoWounds}", Details = new Dictionary<string, object>() });
            }

            foreach (var effect in attackLog.HitEffects)
            {
                log.Add(new CombatLogEntry { Step = step++, Phase = "Hit", Message = $"  Effect: {effect}", Details = new Dictionary<string, object>() });
            }
            if (attackLog.HitDice.Count != 0)
            {
                log.Add(new CombatLogEntry { Step = step++, Phase = "Hit", Message = $"  [DICE] {string.Join(",", attackLog.HitDice)}", Details = new Dictionary<string, object>() });
            }
            log.Add(new CombatLogEntry { Step = step++, Phase = "Hit", Message = "", Details = new Dictionary<string, object>() });

            // Wound Roll (only if there were hits)
            if (attackLog.Hits + attackLog.CriticalHits > 0 || attackLog.AutoWounds > 0)
            {
                log.Add(new CombatLogEntry { Step = step++, Phase = "Wound", Message = "Wound Roll", Details = new Dictionary<string, object>() });
                log.Add(new CombatLogEntry { Step = step++, Phase = "Wound", Message = $"  ✓ Wounds: {attackLog.Wounds}", Details = new Dictionary<string, object>() });
                log.Add(new CombatLogEntry { Step = step++, Phase = "Wound", Message = $"  ⚡ Critical Wounds: {attackLog.CriticalWounds}", Details = new Dictionary<string, object>() });
                log.Add(new CombatLogEntry { Step = step++, Phase = "Wound", Message = $"  ✗ Failed: {attackLog.FailedToWound}", Details = new Dictionary<string, object>() });

                if (attackLog.MortalWounds > 0)
                {
                    log.Add(new CombatLogEntry { Step = step++, Phase = "Wound", Message = $"  💀 Mortal Wounds: {attackLog.MortalWounds}", Details = new Dictionary<string, object>() });
                }

                foreach (var effect in attackLog.WoundEffects)
                {
                    log.Add(new CombatLogEntry { Step = step++, Phase = "Wound", Message = $"  Effect: {effect}", Details = new Dictionary<string, object>() });
                }
                if (attackLog.WoundDice.Count != 0)
                {
                    log.Add(new CombatLogEntry { Step = step++, Phase = "Wound", Message = $"  [DICE] {string.Join(",", attackLog.WoundDice)}", Details = new Dictionary<string, object>() });
                }
                log.Add(new CombatLogEntry { Step = step++, Phase = "Wound", Message = "", Details = new Dictionary<string, object>() });
            }

            // Save Rolls (if there were wounds)
            if ((attackLog.Wounds + attackLog.CriticalWounds + attackLog.MortalWounds) > 0 && attackLog.SaveAttempts.Count > 0)
            {
                foreach (var effect in attackLog.SaveEffects)
                {
                    log.Add(new CombatLogEntry { Step = step++, Phase = "Save", Message = $"  Effect: {effect}", Details = new Dictionary<string, object>() });
                }

                if (attackLog.SaveEffects.Count > 0)
                {
                    log.Add(new CombatLogEntry { Step = step++, Phase = "Save", Message = "", Details = new Dictionary<string, object>() });
                }

                var groupedSaves = attackLog.SaveAttempts.GroupBy(s => s.DefenderName);
                foreach (var defenderSaves in groupedSaves)
                {
                    log.Add(new CombatLogEntry { Step = step++, Phase = "Save", Message = $"Save Roll — {defenderSaves.Key}", Details = new Dictionary<string, object>() });

                    foreach (var save in defenderSaves)
                    {
                        if (save.IsMortalWound)
                        {
                            log.Add(new CombatLogEntry { Step = step++, Phase = "Save", Message = "  💀 Mortal wound bypasses saves", Details = new Dictionary<string, object>() });
                        }
                        else
                        {
                            var result = save.Passed ? "🛡 Save Passed" : "✗ Save Failed";
                            log.Add(new CombatLogEntry { Step = step++, Phase = "Save", Message = $"  Roll: {save.Roll} vs {save.Target}+", Details = new Dictionary<string, object>() });
                            log.Add(new CombatLogEntry { Step = step++, Phase = "Save", Message = $"  {result} ({save.SaveType})", Details = new Dictionary<string, object>() });
                            if (!string.IsNullOrWhiteSpace(save.Summary))
                            {
                                log.Add(new CombatLogEntry { Step = step++, Phase = "Save", Message = $"  ↳ {save.Summary}", Details = new Dictionary<string, object>() });
                            }
                        }
                    }
                    log.Add(new CombatLogEntry { Step = step++, Phase = "Save", Message = "", Details = new Dictionary<string, object>() });
                }
                if (attackLog.SaveDice.Count != 0)
                {
                    log.Add(new CombatLogEntry { Step = step++, Phase = "Save", Message = $"  [DICE] {string.Join(",", attackLog.SaveDice)}", Details = new Dictionary<string, object>() });
                }
            }

            // Damage Events
            if (attackLog.DamageEvents.Count > 0)
            {
                log.Add(new CombatLogEntry { Step = step++, Phase = "Damage", Message = "Damage", Details = new Dictionary<string, object>() });
                foreach (var damageEvent in attackLog.DamageEvents)
                {
                    log.Add(new CombatLogEntry { Step = step++, Phase = "Damage", Message = $"  {damageEvent}", Details = new Dictionary<string, object>() });
                }
                if (attackLog.DamageDice.Count > 0)
                {
                    log.Add(new CombatLogEntry { Step = step++, Phase = "Damage", Message = $"  [DICE] {string.Join(",", attackLog.DamageDice)}", Details = new Dictionary<string, object>() });
                }
                log.Add(new CombatLogEntry { Step = step++, Phase = "Damage", Message = "", Details = new Dictionary<string, object>() });
            }

            // Result Summary
            log.Add(new CombatLogEntry { Step = step++, Phase = "Result", Message = "Result", Details = new Dictionary<string, object>() });

            if (attackLog.TotalDamage == 0)
            {
                if ((attackLog.Wounds + attackLog.CriticalWounds + attackLog.MortalWounds) == 0)
                {
                    log.Add(new CombatLogEntry { Step = step++, Phase = "Result", Message = "  No wounds inflicted", Details = new Dictionary<string, object>() });
                }
                else
                {
                    log.Add(new CombatLogEntry { Step = step++, Phase = "Result", Message = "  All wounds saved", Details = new Dictionary<string, object>() });
                }
            }
            else
            {
                var destroyedText = attackLog.TargetDestroyed ? " ☠️ Target destroyed" : "";
                var overkillNote = attackLog.RawDamage > attackLog.TotalDamage ? $" ({attackLog.RawDamage} rolled, {attackLog.RawDamage - attackLog.TotalDamage} overkill)" : "";
                log.Add(new CombatLogEntry { Step = step++, Phase = "Result", Message = $"  💥 {attackLog.TotalDamage} damage inflicted{overkillNote}{destroyedText}", Details = new Dictionary<string, object>() });
            }

            log.Add(new CombatLogEntry { Step = step++, Phase = "Result", Message = "", Details = new Dictionary<string, object>() });
        }

        private static int DetermineAttacks(
            CombatWeapon weapon,
            VersusContext context,
            AttackSequenceLog? attackLog,
            int defenderModelCount,
            IReadOnlyList<ConditionalModifier> activeAttackerModifiers)
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
                    attackLog?.AttackEffects.Add($"Rapid Fire: Added {weapon.Abilities.RapidFire.Value} attacks (within half range)");
                }
            }

            // Apply Blast
            if (weapon.Abilities.Blast)
            {
                var totalDefenders = defenderModelCount;
                var blastBonus = totalDefenders / 5;
                if (blastBonus > 0)
                {
                    attacks += blastBonus;
                    attackLog?.AttackEffects.Add($"Blast: Added {blastBonus} attacks ({totalDefenders} models in target unit)");
                }
            }

            // Apply modifiers from conditional modifiers
            foreach (var modifier in activeAttackerModifiers)
            {
                if (IsBuiltInRapidFireModifierHandledByWeaponAbility(weapon, modifier))
                    continue;

                if (modifier.Effect.Type == EffectType.AddAttacks &&
                    (modifier.Effect.IntValue.HasValue || !string.IsNullOrWhiteSpace(modifier.Effect.StringValue)))
                {
                    var extraAttacks = RollEffectValue(modifier.Effect, 1);
                    attacks += extraAttacks;
                    attackLog?.AttackEffects.Add($"{modifier.Name}: Added {extraAttacks} attacks");
                }
            }

            return Math.Max(1, attacks);
        }

        private static HitRollResult ResolveHitRolls(
            CombatWeapon weapon,
            VersusContext context,
            int attacks,
            AttackSequenceLog? attackLog,
            AttackModifierCache modifierCache)
        {
            var result = new HitRollResult();

            // Torrent auto-hits
            if (weapon.Abilities.Torrent)
            {
                result.Hits = attacks;
                attackLog?.HitEffects.Add("Torrent: All attacks auto-hit");
                return result;
            }

            var bsWs = weapon.BsWs;
            var hitModifier = context.AttackerGlobalModifiers.HitModifier;

            var skillImprovement = 0;
            foreach (var modifier in modifierCache.ActiveAttackerModifiers)
            {
                var affectsSkill = IsMeleeWeapon(weapon)
                    ? modifier.Effect.Type == EffectType.ImproveWeaponSkill
                    : modifier.Effect.Type == EffectType.ImproveBallisticSkill;

                if (affectsSkill && (modifier.Effect.IntValue.HasValue || !string.IsNullOrWhiteSpace(modifier.Effect.StringValue)))
                {
                    skillImprovement += RollEffectValue(modifier.Effect, 0);
                }
            }

            if (skillImprovement != 0)
            {
                var originalSkill = bsWs;
                bsWs = Math.Clamp(bsWs - skillImprovement, 2, 6);
                attackLog?.HitEffects.Add($"{(IsMeleeWeapon(weapon) ? "Weapon" : "Ballistic")} Skill improved: {originalSkill}+ → {bsWs}+");
            }

            foreach (var modifier in modifierCache.ActiveAttackerModifiers)
            {
                if (modifier.Effect.Type == EffectType.AddHitModifier &&
                    (modifier.Effect.IntValue.HasValue || !string.IsNullOrWhiteSpace(modifier.Effect.StringValue)))
                {
                    hitModifier += RollEffectValue(modifier.Effect, 0);
                }
            }

            foreach (var modifier in modifierCache.ActiveDefenderBattlefieldModifiers)
            {
                if (modifier.Effect.Type == EffectType.AddHitModifier &&
                    (modifier.Effect.IntValue.HasValue || !string.IsNullOrWhiteSpace(modifier.Effect.StringValue)))
                {
                    hitModifier += RollEffectValue(modifier.Effect, 0);
                }
            }

            var criticalHitThreshold = 6;
            foreach (var modifier in modifierCache.ActiveAttackerModifiers)
            {
                if (modifier.Effect.Type == EffectType.CriticalHitOn &&
                    (modifier.Effect.IntValue.HasValue || !string.IsNullOrWhiteSpace(modifier.Effect.StringValue)))
                {
                    criticalHitThreshold = Math.Min(criticalHitThreshold, RollEffectValue(modifier.Effect, 6));
                }
            }

            hitModifier = Math.Clamp(hitModifier, -1, 1);
            var targetRoll = Math.Clamp(bsWs - hitModifier, 2, 6);

            // Check for reroll abilities
            var rerollAll = modifierCache.ActiveAttackerModifiers.Any(m => m.Effect.Type == EffectType.RerollHits);
            var rerollOnes = modifierCache.ActiveAttackerModifiers.Any(m => m.Effect.Type == EffectType.RerollOnes);

            for (int i = 0; i < attacks; i++)
            {
                var roll = RollD6();
                var unmodifiedRoll = roll; // Track unmodified roll for critical checks

                var shouldReroll = (rerollAll && roll < targetRoll) || (rerollOnes && roll == 1);
                if (shouldReroll)
                {
                    var originalRoll = roll;
                    roll = RollD6();
                    unmodifiedRoll = roll; // After reroll, the reroll result is the "unmodified" value
                    attackLog?.HitEffects.Add($"Rerolled hit: {originalRoll} → {roll}");
                }

                attackLog?.HitDice.Add(roll);

                if (roll >= targetRoll)
                {
                    result.Hits++;

                    // Check for critical hit (unmodified 6, rerolls count as unmodified)
                    if (unmodifiedRoll >= criticalHitThreshold)
                    {
                        result.CriticalHits++;

                        // Sustained Hits from weapon ability
                        if (weapon.Abilities.SustainedHits.HasValue)
                        {
                            result.Hits += weapon.Abilities.SustainedHits.Value;
                            attackLog?.HitEffects.Add($"Sustained Hits generated +{weapon.Abilities.SustainedHits.Value} additional hits");
                        }

                        // Sustained Hits / AddExtraHitsOnCrit from conditional modifiers
                        foreach (var modifier in modifierCache.CriticalHitTriggeredModifiers)
                        {
                            if (IsBuiltInCriticalHitModifierHandledByWeaponAbility(weapon, modifier))
                                continue;

                            if (modifier.Effect.Type == EffectType.AddExtraHitsOnCrit &&
                                (modifier.Effect.IntValue.HasValue || !string.IsNullOrWhiteSpace(modifier.Effect.StringValue)))
                            {
                                var extraHits = RollEffectValue(modifier.Effect, 1);
                                result.Hits += extraHits;
                                attackLog?.HitEffects.Add($"{modifier.Name}: +{extraHits} extra hits on critical hit");
                            }
                        }

                        // Lethal Hits
                        var autoWoundsOnCrit = weapon.Abilities.LethalHits ||
                            modifierCache.CriticalHitTriggeredModifiers
                                .Any(m => !IsBuiltInCriticalHitModifierHandledByWeaponAbility(weapon, m) && m.Effect.Type == EffectType.AutoWoundOnCrit);

                        if (autoWoundsOnCrit)
                        {
                            result.AutoWounds++;
                            result.Hits--;
                            attackLog?.HitEffects.Add("Critical hit converted to auto-wound");
                        }
                    }
                }
                else
                {
                    result.Misses++;
                }
            }

            return result;
        }

        private static WoundRollResult ResolveWoundRolls(
            CombatWeapon weapon,
            List<CombatUnit> defenders,
            VersusContext context,
            HitRollResult hitResult,
            AttackSequenceLog? attackLog,
            AttackModifierCache modifierCache)
        {
            var result = new WoundRollResult { Wounds = hitResult.AutoWounds };

            var totalHits = hitResult.Hits;
            if (totalHits <= 0) return result;

            var strength = weapon.S;
            var majorityToughness = CalculateMajorityToughness(defenders); 
            var woundTarget = CalculateWoundTarget(weapon.S, majorityToughness);

            var woundModifier = context.AttackerGlobalModifiers.WoundModifier;

            foreach (var modifier in modifierCache.ActiveAttackerModifiers)
            {
                if (modifier.Effect.Type == EffectType.AddWoundModifier &&
                    (modifier.Effect.IntValue.HasValue || !string.IsNullOrWhiteSpace(modifier.Effect.StringValue)))
                {
                    woundModifier += RollEffectValue(modifier.Effect, 0);
                }
            }

            foreach (var modifier in modifierCache.ActiveDefenderBattlefieldModifiers)
            {
                if (modifier.Effect.Type == EffectType.AddWoundModifier &&
                    (modifier.Effect.IntValue.HasValue || !string.IsNullOrWhiteSpace(modifier.Effect.StringValue)))
                {
                    woundModifier += RollEffectValue(modifier.Effect, 0);
                }
            }

            woundModifier = Math.Clamp(woundModifier, -1, 1);
            woundTarget = Math.Clamp(woundTarget - woundModifier, 2, 6);

            // Check for CriticalWoundOn modifiers (e.g., Anti-Infantry 3+)
            var criticalWoundThreshold = 6;
            foreach (var modifier in modifierCache.ActiveAttackerModifiers)
            {
                if (modifier.Effect.Type == EffectType.CriticalWoundOn &&
                    (modifier.Effect.IntValue.HasValue || !string.IsNullOrWhiteSpace(modifier.Effect.StringValue)))
                {
                    var threshold = RollEffectValue(modifier.Effect, 6);
                    criticalWoundThreshold = Math.Min(criticalWoundThreshold, threshold);
                    attackLog?.WoundEffects.Add($"{modifier.Name}: Critical wounds on {threshold}+");
                }
            }

            // Check for rerolls
            var rerollAll = weapon.Abilities.TwinLinked || modifierCache.ActiveAttackerModifiers.Any(m => m.Effect.Type == EffectType.RerollWounds);
            var rerollOnes = modifierCache.ActiveAttackerModifiers.Any(m => m.Effect.Type == EffectType.RerollOnes);

            for (int i = 0; i < totalHits; i++)
            {
                var roll = RollD6();
                var unmodifiedRoll = roll; // Track unmodified roll for critical checks

                var succeededBeforeReroll = unmodifiedRoll >= criticalWoundThreshold || roll >= woundTarget;
                var shouldReroll = (rerollAll && !succeededBeforeReroll) || (rerollOnes && roll == 1);
                if (shouldReroll)
                {
                    var originalRoll = roll;
                    var reroll = RollD6();
                    roll = reroll;
                    unmodifiedRoll = reroll; // After reroll, the reroll result is the "unmodified" value
                    attackLog?.WoundEffects.Add($"Rerolled wound: {originalRoll} → {reroll}");
                }

                attackLog?.WoundDice.Add(roll);

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
                        var convertsToMortalWounds = weapon.Abilities.DevastatingWounds ||
                            modifierCache.CriticalWoundTriggeredModifiers
                                .Any(m => !IsBuiltInCriticalWoundModifierHandledByWeaponAbility(weapon, m) && m.Effect.Type == EffectType.ConvertToMortalWounds);

                        if (convertsToMortalWounds)
                        {
                            result.DevastatingMortalWounds++;
                            result.Wounds--;
                            attackLog?.WoundEffects.Add("Critical wound converted to mortal wound");
                        }
                    }
                }
                else
                {
                    result.FailedWounds++;
                }
            }

            return result;
        }

        private static DamageResult AllocateWounds(
            CombatWeapon weapon,
            CombatUnit attackerUnit,
            CombatModel attackerModel,
            WoundRollResult woundResult,
            List<CombatUnit> defenders,
            VersusContext context,
            SimulationRun run,
            ref int step,
            string modelDisplayName,
            AttackSequenceLog? attackLog,
            IReadOnlyList<ConditionalModifier> activeAttackerModifiers)
        {
            var result = new DamageResult();
            var totalWounds = woundResult.Wounds + woundResult.MortalWounds + woundResult.DevastatingMortalWounds;

            if (totalWounds <= 0) return result;

            // Get target models based on allocation method
            var targetModels = GetWoundAllocationTargets(defenders, context.SimulationSettings.WoundAllocation);
            var defenderModifierCache = new Dictionary<string, IReadOnlyList<ConditionalModifier>>(targetModels.Count);

            for (var targetIndex = 0; targetIndex < targetModels.Count; targetIndex++)
            {
                var targetModel = targetModels[targetIndex];
                if (totalWounds <= 0) break;
                if (targetModel.IsDestroyed) continue;

                var defenderUnit = targetModel.ParentUnit!;
                if (!defenderModifierCache.TryGetValue(targetModel.Id, out var activeDefenderModifiers))
                {
                    activeDefenderModifiers = GetActiveDefenderModifiers(context, attackerUnit, weapon, defenderUnit, targetModel, defenders).ToList();
                    defenderModifierCache[targetModel.Id] = activeDefenderModifiers;
                }

                // Allocate one wound at a time
                while (totalWounds > 0 && !targetModel.IsDestroyed)
                {
                    var isDevastatingMortal = woundResult.DevastatingMortalWounds > 0;
                    var isMortal = isDevastatingMortal || woundResult.MortalWounds > 0;
                    if (isDevastatingMortal) woundResult.DevastatingMortalWounds--;
                    else if (woundResult.MortalWounds > 0) woundResult.MortalWounds--;
                    else woundResult.Wounds--;

                    var damage = 0;

                    if (isMortal)
                    {
                        // Mortal wounds bypass saves
                        damage = RollWeaponDamage(weapon, activeAttackerModifiers, attackLog);
                        result.UnsavedWounds++;

                        // Apply weapon damage bonus modifiers (e.g. Melta)
                        foreach (var modifier in activeAttackerModifiers)
                        {
                            if (modifier.Effect.Type == EffectType.AddDamageModifier &&
                                (modifier.Effect.IntValue.HasValue || !string.IsNullOrWhiteSpace(modifier.Effect.StringValue)))
                            {
                                var bonusDamage = RollEffectValue(modifier.Effect, 0);
                                damage += bonusDamage;
                                if (bonusDamage != 0)
                                {
                                    attackLog?.DamageEvents.Add($"{modifier.Name}: +{bonusDamage} damage");
                                }
                            }
                        }

                        attackLog?.SaveAttempts.Add(new SaveAttempt
                        {
                            DefenderName = $"{targetModel.Name} ({defenderUnit.DatasheetName})",
                            IsMortalWound = true
                        });
                    }
                    else
                    {
                        // Regular wound - resolve save
                        var saveResult = ResolveSave(weapon, attackerUnit, attackerModel, targetModel, defenderUnit, defenders, context, run, ref step, attackerUnit.DatasheetName, attackLog, activeAttackerModifiers, activeDefenderModifiers);

                        attackLog?.SaveAttempts.Add(new SaveAttempt
                        {
                            DefenderName = $"{targetModel.Name} ({defenderUnit.DatasheetName})",
                            Roll = saveResult.Roll,
                            Target = saveResult.Target,
                            Passed = saveResult.Saved,
                            SaveType = saveResult.UsedInvulnerable ? "Invuln" : "Armor",
                            Summary = saveResult.Summary,
                            IsMortalWound = false
                        });

                        if (saveResult.Roll > 0)
                            attackLog?.SaveDice.Add(saveResult.Roll);

                        if (saveResult.Saved)
                        {
                            result.SuccessfulSaves++;
                            if (saveResult.UsedInvulnerable) result.InvulnerableSaves++;
                        }
                        else
                        {
                            result.FailedSaves++;
                            damage = RollWeaponDamage(weapon, activeAttackerModifiers, attackLog);
                            result.UnsavedWounds++;

                            // Apply weapon damage bonus modifiers (e.g. Melta)
                            foreach (var modifier in activeAttackerModifiers)
                            {
                                if (modifier.Effect.Type == EffectType.AddDamageModifier &&
                                    (modifier.Effect.IntValue.HasValue || !string.IsNullOrWhiteSpace(modifier.Effect.StringValue)))
                                {
                                    var bonusDamage = RollEffectValue(modifier.Effect, 0);
                                    damage += bonusDamage;
                                    if (bonusDamage != 0)
                                    {
                                        attackLog?.DamageEvents.Add($"{modifier.Name}: +{bonusDamage} damage");
                                    }
                                }
                            }

                            // Apply damage modifiers
                            foreach (var modifier in activeDefenderModifiers)
                            {
                                if (modifier.Effect.Type == EffectType.ReduceDamage &&
                                    (modifier.Effect.IntValue.HasValue || !string.IsNullOrWhiteSpace(modifier.Effect.StringValue)))
                                {
                                    var originalDamage = damage;
                                    var reduction = RollEffectValue(modifier.Effect, 0);
                                    damage = Math.Max(1, damage - reduction);
                                    var prevented = originalDamage - damage;
                                    result.DamagePrevented += prevented;
                                    if (prevented > 0)
                                    {
                                        attackLog?.DamageEvents.Add($"{modifier.Name}: reduced damage {originalDamage} → {damage} on {targetModel.Name} ({defenderUnit.DatasheetName})");
                                    }
                                }
                                else if (modifier.Effect.Type == EffectType.HalveDamage)
                                {
                                    var originalDamage = damage;
                                    var halved = damage / 2;
                                    var reducedDamage = Math.Max(1, halved);
                                    result.DamagePrevented += originalDamage - reducedDamage;
                                    if (originalDamage != reducedDamage)
                                    {
                                        attackLog?.DamageEvents.Add($"{modifier.Name}: reduced damage {originalDamage} → {reducedDamage} on {targetModel.Name} ({defenderUnit.DatasheetName})");
                                    }
                                    damage = reducedDamage;
                                }
                            }
                        }
                    }

                    damage = ApplyFeelNoPain(context, attackerUnit, weapon, defenderUnit, targetModel, defenders, damage, result, attackLog, activeDefenderModifiers);

                    // Apply damage
                    if (damage > 0)
                    {
                        if (isMortal && !isDevastatingMortal)
                        {
                            var originalDamage = damage;
                            var remainingDamage = damage;
                            var currentIndex = targetIndex;

                            while (remainingDamage > 0 && currentIndex < targetModels.Count)
                            {
                                var spillTarget = targetModels[currentIndex];
                                if (spillTarget.IsDestroyed)
                                {
                                    currentIndex++;
                                    continue;
                                }

                                var spillUnit = spillTarget.ParentUnit!;
                                var appliedDamage = Math.Min(remainingDamage, spillTarget.CurrentWounds);
                                spillTarget.CurrentWounds = Math.Max(0, spillTarget.CurrentWounds - appliedDamage);
                                result.TotalDamage += appliedDamage;
                                remainingDamage -= appliedDamage;

                                if (attackLog != null)
                                {
                                    attackLog.DamageEvents.Add($"{appliedDamage} mortal damage to {spillTarget.Name} ({spillUnit.DatasheetName}) - {spillTarget.CurrentWounds}/{spillTarget.MaxWounds} remaining");
                                }

                                if (spillTarget.IsDestroyed)
                                {
                                    attackLog?.DamageEvents.Add($"Mortal wounds spill over from {spillTarget.Name} to the next model");
                                    attackLog?.TargetDestroyed = true;
                                    currentIndex++;
                                }
                            }

                            if (attackLog != null)
                            {
                                attackLog.RawDamage += originalDamage;
                                if (remainingDamage > 0)
                                {
                                    attackLog.DamageEvents.Add($"{remainingDamage} mortal damage lost (no remaining models)");
                                }
                            }
                        }
                        else
                        {
                            // Cap effective damage to remaining wounds (excess damage is overkill and lost)
                            var effectiveDamage = Math.Min(damage, targetModel.CurrentWounds);
                            targetModel.CurrentWounds = Math.Max(0, targetModel.CurrentWounds - damage);
                            result.TotalDamage += effectiveDamage;

                            if (attackLog != null)
                            {
                                attackLog.RawDamage += damage;
                                var cappedNote = damage > effectiveDamage ? $" ({effectiveDamage} effective, {damage - effectiveDamage} overkill)" : "";
                                var damageLabel = isDevastatingMortal ? "mortal damage" : "damage";
                                attackLog.DamageEvents.Add($"{damage} {damageLabel} to {targetModel.Name} ({defenderUnit.DatasheetName}){cappedNote} - {targetModel.CurrentWounds}/{targetModel.MaxWounds} remaining");
                            }

                            if (targetModel.IsDestroyed)
                            {
                                attackLog?.TargetDestroyed = true;
                            }
                        }
                    }

                    totalWounds--;
                }
            }

            return result;
        }

        private static SaveResult ResolveSave(
            CombatWeapon weapon,
            CombatUnit attackerUnit,
            CombatModel attackerModel,
            CombatModel defender,
            CombatUnit defenderUnit,
            List<CombatUnit> defenders,
            VersusContext context,
            SimulationRun run,
            ref int step,
            string attackerName,
            AttackSequenceLog? attackLog,
            IReadOnlyList<ConditionalModifier> activeAttackerModifiers,
            IReadOnlyList<ConditionalModifier> activeDefenderModifiers)
        {
            var result = new SaveResult();

            var originalAp = weapon.AP;
            var ap = weapon.AP;
            var originalArmorSave = defender.Sv;
            var armorSave = defender.Sv;
            var invulnSave = defender.InvSv;

            foreach (var modifier in activeAttackerModifiers)
            {
                if (modifier.Effect.Type == EffectType.AddAPModifier &&
                    (modifier.Effect.IntValue.HasValue || !string.IsNullOrWhiteSpace(modifier.Effect.StringValue)))
                {
                    ap -= RollEffectValue(modifier.Effect, 0);
                }
            }

            // Apply cover
            var ignoresCover = weapon.Abilities.IgnoresCover || context.AttackerGlobalModifiers.IgnoreCover ||
                activeAttackerModifiers.Any(m => m.Effect.Type == EffectType.IgnoreCover);

            var isRangedAttack = !IsMeleeWeapon(weapon);
            var coverEligible = isRangedAttack && !(originalArmorSave <= 3 && ap >= 0);

            if (context.DefenderGlobalModifiers.Cover && !ignoresCover && coverEligible)
            {
                armorSave -= 1;
            }

            if (context.DefenderGlobalModifiers.Cover)
            {
                if (ignoresCover)
                {
                    AddUniqueLogMessage(attackLog?.SaveEffects, "Cover ignored");
                }
                else if (!isRangedAttack)
                {
                    AddUniqueLogMessage(attackLog?.SaveEffects, "Cover does not apply in melee");
                }
                else if (!coverEligible)
                {
                    AddUniqueLogMessage(attackLog?.SaveEffects, "Cover does not apply to 3+/2+ armor against AP 0");
                }
                else
                {
                    AddUniqueLogMessage(attackLog?.SaveEffects, $"Cover applied: armor {originalArmorSave}+ → {armorSave}+");
                }
            }

            var modifiedArmorSave = Math.Clamp(armorSave - ap, 2, 7);

            if (ap != originalAp)
            {
                AddUniqueLogMessage(attackLog?.SaveEffects, $"AP modified: {FormatAp(originalAp)} → {FormatAp(ap)}");
            }

            var ignoreInvulnerable = activeAttackerModifiers.Any(m => m.Effect.Type == EffectType.IgnoreInvulnerable);

            if (ignoreInvulnerable && invulnSave > 0)
            {
                AddUniqueLogMessage(attackLog?.SaveEffects, $"Invulnerable save ignored ({invulnSave}+)");
            }

            // Choose best save
            var effectiveSave = modifiedArmorSave;
            if (!ignoreInvulnerable && invulnSave > 0 && invulnSave < modifiedArmorSave)
            {
                effectiveSave = invulnSave;
                result.UsedInvulnerable = true;
                AddUniqueLogMessage(attackLog?.SaveEffects, $"Using invulnerable save: {invulnSave}+");
            }
            else
            {
                AddUniqueLogMessage(attackLog?.SaveEffects, $"Using armor save: {modifiedArmorSave}+");
            }

            var saveModifier = context.DefenderGlobalModifiers.SaveModifier;
            foreach (var modifier in activeDefenderModifiers)
            {
                if (modifier.Effect.Type == EffectType.AddSaveModifier &&
                    (modifier.Effect.IntValue.HasValue || !string.IsNullOrWhiteSpace(modifier.Effect.StringValue)))
                {
                    saveModifier += RollEffectValue(modifier.Effect, 0);
                }
            }

            var saveBeforeModifier = effectiveSave;
            effectiveSave = Math.Clamp(effectiveSave - saveModifier, 2, 7);

            if (saveModifier != 0)
            {
                AddUniqueLogMessage(attackLog?.SaveEffects, $"Save modifier {FormatSigned(saveModifier)}: {saveBeforeModifier}+ → {effectiveSave}+");
            }

            result.Summary = result.UsedInvulnerable
                ? $"Invuln {effectiveSave}+ ({FormatAp(ap)} AP)"
                : $"Armor {effectiveSave}+ ({FormatAp(ap)} AP)";

            if (effectiveSave >= 7)
            {
                result.Saved = false;
                result.Roll = 0;
                result.Target = effectiveSave;
                return result;
            }

            var saveRoll = RollD6();
            result.Saved = saveRoll >= effectiveSave;
            result.Roll = saveRoll;
            result.Target = effectiveSave;

            return result;
        }

        // Helper methods

        private static List<CombatModel> GetWoundAllocationTargets(List<CombatUnit> defenders, WoundAllocationMethod method)
        {
            //var allModels = defenders
            //    .SelectMany(u => u.Models)
            //    .Where(m => !m.IsDestroyed)
            //    .ToList();

            var allModels = new List<CombatModel>();

            foreach (var u in defenders)
                foreach (var m in u.Models)
                    if (!m.IsDestroyed)
                        allModels.Add(m);

            switch (method)   
            {
                case WoundAllocationMethod.TargetWeakest:
                    return allModels.OrderBy(m => m.CurrentWounds).ToList();

                case WoundAllocationMethod.TargetStrongest:
                    return allModels.OrderByDescending(m => m.CurrentWounds).ToList();

                case WoundAllocationMethod.RandomAllocation:
                    for (int i = allModels.Count - 1; i > 0; i--)
                    {
                        int j = _rng.Value!.Next(i + 1);
                        (allModels[i], allModels[j]) = (allModels[j], allModels[i]);
                    }
                    return allModels;
                default:
                    return allModels;
            }
        }

        private static int CalculateMajorityToughness(List<CombatUnit> defenders)
        {
            var counts = new Dictionary<int, int>();

            foreach (var unit in defenders)
            {
                foreach (var model in unit.Models)
                {
                    if (model.IsDestroyed)
                        continue;

                    counts.TryAdd(model.T, 0);
                    counts[model.T]++;
                }
            }

            if (counts.Count == 0)
                return 1;

            return counts.OrderByDescending(x => x.Value).First().Key;
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
                ConditionType.TargetWithinEngagementRange => IsWithinEngagementRange(context, weapon),
                ConditionType.UnitRemainedStationary => context.SimulationSettings.AttackerRemainedStationary,
                ConditionType.TargetUnitSize5Plus => defenders != null && defenders.Sum(u => u.Models.Sum(m => m.Quantity)) >= 5,
                ConditionType.TargetUnitSize10Plus => defenders != null && defenders.Sum(u => u.Models.Sum(m => m.Quantity)) >= 10,
                ConditionType.TargetHasKeyword => defenders != null && !string.IsNullOrEmpty(condition.Value) && defenders.Any(u => u.DatasheetDetail?.Keywords.Any(k => k.Equals(condition.Value, StringComparison.OrdinalIgnoreCase)) == true),
                ConditionType.TargetIsInfantry => defenders != null && defenders.Any(u => u.DatasheetDetail?.Keywords.Any(k => k.Equals("Infantry", StringComparison.OrdinalIgnoreCase)) == true),
                ConditionType.TargetIsVehicle => defenders != null && defenders.Any(u => u.DatasheetDetail?.Keywords.Any(k => k.Equals("Vehicle", StringComparison.OrdinalIgnoreCase)) == true),
                ConditionType.TargetIsMonster => defenders != null && defenders.Any(u => u.DatasheetDetail?.Keywords.Any(k => k.Equals("Monster", StringComparison.OrdinalIgnoreCase)) == true),
                ConditionType.TargetIsCharacter => defenders != null && defenders.Any(u => u.DatasheetDetail?.Keywords.Any(k => k.Equals("Character", StringComparison.OrdinalIgnoreCase)) == true),
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

        private static bool IsWithinEngagementRange(VersusContext context, CombatWeapon weapon)
        {
            if (IsMeleeWeapon(weapon))
                return true;

            return context.SimulationSettings.RangeToTarget.HasValue && context.SimulationSettings.RangeToTarget.Value <= 1;
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
                    Modifiers = unit.Modifiers.ToList(),
                    DatasheetDetail = unit.DatasheetDetail // Preserve DatasheetDetail for keyword checks
                };

                foreach (var model in unit.Models)
                {
                    // Expand models with Quantity > 1 into individual instances
                    // so each tracks wounds independently during simulation
                    for (int i = 0; i < model.Quantity; i++)
                    {
                        clonedUnit.Models.Add(new CombatModel
                        {
                            Id = $"{model.Id}_inst_{i}",
                            Name = model.Quantity > 1 ? $"{model.Name} #{i + 1}" : model.Name,
                            Quantity = 1,
                            M = model.M,
                            T = model.T,
                            Sv = model.Sv,
                            InvSv = model.InvSv,
                            W = model.W,
                            Ld = model.Ld,
                            OC = model.OC,
                            CurrentWounds = model.W,
                            MaxWounds = model.W,
                            Modifiers = model.Modifiers.ToList(),
                            ParentUnit = clonedUnit
                        });
                    }
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
            if (simulations.Count != 0)
            {
                var sampleLog = simulations[0].Log;
                if (sampleLog != null)
                {
                    result.SampleCombatLog = new List<CombatLogEntry>(sampleLog);
                }

                foreach (var sim in simulations)
                {
                    if (sim.Log != null)
                        ReturnLog(sim.Log);
                }
            }

            return result;
        }

        private static double CalculateStandardDeviation(List<double> values)
        {
            var avg = values.Average();
            var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumOfSquares / values.Count);
        }

        private static void AddUniqueLogMessage(List<string>? messages, string message)
        {
            if (messages == null || string.IsNullOrWhiteSpace(message) || messages.Contains(message))
                return;

            messages.Add(message);
        }

        private static string FormatAp(int ap)
        {
            return ap > 0 ? $"+{ap}" : ap.ToString();
        }

        private static string FormatSigned(int value)
        {
            return value > 0 ? $"+{value}" : value.ToString();
        }

        private static int ApplyFeelNoPain(
            VersusContext context,
            CombatUnit attackerUnit,
            CombatWeapon weapon,
            CombatUnit defenderUnit,
            CombatModel targetModel,
            List<CombatUnit> defenders,
            int damage,
            DamageResult result,
            AttackSequenceLog? attackLog,
            IReadOnlyList<ConditionalModifier> activeDefenderModifiers)
        {
            if (damage <= 0)
                return 0;

            var feelNoPainModifiers = activeDefenderModifiers
                .Where(m => m.Effect.Type == EffectType.FeelNoPain)
                .Select(m => new
                {
                    Modifier = m,
                    Target = RollEffectValue(m.Effect, 5)
                })
                .Where(x => x.Target > 0)
                .OrderBy(x => x.Target)
                .ToList();

            if (feelNoPainModifiers.Count == 0)
                return damage;

            var bestFeelNoPain = feelNoPainModifiers[0];
            var ignoredDamage = 0;
            var rolls = new List<int>(damage);

            for (var i = 0; i < damage; i++)
            {
                var roll = RollD6();
                rolls.Add(roll);

                if (roll >= bestFeelNoPain.Target)
                {
                    ignoredDamage++;
                }
            }

            if (ignoredDamage > 0)
            {
                result.FeelNoPainSaves += ignoredDamage;
                result.DamagePrevented += ignoredDamage;
            }

            attackLog?.DamageEvents.Add(
                $"{bestFeelNoPain.Modifier.Name}: ignored {ignoredDamage} of {damage} damage on {targetModel.Name} ({defenderUnit.DatasheetName})");

            attackLog?.DamageEvents.Add(
                $"[VERBOSE] {bestFeelNoPain.Modifier.Name}: rolls [{string.Join(", ", rolls)}] vs {bestFeelNoPain.Target}+");

            return damage - ignoredDamage;
        }

        private static bool IsMeleeWeapon(CombatWeapon weapon)
        {
            return string.IsNullOrEmpty(weapon.Range) || 
                   weapon.Range.Equals("Melee", StringComparison.OrdinalIgnoreCase) ||
                   weapon.Range.Equals("-", StringComparison.OrdinalIgnoreCase);
        }

        private static readonly ThreadLocal<Random> _rng =
            new(() => new Random());

        private static int RollD6() => _rng.Value!.Next(1, 7);

        /// <summary>
        /// Resolves a modifier effect value, rolling dice if the value is an expression like D3, D6+1, 2D6.
        /// Falls back to IntValue for plain integers saved before StringValue support was added.
        /// </summary>
        private static int RollEffectValue(ModifierEffect effect, int fallback = 0)
        {
            if (!string.IsNullOrWhiteSpace(effect.StringValue))
                return RollDamage(effect.StringValue);
            return effect.IntValue ?? fallback;
        }

        private static int RollWeaponDamage(
            CombatWeapon weapon,
            IReadOnlyList<ConditionalModifier> activeAttackerModifiers,
            AttackSequenceLog? attackLog)
        {
            var damage = RollDamage(weapon.D);
            attackLog?.DamageDice.Add(damage);

            if (!activeAttackerModifiers.Any(modifier => modifier.Effect.Type == EffectType.RerollDamage) ||
                !IsVariableRoll(weapon.D))
            {
                return damage;
            }

            var maxDamage = GetMaxRollValue(weapon.D);
            if (maxDamage <= 0 || damage * 2 > maxDamage)
            {
                return damage;
            }

            var rerolledDamage = RollDamage(weapon.D);
            attackLog?.DamageDice.Add(rerolledDamage);
            attackLog?.DamageEvents.Add($"Rerolled damage: {damage} → {rerolledDamage}");
            return rerolledDamage;
        }

        private static bool IsVariableRoll(string value)
            => !string.IsNullOrWhiteSpace(value) && value.Contains('D', StringComparison.OrdinalIgnoreCase);

        private static int GetMaxRollValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            value = value.Trim();

            if (int.TryParse(value, out var flat))
                return flat;

            var dIndex = value.IndexOf('D');
            if (dIndex == -1)
                return 0;

            var dice = dIndex == 0
                ? 1
                : int.Parse(value.Substring(0, dIndex));

            var plusIndex = value.IndexOf('+');
            var size = plusIndex > 0
                ? int.Parse(value.Substring(dIndex + 1, plusIndex - dIndex - 1))
                : int.Parse(value[(dIndex + 1)..]);
            var mod = plusIndex > 0
                ? int.Parse(value[(plusIndex + 1)..])
                : 0;

            return (dice * size) + mod;
        }

        private static int RollDamage(string damage)
        {
            if (string.IsNullOrWhiteSpace(damage))
                return 0;

            if (int.TryParse(damage, out int flat))
                return flat;

            int dIndex = damage.IndexOf('D');

            int dice =
                dIndex == 0
                    ? 1
                    : int.Parse(damage.Substring(0, dIndex));

            int plusIndex = damage.IndexOf('+');

            int size;
            int mod = 0;

            if (plusIndex > 0)
            {
                size = int.Parse(damage.Substring(dIndex + 1, plusIndex - dIndex - 1));
                mod = int.Parse(damage[(plusIndex + 1)..]);
            }
            else
            {
                size = int.Parse(damage[(dIndex + 1)..]);
            }

            int total = 0;

            for (int i = 0; i < dice; i++)
                total += _rng.Value!.Next(1, size + 1);

            return total + mod;
        }

        private static int ParseDiceValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            value = value.Trim();

            if (int.TryParse(value, out int flat))
                return flat;

            int dIndex = value.IndexOf('D');

            if (dIndex == -1)
                return 0;

            int dice =
                dIndex == 0
                    ? 1
                    : int.Parse(value.Substring(0, dIndex));

            int size = int.Parse(value.Substring(dIndex + 1));

            return dice * (size + 1) / 2;
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

        private static readonly ObjectPool<List<CombatLogEntry>> _logPool =
            new DefaultObjectPool<List<CombatLogEntry>>(new ListPolicy<CombatLogEntry>());

        private static List<CombatLogEntry> GetLog()
        {
            var log = _logPool.Get();
            log.Clear();
            return log;
        }

        private static void ReturnLog(List<CombatLogEntry> log)
        {
            log.Clear();
            _logPool.Return(log);
        }

        class ListPolicy<T> : PooledObjectPolicy<List<T>>
        {
            public override List<T> Create() => new List<T>(256);

            public override bool Return(List<T> obj)
            {
                obj.Clear();
                return true;
            }
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

        /// <summary>
        /// Collects and formats weapon abilities for display.
        /// </summary>
        private static void CollectWeaponAbilities(CombatWeapon weapon, AttackSequenceLog? attackLog)
        {
            if (attackLog == null) return;

            // Add all abilities from the list
            foreach (var ability in weapon.Abilities.Abilities)
            {
                attackLog?.WeaponAbilities.Add(ability.DisplayName);
            }
        }

        /// <summary>
        /// Collects active conditional modifiers for display.
        /// </summary>
        private static void CollectActiveModifiers(IReadOnlyList<ConditionalModifier> activeAttackerModifiers, AttackSequenceLog? attackLog)
        {
            if (attackLog == null) return;
            foreach (var modifier in activeAttackerModifiers)
            {
                if (modifier.Condition.Type == ConditionType.CriticalHit || modifier.Condition.Type == ConditionType.CriticalWound)
                    continue;

                var effectDescription = GetEffectDescription(modifier.Effect);
                if (!string.IsNullOrEmpty(effectDescription))
                {
                    attackLog?.ActiveModifiers.Add($"{modifier.Name}: {effectDescription}");
                }
            }
        }

        private static AttackModifierCache BuildModifierCache(
            VersusContext context,
            CombatUnit attackerUnit,
            CombatModel attackerModel,
            CombatWeapon weapon,
            List<CombatUnit> defenders)
        {
            var activeAttackerModifiers = new List<ConditionalModifier>();
            var criticalHitTriggeredModifiers = new List<ConditionalModifier>();
            var criticalWoundTriggeredModifiers = new List<ConditionalModifier>();

            foreach (var modifier in GetAttackerModifiers(context, attackerUnit, attackerModel, weapon))
            {
                if (!modifier.IsActive)
                    continue;

                if (modifier.Condition.Type == ConditionType.CriticalHit)
                {
                    criticalHitTriggeredModifiers.Add(modifier);
                    continue;
                }

                if (modifier.Condition.Type == ConditionType.CriticalWound)
                {
                    criticalWoundTriggeredModifiers.Add(modifier);
                    continue;
                }

                if (!EvaluateCondition(modifier.Condition, context, attackerUnit, weapon, defenders))
                    continue;

                activeAttackerModifiers.Add(modifier);
                criticalHitTriggeredModifiers.Add(modifier);
                criticalWoundTriggeredModifiers.Add(modifier);
            }

            var activeDefenderBattlefieldModifiers = new List<ConditionalModifier>();
            var seenDefenderModifierIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var modifier in context.DefenderGlobalModifiers.Modifiers)
            {
                AddActiveDefenderBattlefieldModifier(context, attackerUnit, weapon, defenders, modifier, activeDefenderBattlefieldModifiers, seenDefenderModifierIds);
            }

            foreach (var defender in defenders)
            {
                foreach (var modifier in defender.Modifiers)
                {
                    AddActiveDefenderBattlefieldModifier(context, attackerUnit, weapon, defenders, modifier, activeDefenderBattlefieldModifiers, seenDefenderModifierIds);
                }

                foreach (var model in defender.Models)
                {
                    foreach (var modifier in model.Modifiers)
                    {
                        AddActiveDefenderBattlefieldModifier(context, attackerUnit, weapon, defenders, modifier, activeDefenderBattlefieldModifiers, seenDefenderModifierIds);
                    }
                }
            }

            return new AttackModifierCache(activeAttackerModifiers, criticalHitTriggeredModifiers, criticalWoundTriggeredModifiers, activeDefenderBattlefieldModifiers);
        }

        private static void AddActiveDefenderBattlefieldModifier(
            VersusContext context,
            CombatUnit attackerUnit,
            CombatWeapon weapon,
            List<CombatUnit> defenders,
            ConditionalModifier modifier,
            List<ConditionalModifier> activeDefenderBattlefieldModifiers,
            HashSet<string> seenDefenderModifierIds)
        {
            if (!modifier.IsActive ||
                !seenDefenderModifierIds.Add(modifier.Id) ||
                !EvaluateCondition(modifier.Condition, context, attackerUnit, weapon, defenders))
            {
                return;
            }

            activeDefenderBattlefieldModifiers.Add(modifier);
        }

        private static IEnumerable<ConditionalModifier> GetActiveAttackerModifiers(
            VersusContext context,
            CombatUnit attackerUnit,
            CombatModel attackerModel,
            CombatWeapon weapon,
            List<CombatUnit>? defenders)
        {
            return GetAttackerModifiers(context, attackerUnit, attackerModel, weapon)
                .Where(m => m.IsActive && EvaluateCondition(m.Condition, context, attackerUnit, weapon, defenders));
        }

        private static IEnumerable<ConditionalModifier> GetCriticalHitTriggeredModifiers(
            VersusContext context,
            CombatUnit attackerUnit,
            CombatModel attackerModel,
            CombatWeapon weapon,
            List<CombatUnit>? defenders)
        {
            return GetAttackerModifiers(context, attackerUnit, attackerModel, weapon)
                .Where(m => m.IsActive && (m.Condition.Type == ConditionType.CriticalHit || EvaluateCondition(m.Condition, context, attackerUnit, weapon, defenders)));
        }

        private static IEnumerable<ConditionalModifier> GetCriticalWoundTriggeredModifiers(
            VersusContext context,
            CombatUnit attackerUnit,
            CombatModel attackerModel,
            CombatWeapon weapon,
            List<CombatUnit>? defenders)
        {
            return GetAttackerModifiers(context, attackerUnit, attackerModel, weapon)
                .Where(m => m.IsActive && (m.Condition.Type == ConditionType.CriticalWound || EvaluateCondition(m.Condition, context, attackerUnit, weapon, defenders)));
        }

        private static IEnumerable<ConditionalModifier> GetAttackerModifiers(
            VersusContext context,
            CombatUnit attackerUnit,
            CombatModel attackerModel,
            CombatWeapon weapon)
        {
            return context.AttackerGlobalModifiers.Modifiers
                .Concat(attackerUnit.Modifiers)
                .Concat(attackerModel.Modifiers)
                .Concat(weapon.Modifiers);
        }

        private static IEnumerable<ConditionalModifier> GetActiveDefenderModifiers(
            VersusContext context,
            CombatUnit attackerUnit,
            CombatWeapon weapon,
            CombatUnit defenderUnit,
            CombatModel defenderModel,
            List<CombatUnit> defenders)
        {
            return context.DefenderGlobalModifiers.Modifiers
                .Concat(defenderUnit.Modifiers)
                .Concat(defenderModel.Modifiers)
                .Where(m => m.IsActive && EvaluateCondition(m.Condition, context, attackerUnit, weapon, defenders));
        }

        private static IEnumerable<ConditionalModifier> GetActiveDefenderBattlefieldModifiers(
            VersusContext context,
            CombatUnit attackerUnit,
            CombatWeapon weapon,
            List<CombatUnit> defenders)
        {
            return context.DefenderGlobalModifiers.Modifiers
                .Concat(defenders.SelectMany(u => u.Modifiers))
                .Concat(defenders.SelectMany(u => u.Models.SelectMany(m => m.Modifiers)))
                .Where(m => m.IsActive && EvaluateCondition(m.Condition, context, attackerUnit, weapon, defenders))
                .GroupBy(m => m.Id)
                .Select(g => g.First());
        }

        private static bool IsBuiltInRapidFireModifierHandledByWeaponAbility(CombatWeapon weapon, ConditionalModifier modifier)
        {
            return weapon.Abilities.RapidFire.HasValue &&
                modifier.Effect.Type == EffectType.AddAttacks &&
                modifier.Name.Contains("Rapid Fire", StringComparison.OrdinalIgnoreCase) &&
                modifier.Name.Contains("(Built-in)", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBuiltInCriticalHitModifierHandledByWeaponAbility(CombatWeapon weapon, ConditionalModifier modifier)
        {
            if (!modifier.Name.Contains("(Built-in)", StringComparison.OrdinalIgnoreCase))
                return false;

            return (modifier.Effect.Type == EffectType.AddExtraHitsOnCrit && weapon.Abilities.SustainedHits.HasValue) ||
                   (modifier.Effect.Type == EffectType.AutoWoundOnCrit && weapon.Abilities.LethalHits);
        }

        private static bool IsBuiltInCriticalWoundModifierHandledByWeaponAbility(CombatWeapon weapon, ConditionalModifier modifier)
        {
            return modifier.Name.Contains("(Built-in)", StringComparison.OrdinalIgnoreCase) &&
                   modifier.Effect.Type == EffectType.ConvertToMortalWounds &&
                   weapon.Abilities.DevastatingWounds;
        }

        /// <summary>
        /// Gets a human-readable description of a modifier effect.
        /// </summary>
        private static string GetEffectDescription(ModifierEffect effect)
        {
            return effect.Type switch
            {
                EffectType.AddHitModifier => $"+{effect.IntValue} to Hit",
                EffectType.ImproveBallisticSkill => $"Improve Ballistic Skill by {effect.IntValue}",
                EffectType.ImproveWeaponSkill => $"Improve Weapon Skill by {effect.IntValue}",
                EffectType.AddWoundModifier => $"+{effect.IntValue} to Wound",
                EffectType.AddSaveModifier => $"+{effect.IntValue} to Save",
                EffectType.AddAPModifier => $"+{effect.IntValue} AP",
                EffectType.AddDamageModifier => $"+{effect.IntValue} Damage",
                EffectType.AddAttacks => $"+{effect.IntValue} Attacks",
                EffectType.RerollHits => "Reroll all Hit rolls",
                EffectType.RerollWounds => "Reroll all Wound rolls",
                EffectType.RerollDamage => "Reroll all Damage rolls",
                EffectType.RerollOnes => "Reroll 1s",
                EffectType.IgnoreInvulnerable => "Ignore Invulnerable saves",
                EffectType.IgnoreCover => "Ignore Cover",
                EffectType.HalveDamage => "Halve Damage",
                EffectType.ReduceDamage => $"Reduce Damage by {effect.IntValue}",
                EffectType.FeelNoPain => $"Feel No Pain {effect.IntValue}+",
                EffectType.CriticalHitOn => $"Critical Hit on {effect.IntValue}+",
                EffectType.CriticalWoundOn => $"Critical Wound on {effect.IntValue}+",
                EffectType.AutoWoundOnCrit => "Auto-wound on Critical Hits",
                EffectType.ConvertToMortalWounds => "Convert to Mortal Wounds",
                _ => ""
            };
        }

        private static List<AttackInstance> BuildAttackInstances(VersusContext context)
        {
            var list = new List<AttackInstance>();

            foreach (var unit in context.AttackingUnits)
            {
                foreach (var model in unit.Models)
                {
                    foreach (var weapon in model.Weapons)
                    {
                        if (!weapon.IsSelected)
                            continue;

                        for (int i = 0; i < weapon.Quantity; i++)
                        {
                            list.Add(new AttackInstance
                            {
                                Unit = unit,
                                Model = model,
                                Weapon = weapon,
                                Instance = i + 1
                            });
                        }
                    }
                }
            }

            return list;
        }

        private readonly struct AttackInstance
        {
            public CombatUnit Unit { get; init; }
            public CombatModel Model { get; init; }
            public CombatWeapon Weapon { get; init; }
            public int Instance { get; init; }
        }

        // Internal classes for simulation

        private class AttackSequenceLog
        {
            public string AttackerName { get; set; } = string.Empty;
            public string WeaponName { get; set; } = string.Empty;
            public bool IsMelee { get; set; }
            public List<string> WeaponAbilities { get; set; } = new();
            public List<string> ActiveModifiers { get; set; } = new();
            public int Attacks { get; set; }
            public int Hits { get; set; }
            public int CriticalHits { get; set; }
            public int Misses { get; set; }
            public int AutoWounds { get; set; }
            public List<string> HitEffects { get; set; } = new();
            public List<int> HitDice { get; set; } = new();
            public int Wounds { get; set; }
            public int CriticalWounds { get; set; }
            public int FailedToWound { get; set; }
            public int MortalWounds { get; set; }
            public List<string> WoundEffects { get; set; } = new();
            public List<int> WoundDice { get; set; } = new();
            public List<string> SaveEffects { get; set; } = new();
            public List<SaveAttempt> SaveAttempts { get; set; } = new();
            public List<int> SaveDice { get; set; } = new();
            public int TotalDamage { get; set; }
            public int RawDamage { get; set; }
            public List<string> AttackEffects { get; set; } = new();
            public List<int> DamageDice { get; set; } = new();
            public List<string> DamageEvents { get; set; } = new();
            public bool TargetDestroyed { get; set; }
        }

        private class SaveAttempt
        {
            public string DefenderName { get; set; } = string.Empty;
            public int Roll { get; set; }
            public int Target { get; set; }
            public bool Passed { get; set; }
            public string SaveType { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
            public bool IsMortalWound { get; set; }
        }

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

        private struct HitRollResult
        {
            public int Hits;
            public int CriticalHits;
            public int AutoWounds;
            public int Misses;
        }

        private struct WoundRollResult
        {
            public int Wounds;
            public int CriticalWounds;
            public int MortalWounds;
            public int DevastatingMortalWounds;
            public int FailedWounds;
        }

        private struct DamageResult
        {
            public double TotalDamage;
            public int UnsavedWounds;
            public int FailedSaves;
            public int SuccessfulSaves;
            public int InvulnerableSaves;
            public int FeelNoPainSaves;
            public double DamagePrevented;
        }

        private class SaveResult
        {
            public bool Saved { get; set; }
            public bool UsedInvulnerable { get; set; }
            public int Roll { get; set; }
            public int Target { get; set; }
            public string Summary { get; set; } = string.Empty;
        }

        private sealed class AttackModifierCache
        {
            public AttackModifierCache(
                List<ConditionalModifier> activeAttackerModifiers,
                List<ConditionalModifier> criticalHitTriggeredModifiers,
                List<ConditionalModifier> criticalWoundTriggeredModifiers,
                List<ConditionalModifier> activeDefenderBattlefieldModifiers)
            {
                ActiveAttackerModifiers = activeAttackerModifiers;
                CriticalHitTriggeredModifiers = criticalHitTriggeredModifiers;
                CriticalWoundTriggeredModifiers = criticalWoundTriggeredModifiers;
                ActiveDefenderBattlefieldModifiers = activeDefenderBattlefieldModifiers;
            }

            public IReadOnlyList<ConditionalModifier> ActiveAttackerModifiers { get; }

            public IReadOnlyList<ConditionalModifier> CriticalHitTriggeredModifiers { get; }

            public IReadOnlyList<ConditionalModifier> CriticalWoundTriggeredModifiers { get; }

            public IReadOnlyList<ConditionalModifier> ActiveDefenderBattlefieldModifiers { get; }
        }
    }
}

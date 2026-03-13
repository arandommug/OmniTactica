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

                ProcessWeaponsForPhase(context, defenders, run, ref step, enableLogging, isRanged: true);
            }

            // Fight Phase
            if (runFight)
            {
                if (enableLogging && phaseMode == CombatPhaseMode.Both)
                {
                    LogPhaseHeader(run, ref step, "FIGHT PHASE");
                }

                ProcessWeaponsForPhase(context, defenders, run, ref step, enableLogging, isRanged: false);
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
            bool isRanged)
        {
            foreach (var attacker in context.AttackingUnits)
            {
                foreach (var model in attacker.Models)
                {
                    for (int modelInstance = 0; modelInstance < model.Quantity; modelInstance++)
                    {
                        var phaseWeapons = model.Weapons
                            .Where(w => w.IsSelected && IsMeleeWeapon(w) != isRanged)
                            .ToList();

                        if (!phaseWeapons.Any()) continue;

                        if (enableLogging && model.Quantity > 1)
                        {
                            Log(run, step++, "Attacks", attacker.DatasheetName, model.Name, "", "", "",
                                $"═══ {model.Name} #{modelInstance + 1} of {model.Quantity} ═══",
                                new Dictionary<string, object>());
                        }

                        foreach (var weapon in phaseWeapons)
                        {
                            if (!defenders.Any(u => u.Models.Any(m => !m.IsDestroyed)))
                                break;

                            var weaponResult = ResolveWeaponAttack(
                                attacker, model, weapon,
                                defenders,
                                context,
                                run, ref step, modelInstance + 1);

                            run.TotalDamage += weaponResult.Damage;
                        }
                    }
                }
            }
        }

        private static WeaponAttackResult ResolveWeaponAttack(
            CombatUnit attackerUnit,
            CombatModel attackerModel,
            CombatWeapon weapon,
            List<CombatUnit> defenders,
            VersusContext context,
            SimulationRun run,
            ref int step,
            int modelInstance = 1)
        {
            var result = new WeaponAttackResult
            {
                WeaponId = weapon.Id,
                WeaponName = weapon.Name
            };

            // Create model display name with instance number if multiple models
            var modelDisplayName = attackerModel.Quantity > 1 
                ? $"{attackerModel.Name} #{modelInstance}"
                : attackerModel.Name;

            // Create attack sequence log for battle report format
            var attackLog = new AttackSequenceLog
            {
                AttackerName = modelDisplayName,
                WeaponName = weapon.Name,
                IsMelee = IsMeleeWeapon(weapon)
            };

            // Collect weapon abilities
            CollectWeaponAbilities(weapon, attackLog);

            // Collect active modifiers
            CollectActiveModifiers(weapon, context, attackerUnit, defenders, attackLog);

            // Step 1: Determine number of attacks
            result.Attacks = DetermineAttacks(weapon, attackerUnit, attackerModel, defenders, context, attackLog);
            attackLog.Attacks = result.Attacks;
            run.Stages.TotalAttacks += result.Attacks;

            // Step 2: Resolve hit rolls
            var hitResult = ResolveHitRolls(weapon, attackerUnit, attackerModel, result.Attacks, context, run, ref step, modelDisplayName, attackLog);
            result.Hits = hitResult.Hits;
            result.CriticalHits = hitResult.CriticalHits;
            result.AutoWounds = hitResult.AutoWounds;
            attackLog.Hits = hitResult.Hits;
            attackLog.CriticalHits = hitResult.CriticalHits;
            attackLog.AutoWounds = hitResult.AutoWounds;
            attackLog.Misses = result.Attacks - hitResult.Hits - hitResult.CriticalHits;
            run.Stages.TotalHits += result.Hits;
            run.Stages.CriticalHits += result.CriticalHits;

            // Step 3: Resolve wound rolls
            var woundResult = ResolveWoundRolls(weapon, attackerUnit, attackerModel, hitResult, defenders, context, run, ref step, modelDisplayName, attackLog);
            result.Wounds = woundResult.Wounds;
            result.CriticalWounds = woundResult.CriticalWounds;
            result.MortalWounds = woundResult.MortalWounds;
            attackLog.Wounds = woundResult.Wounds;
            attackLog.CriticalWounds = woundResult.CriticalWounds;
            attackLog.MortalWounds = woundResult.MortalWounds;
            attackLog.FailedToWound = hitResult.Hits - woundResult.Wounds - woundResult.CriticalWounds;
            run.Stages.TotalWounds += result.Wounds;
            run.Stages.CriticalWounds += result.CriticalWounds;
            run.Stages.MortalWounds += result.MortalWounds;

            // Step 4: Allocate and resolve saves
            var damageResult = AllocateWounds(weapon, attackerUnit, woundResult, defenders, context, run, ref step, modelDisplayName, attackLog);
            result.Damage = damageResult.TotalDamage;
            result.UnsavedWounds = damageResult.UnsavedWounds;
            attackLog.TotalDamage = (int)damageResult.TotalDamage;
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
        private static void LogFormattedAttackBlock(AttackSequenceLog attackLog, SimulationRun run, ref int step)
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
                Message = $"{attackLog.AttackerName} — {attackLog.WeaponName}",
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
            if (attackLog.WeaponAbilities.Any() || attackLog.ActiveModifiers.Any())
            {
                log.Add(new CombatLogEntry
                {
                    Step = step++,
                    Phase = "WeaponInfo",
                    Message = "Weapon Abilities & Active Modifiers:",
                    Details = new Dictionary<string, object>()
                });

                foreach (var ability in attackLog.WeaponAbilities)
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
            if (attackLog.HitDice.Any())
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
                if (attackLog.WoundDice.Any())
                {
                    log.Add(new CombatLogEntry { Step = step++, Phase = "Wound", Message = $"  [DICE] {string.Join(",", attackLog.WoundDice)}", Details = new Dictionary<string, object>() });
                }
                log.Add(new CombatLogEntry { Step = step++, Phase = "Wound", Message = "", Details = new Dictionary<string, object>() });
            }

            // Save Rolls (if there were wounds)
            if ((attackLog.Wounds + attackLog.CriticalWounds + attackLog.MortalWounds) > 0 && attackLog.SaveAttempts.Count > 0)
            {
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
                        }
                    }
                    log.Add(new CombatLogEntry { Step = step++, Phase = "Save", Message = "", Details = new Dictionary<string, object>() });
                }
                if (attackLog.SaveDice.Any())
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
                log.Add(new CombatLogEntry { Step = step++, Phase = "Result", Message = $"  💥 {attackLog.TotalDamage} damage inflicted{destroyedText}", Details = new Dictionary<string, object>() });
            }

            log.Add(new CombatLogEntry { Step = step++, Phase = "Result", Message = "", Details = new Dictionary<string, object>() });
        }

        private static int DetermineAttacks(
            CombatWeapon weapon,
            CombatUnit attackerUnit,
            CombatModel attackerModel,
            List<CombatUnit> defenders,
            VersusContext context,
            AttackSequenceLog attackLog)
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
                    attackLog.AttackEffects.Add($"Rapid Fire: Added {weapon.Abilities.RapidFire.Value} attacks (within half range)");
                }
            }

            // Apply Blast
            if (weapon.Abilities.Blast)
            {
                var totalDefenders = defenders.Sum(u => u.Models.Sum(m => m.Quantity));
                if (totalDefenders >= 10)
                {
                    attacks += 2;
                    attackLog.AttackEffects.Add("Blast: Added 2 attacks (10+ models)");
                }
                else if (totalDefenders >= 5)
                {
                    attacks++;
                    attackLog.AttackEffects.Add("Blast: Added 1 attack (5+ models)");
                }
            }

            // Apply modifiers from conditional modifiers
            foreach (var modifier in weapon.Modifiers.Where(m => m.IsActive))
            {
                if (EvaluateCondition(modifier.Condition, context, attackerUnit, weapon, defenders) &&
                    modifier.Effect.Type == EffectType.AddAttacks && modifier.Effect.IntValue.HasValue)
                {
                    attacks += modifier.Effect.IntValue.Value;
                    attackLog.AttackEffects.Add($"{modifier.Name}: Added {modifier.Effect.IntValue.Value} attacks");
                }
            }

            return Math.Max(1, attacks);
        }

        private static HitRollResult ResolveHitRolls(
            CombatWeapon weapon,
            CombatUnit attackerUnit,
            CombatModel attackerModel,
            int attacks,
            VersusContext context,
            SimulationRun run,
            ref int step,
            string modelDisplayName,
            AttackSequenceLog attackLog)
        {
            var result = new HitRollResult();
            var actionVerb = IsMeleeWeapon(weapon) ? "attacks with" : "fires";

            // Torrent auto-hits
            if (weapon.Abilities.Torrent)
            {
                result.Hits = attacks;
                attackLog.HitEffects.Add("Torrent: All attacks auto-hit");
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

            int misses = 0;
            for (int i = 0; i < attacks; i++)
            {
                var roll = RollD6();
                var unmodifiedRoll = roll; // Track unmodified roll for critical checks

                if ((rerollAll) || (rerollOnes && roll == 1))
                {
                    roll = RollD6();
                    unmodifiedRoll = roll; // After reroll, the reroll result is the "unmodified" value
                }

                attackLog.HitDice.Add(roll);

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
                            attackLog.HitEffects.Add($"Sustained Hits generated +{weapon.Abilities.SustainedHits.Value} additional hits");
                        }

                        // Lethal Hits
                        if (weapon.Abilities.LethalHits)
                        {
                            result.AutoWounds++;
                            result.Hits--;
                            attackLog.HitEffects.Add("Lethal Hits: Critical hit converted to auto-wound");
                        }
                    }
                }
                else
                {
                    misses++;
                }
            }

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
            ref int step,
            string modelDisplayName,
            AttackSequenceLog attackLog)
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
                    attackLog.WoundEffects.Add($"{modifier.Name}: Critical wounds on {modifier.Effect.IntValue.Value}+");
                }
            }

            // Check for rerolls
            var rerollAll = weapon.Abilities.TwinLinked || weapon.Modifiers.Any(m => m.IsActive && EvaluateCondition(m.Condition, context, attackerUnit, weapon, defenders) && m.Effect.Type == EffectType.RerollWounds);
            var rerollOnes = weapon.Modifiers.Any(m => m.IsActive && EvaluateCondition(m.Condition, context, attackerUnit, weapon, defenders) && m.Effect.Type == EffectType.RerollOnes);

            int failedToWound = 0;
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

                attackLog.WoundDice.Add(roll);

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
                            attackLog.WoundEffects.Add("Devastating Wounds: Critical wound converted to mortal wound");
                        }
                    }
                }
                else
                {
                    failedToWound++;
                }
            }

            return result;
        }

        private static DamageResult AllocateWounds(
            CombatWeapon weapon,
            CombatUnit attackerUnit,
            WoundRollResult woundResult,
            List<CombatUnit> defenders,
            VersusContext context,
            SimulationRun run,
            ref int step,
            string modelDisplayName,
            AttackSequenceLog attackLog)
        {
            var result = new DamageResult();
            var totalWounds = woundResult.Wounds + woundResult.MortalWounds;
            var initialNormalWounds = woundResult.Wounds;
            var initialMortalWounds = woundResult.MortalWounds;

            if (totalWounds <= 0) return result;

            // Get target models based on allocation method
            var targetModels = GetWoundAllocationTargets(defenders, context.SimulationSettings.WoundAllocation);

            foreach (var targetModel in targetModels)
            {
                if (totalWounds <= 0) break;
                if (targetModel.IsDestroyed) continue;

                var defenderUnit = defenders.First(u => u.Models.Contains(targetModel));

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
                        attackLog.SaveAttempts.Add(new SaveAttempt
                        {
                            DefenderName = $"{targetModel.Name} ({defenderUnit.DatasheetName})",
                            IsMortalWound = true
                        });
                    }
                    else
                    {
                        // Regular wound - resolve save
                        var saveResult = ResolveSave(weapon, targetModel, defenderUnit, context, run, ref step, attackerUnit.DatasheetName);

                        attackLog.SaveAttempts.Add(new SaveAttempt
                        {
                            DefenderName = $"{targetModel.Name} ({defenderUnit.DatasheetName})",
                            Roll = saveResult.Roll,
                            Target = saveResult.Target,
                            Passed = saveResult.Saved,
                            SaveType = saveResult.UsedInvulnerable ? "Invuln" : "Armor",
                            IsMortalWound = false
                        });

                        if (saveResult.Roll > 0)
                            attackLog.SaveDice.Add(saveResult.Roll);

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
                                }
                            }
                        }
                    }

                    // Apply damage
                    if (damage > 0)
                    {
                        // Cap effective damage to remaining wounds (excess damage is overkill and lost)
                        var effectiveDamage = Math.Min(damage, targetModel.CurrentWounds);
                        targetModel.CurrentWounds = Math.Max(0, targetModel.CurrentWounds - damage);
                        result.TotalDamage += effectiveDamage;

                        attackLog.DamageEvents.Add($"{damage} damage to {targetModel.Name} ({defenderUnit.DatasheetName}) - {targetModel.CurrentWounds}/{targetModel.MaxWounds} remaining");

                        if (targetModel.IsDestroyed)
                        {
                            attackLog.TargetDestroyed = true;
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
            CombatUnit defenderUnit,
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
            var allModels = defenders
                .SelectMany(u => u.Models)
                .Where(m => !m.IsDestroyed)
                .ToList();

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
                            Modifiers = model.Modifiers.ToList()
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

        private static bool IsMeleeWeapon(CombatWeapon weapon)
        {
            return string.IsNullOrEmpty(weapon.Range) || 
                   weapon.Range.Equals("Melee", StringComparison.OrdinalIgnoreCase) ||
                   weapon.Range.Equals("-", StringComparison.OrdinalIgnoreCase);
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

        /// <summary>
        /// Collects and formats weapon abilities for display.
        /// </summary>
        private static void CollectWeaponAbilities(CombatWeapon weapon, AttackSequenceLog attackLog)
        {
            // Add all abilities from the list
            foreach (var ability in weapon.Abilities.Abilities)
            {
                attackLog.WeaponAbilities.Add(ability.DisplayName);
            }
        }

        /// <summary>
        /// Collects active conditional modifiers for display.
        /// </summary>
        private static void CollectActiveModifiers(CombatWeapon weapon, VersusContext context, 
            CombatUnit attackerUnit, List<CombatUnit> defenders, AttackSequenceLog attackLog)
        {
            foreach (var modifier in weapon.Modifiers.Where(m => m.IsActive))
            {
                if (EvaluateCondition(modifier.Condition, context, attackerUnit, weapon, defenders))
                {
                    var effectDescription = GetEffectDescription(modifier.Effect);
                    if (!string.IsNullOrEmpty(effectDescription))
                    {
                        attackLog.ActiveModifiers.Add($"{modifier.Name}: {effectDescription}");
                    }
                }
            }
        }

        /// <summary>
        /// Gets a human-readable description of a modifier effect.
        /// </summary>
        private static string GetEffectDescription(ModifierEffect effect)
        {
            return effect.Type switch
            {
                EffectType.AddHitModifier => $"+{effect.IntValue} to Hit",
                EffectType.AddWoundModifier => $"+{effect.IntValue} to Wound",
                EffectType.AddSaveModifier => $"+{effect.IntValue} to Save",
                EffectType.AddAPModifier => $"+{effect.IntValue} AP",
                EffectType.AddDamageModifier => $"+{effect.IntValue} Damage",
                EffectType.AddAttacks => $"+{effect.IntValue} Attacks",
                EffectType.RerollHits => "Reroll all Hit rolls",
                EffectType.RerollWounds => "Reroll all Wound rolls",
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
            public List<SaveAttempt> SaveAttempts { get; set; } = new();
            public List<int> SaveDice { get; set; } = new();
            public int TotalDamage { get; set; }
            public List<string> AttackEffects { get; set; } = new();
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
            public int Roll { get; set; }
            public int Target { get; set; }
        }
    }
}

using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using OmniTactica.AppCode.Models.Core;
using OmniTactica.AppCode.Services;
using Microsoft.VSDiagnostics;

namespace OmniTactica.Benchmarks;
[CPUUsageDiagnoser]
public class VersusCalculatorMonteCarloBenchmarks
{
    private VersusContext _heavyShootingContext = null!;
    private VersusContext _combinedArmsContext = null!;
    [GlobalSetup]
    public void Setup()
    {
        _heavyShootingContext = CreateHeavyShootingContext();
        _combinedArmsContext = CreateCombinedArmsContext();
    }

    [Benchmark]
    public VersusResult Calculate_HeavyShooting()
    {
        return VersusCalculator.Calculate(_heavyShootingContext);
    }

    [Benchmark]
    public VersusResult Calculate_CombinedArms()
    {
        return VersusCalculator.Calculate(_combinedArmsContext);
    }

    private static VersusContext CreateHeavyShootingContext()
    {
        var attackerWeapon = CreateWeapon(name: "Storm fusillade", range: "24\"", attacks: "6", skill: 3, strength: 5, ap: 1, damage: "2", quantity: 10, abilities: new WeaponAbilities { Abilities = { new WeaponAbility { Id = "rapid_fire", Name = "Rapid Fire", Value = 2 }, new WeaponAbility { Id = "sustained_hits", Name = "Sustained Hits", Value = 1 }, new WeaponAbility { Id = "lethal_hits", Name = "Lethal Hits" } } }, modifiers: new List<ConditionalModifier> { CreateModifier("Coordinated Volley", ConditionType.Always, EffectType.AddHitModifier, 1), CreateModifier("Target Lock", ConditionType.Always, EffectType.RerollOnes), CreateModifier("Focused Barrage", ConditionType.Always, EffectType.AddAttacks, 2), CreateModifier("Crack Shot", ConditionType.CriticalHit, EffectType.AddExtraHitsOnCrit, 1), CreateModifier("Melta Surge", ConditionType.TargetWithinHalfRange, EffectType.AddDamageModifier, 1) });
        var attackerModel = new CombatModel
        {
            Name = "Shooter",
            Quantity = 10,
            T = 4,
            Sv = 3,
            W = 2,
            MaxWounds = 2,
            CurrentWounds = 2,
            Weapons =
            {
                attackerWeapon
            },
            Modifiers =
            {
                CreateModifier("Marksman Drill", ConditionType.Always, EffectType.ImproveBallisticSkill, 1),
                CreateModifier("Wound Guidance", ConditionType.Always, EffectType.AddWoundModifier, 1)
            }
        };
        var attackerUnit = new CombatUnit
        {
            DatasheetName = "Heavy Shooters",
            Models =
            {
                attackerModel
            },
            Modifiers =
            {
                CreateModifier("Unit Rerolls", ConditionType.Always, EffectType.RerollHits),
                CreateModifier("Devastator Doctrine", ConditionType.UnitRemainedStationary, EffectType.AddAPModifier, 1)
            }
        };
        var defenderModel = new CombatModel
        {
            Name = "Target",
            Quantity = 20,
            T = 5,
            Sv = 3,
            InvSv = 5,
            W = 3,
            MaxWounds = 3,
            CurrentWounds = 3,
            Modifiers =
            {
                CreateModifier("Reactive Plating", ConditionType.Always, EffectType.ReduceDamage, 1),
                CreateModifier("Emergency Shielding", ConditionType.Always, EffectType.AddSaveModifier, 1),
                CreateModifier("Endurance", ConditionType.Always, EffectType.FeelNoPain, 5)
            }
        };
        var defenderUnit = new CombatUnit
        {
            DatasheetName = "Elite Targets",
            DatasheetDetail = new DatasheetDetail
            {
                Name = "Elite Targets",
                Keywords = new List<string>
                {
                    "Infantry"
                }
            },
            Models =
            {
                defenderModel
            },
            Modifiers =
            {
                CreateModifier("Defensive Fog", ConditionType.Always, EffectType.AddHitModifier, -1),
                CreateModifier("Braced Armor", ConditionType.Always, EffectType.HalveDamage)
            }
        };
        return new VersusContext
        {
            AttackingUnits =
            {
                attackerUnit
            },
            DefendingUnits =
            {
                defenderUnit
            },
            AttackerGlobalModifiers = new GlobalModifiers
            {
                HitModifier = 1,
                WoundModifier = 1,
                IgnoreCover = false,
                Modifiers =
                {
                    CreateModifier("Battle Focus", ConditionType.UnitRemainedStationary, EffectType.AddAttacks, 1)
                }
            },
            DefenderGlobalModifiers = new GlobalModifiers
            {
                Cover = true,
                SaveModifier = 1,
                Modifiers =
                {
                    CreateModifier("Smoke", ConditionType.Always, EffectType.AddHitModifier, -1)
                }
            },
            SimulationSettings = new SimulationSettings
            {
                Iterations = 2000,
                EnableDetailedLogging = false,
                RangeToTarget = -1,
                AttackerRemainedStationary = true,
                CombatPhase = CombatPhaseMode.ShootingOnly,
                WoundAllocation = WoundAllocationMethod.TargetWeakest
            }
        };
    }

    private static VersusContext CreateCombinedArmsContext()
    {
        var rangedWeapon = CreateWeapon(name: "Burst cannon", range: "18\"", attacks: "4", skill: 3, strength: 6, ap: 1, damage: "2", quantity: 6, abilities: new WeaponAbilities { Abilities = { new WeaponAbility { Id = "rapid_fire", Name = "Rapid Fire", Value = 1 }, new WeaponAbility { Id = "sustained_hits", Name = "Sustained Hits", Value = 1 } } }, modifiers: new List<ConditionalModifier> { CreateModifier("Precision Targeting", ConditionType.Always, EffectType.AddHitModifier, 1), CreateModifier("Pinning Fire", ConditionType.Always, EffectType.AddWoundModifier, 1) });
        var meleeWeapon = CreateWeapon(name: "Chain blade", range: "Melee", attacks: "5", skill: 3, strength: 7, ap: 2, damage: "2", quantity: 6, abilities: new WeaponAbilities { Abilities = { new WeaponAbility { Id = "lethal_hits", Name = "Lethal Hits" }, new WeaponAbility { Id = "devastating_wounds", Name = "Devastating Wounds" } } }, modifiers: new List<ConditionalModifier> { CreateModifier("Shock Assault", ConditionType.UnitCharged, EffectType.AddAttacks, 1), CreateModifier("Brutal Strikes", ConditionType.CriticalWound, EffectType.ConvertToMortalWounds) });
        var attackerModel = new CombatModel
        {
            Name = "Assault Trooper",
            Quantity = 6,
            T = 4,
            Sv = 3,
            W = 3,
            MaxWounds = 3,
            CurrentWounds = 3,
            Weapons =
            {
                rangedWeapon,
                meleeWeapon
            },
            Modifiers =
            {
                CreateModifier("Disciplined Fury", ConditionType.Always, EffectType.RerollOnes),
                CreateModifier("Critical Precision", ConditionType.Always, EffectType.CriticalHitOn, 5)
            }
        };
        var attackerUnit = new CombatUnit
        {
            DatasheetName = "Assault Squad",
            Models =
            {
                attackerModel
            }
        };
        var defenderModel = new CombatModel
        {
            Name = "Bruiser",
            Quantity = 12,
            T = 6,
            Sv = 2,
            InvSv = 4,
            W = 4,
            MaxWounds = 4,
            CurrentWounds = 4,
            Modifiers =
            {
                CreateModifier("Iron Hide", ConditionType.Always, EffectType.ReduceDamage, 1),
                CreateModifier("Lasting Vigor", ConditionType.Always, EffectType.FeelNoPain, 6)
            }
        };
        var defenderUnit = new CombatUnit
        {
            DatasheetName = "Bruiser Pack",
            DatasheetDetail = new DatasheetDetail
            {
                Name = "Bruiser Pack",
                Keywords = new List<string>
                {
                    "Monster"
                }
            },
            Models =
            {
                defenderModel
            }
        };
        return new VersusContext
        {
            AttackingUnits =
            {
                attackerUnit
            },
            DefendingUnits =
            {
                defenderUnit
            },
            AttackerGlobalModifiers = new GlobalModifiers(),
            DefenderGlobalModifiers = new GlobalModifiers
            {
                Cover = true
            },
            SimulationSettings = new SimulationSettings
            {
                Iterations = 2000,
                EnableDetailedLogging = false,
                RangeToTarget = -1,
                AttackerCharged = true,
                CombatPhase = CombatPhaseMode.Both,
                WoundAllocation = WoundAllocationMethod.EvenDistribution
            }
        };
    }

    private static CombatWeapon CreateWeapon(string name, string range, string attacks, int skill, int strength, int ap, string damage, int quantity, WeaponAbilities abilities, List<ConditionalModifier> modifiers)
    {
        return new CombatWeapon
        {
            Name = name,
            Range = range,
            A = attacks,
            BsWs = skill,
            S = strength,
            AP = ap,
            D = damage,
            Quantity = quantity,
            IsSelected = true,
            Abilities = abilities,
            Modifiers = modifiers
        };
    }

    private static ConditionalModifier CreateModifier(string name, ConditionType condition, EffectType effectType, int? intValue = null)
    {
        return new ConditionalModifier
        {
            Name = name,
            Condition = new ModifierCondition
            {
                Type = condition
            },
            Effect = new ModifierEffect
            {
                Type = effectType,
                IntValue = intValue
            },
            IsActive = true
        };
    }
}
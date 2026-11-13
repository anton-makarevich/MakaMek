using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Classic BattleTech implementation of the physical attack to-hit calculator.
/// Base to-hit is the attacker's Piloting Skill plus a per-type base modifier
/// (Punch +0, Kick -2, Push -1, Charge +0, DFA +0); standard attacker movement, target movement
/// and terrain modifiers apply, as do target-side modifiers (partial cover, skidding, jumped) and
/// the attacker-side skidding modifier. Heat and sensor modifiers are never applied. A DFA is the
/// one exception to terrain: it applies all the normal attack modifiers, including the attacker's
/// jumping movement, but its roll is never modified for terrain.
/// <para>
/// Attacker movement modifiers are cumulative (Attack Modifiers Table): the skidding
/// attacker modifier (+1) is added on top of the movement-type modifier, not in place
/// of it. Prone attackers cannot perform physical attacks (see
/// <see cref="AttackerProneGate"/>); the only exception (punching a vehicle in the same
/// hex) is not modelled yet.
/// </para>
/// </summary>
public class PhysicalAttackCalculator : IPhysicalAttackCalculator
{
    private readonly IRulesProvider _rules;

    public PhysicalAttackCalculator(IRulesProvider rules)
    {
        _rules = rules;
    }

    public int GetToHitNumber(IUnit attacker, IUnit target, PhysicalAttackType attackType, IBattleMap map)
    {
        if (attackType == PhysicalAttackType.DFA)
            return GetDfaToHitNumber(attacker, target, map);

        var scenario = PhysicalAttackScenario.FromUnits(attacker, target, attackType);
        return GetModifierBreakdown(scenario, map).Total;
    }

    public int GetDamage(IUnit attacker, PhysicalAttackType attackType, PartLocation limb) =>
        PhysicalAttackDamageCalculator.Calculate(_rules, attacker, attackType, limb);

    public PhysicalAttackBreakdown GetModifierBreakdown(IUnit attacker, IUnit target, PhysicalAttackType attackType, IBattleMap map)
    {
        if (attackType == PhysicalAttackType.DFA)
            return GetDfaModifierBreakdown(attacker, target, map);

        var scenario = PhysicalAttackScenario.FromUnits(attacker, target, attackType);
        return GetModifierBreakdown(scenario, map);
    }

    public int GetToHitNumber(PhysicalAttackScenario scenario, IBattleMap map)
    {
        // Movement attacks add the relative-piloting modifier on top of the standard physical
        // attack modifiers, exactly as the resolution path does.
        return PhysicalAttackDefinitions.IsMovementAttack(scenario.AttackType)
            ? GetMovementAttackModifierBreakdown(scenario, map).Total
            : GetModifierBreakdown(scenario, map).Total;
    }

    public PhysicalAttackBreakdown GetModifierBreakdown(PhysicalAttackScenario scenario, IBattleMap map)
    {
        var losResult = map.GetLineOfSight(
            scenario.AttackerPosition,
            scenario.TargetPosition,
            scenario.AttackerHeight,
            scenario.TargetHeight);

        // Total Warfare, p. 144 (Attack Modifiers Table footnote): a DFA applies all the normal
        // attack modifiers, including the attacker's jumping movement, but the roll is not
        // modified for terrain. Target movement and target-status modifiers still apply.
        var terrainModifiers =
            scenario.AttackType == PhysicalAttackType.DFA
                ? []
                : losResult.GetTerrainToHitModifiers(_rules);

        var otherModifiers = GetTargetModifiers(scenario, losResult);
        AddPushShoulderModifier(scenario, otherModifiers);

        // Attacker movement modifiers are cumulative (Attack Modifiers Table): a skidding
        // attacker adds +1 on top of its movement-type modifier.
        var isSkidding = scenario.AttackerUnit?.IsSkidding == true;
        if (isSkidding)
        {
            otherModifiers.Add(new SkiddingAttackerModifier
            {
                Value = SkiddingAttackerModifier.DefaultValue
            });
        }

        return new PhysicalAttackBreakdown
        {
            PilotingBase = new PilotingRollModifier
            {
                Value = scenario.AttackerPiloting
            },
            AttackTypeModifier = new PhysicalAttackBaseModifier
            {
                Value = _rules.GetPhysicalAttackBaseModifier(scenario.AttackType),
                AttackType = scenario.AttackType
            },
            AttackerMovement = new AttackerMovementModifier
            {
                Value = _rules.GetAttackerMovementModifier(scenario.AttackerMovementType),
                MovementType = scenario.AttackerMovementType
            },
            TargetMovement = new TargetMovementModifier
            {
                Value = _rules.GetTargetMovementModifier(scenario.TargetHexesMoved),
                HexesMoved = scenario.TargetHexesMoved
            },
            OtherModifiers = otherModifiers,
            TerrainModifiers = terrainModifiers
        };
    }

    /// <inheritdoc />
    public PhysicalAttackBreakdown GetChargeModifierBreakdown(
        IUnit attacker,
        IUnit target,
        IBattleMap map,
        bool isReplacementTarget)
    {
        var scenario = PhysicalAttackScenario.FromUnits(attacker, target, PhysicalAttackType.Charge);
        var breakdown = GetMovementAttackModifierBreakdown(scenario, map);
        if (!isReplacementTarget) return breakdown;

        return breakdown with
        {
            OtherModifiers =
            [
                .. breakdown.OtherModifiers,
                new ChargeReplacementTargetModifier
                {
                    Value = ChargeReplacementTargetModifier.DefaultValue
                }
            ]
        };
    }

    /// <inheritdoc />
    public PhysicalAttackBreakdown GetMovementAttackModifierBreakdown(
        PhysicalAttackScenario scenario,
        IBattleMap map)
    {
        var breakdown = GetModifierBreakdown(scenario, map);

        // Total Warfare p. 148: add the difference when the target has the lower (better) Piloting
        // Skill Rating, subtract it when the attacker has the lower one. Piloting skill is static
        // for a whole game, so reading it from the live target unit is safe.
        if (scenario.TargetUnit?.Pilot is not { } targetPilot) return breakdown;

        return breakdown with
        {
            OtherModifiers =
            [
                .. breakdown.OtherModifiers,
                new RelativePilotingModifier
                {
                    Value = scenario.AttackerPiloting - targetPilot.Piloting,
                    AttackerPiloting = scenario.AttackerPiloting,
                    TargetPiloting = targetPilot.Piloting
                }
            ]
        };
    }

    /// <inheritdoc />
    public int GetChargeToHitNumber(IUnit attacker, IUnit target, IBattleMap map, bool isReplacementTarget) =>
        GetChargeModifierBreakdown(attacker, target, map, isReplacementTarget).Total;

    /// <inheritdoc />
    public PhysicalAttackBreakdown GetDfaModifierBreakdown(IUnit attacker, IUnit target, IBattleMap map)
    {
        var scenario = PhysicalAttackScenario.FromUnits(attacker, target, PhysicalAttackType.DFA);
        return GetMovementAttackModifierBreakdown(scenario, map);
    }

    /// <inheritdoc />
    public int GetDfaToHitNumber(IUnit attacker, IUnit target, IBattleMap map) =>
        GetDfaModifierBreakdown(attacker, target, map).Total;

    /// <summary>
    /// A push adds +2 for each damaged shoulder actuator across the attacker's arms. It is not a
    /// blocker: a Mech with a destroyed shoulder can still push (Total Warfare, p. 148).
    /// </summary>
    private void AddPushShoulderModifier(PhysicalAttackScenario scenario, List<RollModifier> modifiers)
    {
        if (scenario.AttackType != PhysicalAttackType.Push || scenario.AttackerUnit is not { } attacker) return;

        PartLocation[] arms = [PartLocation.LeftArm, PartLocation.RightArm];
        var damagedShoulders = arms.Count(arm =>
            attacker.GetComponentsAtLocation<ShoulderActuator>(arm).Any(component => component.IsDestroyed));
        if (damagedShoulders == 0) return;

        modifiers.Add(new DamagedShoulderModifier
        {
            Value = _rules.GetPushDamagedShoulderModifier() * damagedShoulders,
            DamagedShoulders = damagedShoulders
        });
    }

    /// <summary>
    /// Collects the target-side modifiers that apply to physical attacks.
    /// Partial cover (+1) and skidding targets (+2) apply to all attacks, weapon and physical alike,
    /// except a DFA: partial cover is a terrain modifier, and a DFA roll is never modified for terrain.
    /// </summary>
    private List<RollModifier> GetTargetModifiers(
        PhysicalAttackScenario scenario,
        LineOfSightResult losResult)
    {
        List<RollModifier> modifiers = [];

        if (scenario.TargetUnit is null) return modifiers;

        if (scenario.AttackType != PhysicalAttackType.DFA
            && _rules.HasPartialCover(scenario.TargetUnit, losResult))
        {
            modifiers.Add(new PartialCoverModifier
            {
                Value = _rules.GetPartialCoverModifier()
            });
        }

        if (scenario.TargetUnit.IsSkidding)
        {
            modifiers.Add(new SkiddingTargetModifier
            {
                Value = SkiddingTargetModifier.DefaultValue
            });
        }

        if (scenario.TargetUnit.Status.HasFlag(UnitStatus.Prone))
        {
            var distance = scenario.AttackerPosition.Coordinates.DistanceTo(scenario.TargetPosition.Coordinates);
            modifiers.Add(new ProneTargetModifier { Value = _rules.GetProneTargetModifier(distance) });
        }

        // A target that jumped this turn is +1 to hit, cumulatively with its movement modifier. A
        // pending DFA attacker hovers in its approach hex and counts the same way (Total Warfare,
        // p. 117 and p. 148; docs/rules/physical-attacks.md section 3.1).
        if (scenario.TargetUnit.MovementTaken?.MovementType == MovementType.Jump
            || scenario.TargetUnit.IsAirborneDfaAttacker)
        {
            modifiers.Add(new JumpedTargetModifier { Value = 1 });
        }

        return modifiers;
    }
}

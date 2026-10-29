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
/// (Punch +0, Kick -2, Push -1); standard attacker movement, target movement and terrain
/// modifiers apply, as do target-side modifiers (partial cover, skidding) and the
/// attacker-side skidding modifier. Heat and sensor modifiers are never applied.
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
        var scenario = PhysicalAttackScenario.FromUnits(attacker, target, attackType);
        return GetModifierBreakdown(scenario, map).Total;
    }

    public PhysicalAttackBreakdown GetModifierBreakdown(IUnit attacker, IUnit target, PhysicalAttackType attackType, IBattleMap map)
    {
        var scenario = PhysicalAttackScenario.FromUnits(attacker, target, attackType);
        return GetModifierBreakdown(scenario, map);
    }

    public int GetToHitNumber(PhysicalAttackScenario scenario, IBattleMap map)
    {
        return GetModifierBreakdown(scenario, map).Total;
    }

    public PhysicalAttackBreakdown GetModifierBreakdown(PhysicalAttackScenario scenario, IBattleMap map)
    {
        var losResult = map.GetLineOfSight(
            scenario.AttackerPosition.Coordinates,
            scenario.TargetPosition.Coordinates,
            scenario.AttackerHeight,
            scenario.TargetHeight,
            scenario.AttackerPosition.Surface,
            scenario.TargetPosition.Surface);

        var terrainModifiers = losResult.GetTerrainToHitModifiers(_rules);

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

    /// <summary>
    /// A push adds +2 for each damaged shoulder actuator across the attacker's arms. It is not a
    /// blocker: a Mech with a destroyed shoulder can still push (Total Warfare, p. 148).
    /// </summary>
    private static void AddPushShoulderModifier(PhysicalAttackScenario scenario, List<RollModifier> modifiers)
    {
        if (scenario.AttackType != PhysicalAttackType.Push || scenario.AttackerUnit is not { } attacker) return;

        PartLocation[] arms = [PartLocation.LeftArm, PartLocation.RightArm];
        var damagedShoulders = arms.Count(arm =>
            attacker.GetComponentsAtLocation<ShoulderActuator>(arm).Any(component => component.IsDestroyed));
        if (damagedShoulders == 0) return;

        modifiers.Add(new DamagedShoulderModifier
        {
            Value = DamagedShoulderModifier.ModifierPerDamagedShoulder * damagedShoulders,
            DamagedShoulders = damagedShoulders
        });
    }

    /// <summary>
    /// Collects the target-side modifiers that apply to physical attacks.
    /// Partial cover (+1) and skidding targets (+2) apply to all attacks,
    /// weapon and physical alike.
    /// </summary>
    private List<RollModifier> GetTargetModifiers(
        PhysicalAttackScenario scenario,
        LineOfSightResult losResult)
    {
        List<RollModifier> modifiers = [];

        if (scenario.TargetUnit is null) return modifiers;

        if (_rules.HasPartialCover(scenario.TargetUnit, losResult))
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

        return modifiers;
    }
}

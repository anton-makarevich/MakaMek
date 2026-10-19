using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Punch and kick eligibility per docs/rules/physical-attacks.md section 3 and the physical attacks
/// PRD (Background items 9 and 10, R1.4). The elevation limits follow the rules extract posted on
/// issue #1504.
/// </summary>
public class PhysicalAttackEligibility : IPhysicalAttackEligibility
{
    private static readonly PhysicalAttackType[] SupportedAttackTypes =
        [PhysicalAttackType.Punch, PhysicalAttackType.Kick];

    public IReadOnlyList<PartLocation> GetEligibleLimbs(IUnit attacker, IUnit target, PhysicalAttackType attackType,
        IBattleMap map)
    {
        if (attacker is not Mech mech || !CanMakePhysicalAttack(mech) || !IsValidTarget(mech, target)
            || !IsWithinReach(mech, (Mech)target, attackType, map))
            return [];

        return attackType switch
        {
            PhysicalAttackType.Punch => GetPunchingArms(mech, target),
            PhysicalAttackType.Kick => GetKickingLegs(mech, target),
            _ => []
        };
    }

    public bool HasAnyOption(IUnit attacker, IEnumerable<IUnit> units, IBattleMap map) =>
        units.Any(target => SupportedAttackTypes.Any(type => GetEligibleLimbs(attacker, target, type, map).Count > 0));

    /// <summary>
    /// A standing Mech that has not made its one physical attack this turn. A skidding Mech may
    /// still attack; it takes a to-hit penalty instead.
    /// </summary>
    private static bool CanMakePhysicalAttack(Mech attacker) =>
        attacker is { IsDestroyed: false, IsShutdown: false, IsProne: false, Position: not null }
        && !attacker.HasDeclaredPhysicalAttack;

    /// <summary>
    /// An enemy Mech that is still standing on the map in an adjacent hex.
    /// </summary>
    private static bool IsValidTarget(Mech attacker, IUnit target) =>
        target is Mech { IsDestroyed: false, Position: not null } targetMech
        && targetMech.Owner?.Id != attacker.Owner?.Id
        && attacker.Position!.Coordinates.DistanceTo(targetMech.Position.Coordinates) == 1;

    /// <summary>
    /// The two hexes have to be within one level of each other. A punch cannot reach a target one
    /// level lower. A kick cannot reach a target one level higher, and reaches a target one level
    /// lower only when that target is standing.
    /// </summary>
    private static bool IsWithinReach(Mech attacker, Mech target, PhysicalAttackType attackType, IBattleMap map)
    {
        var attackerHex = map.GetHex(attacker.Position!.Coordinates);
        var targetHex = map.GetHex(target.Position!.Coordinates);
        if (attackerHex == null || targetHex == null) return false;

        var targetIsHigherBy = targetHex.GetLevelDifference(attackerHex);
        return attackType switch
        {
            PhysicalAttackType.Punch => targetIsHigherBy is 0 or 1,
            PhysicalAttackType.Kick => targetIsHigherBy == 0 || (targetIsHigherBy == -1 && !target.IsProne),
            _ => false
        };
    }

    /// <summary>
    /// Punch arcs follow the torso: both arms reach the forward arc, each arm only its own side arc.
    /// An arm can punch if its shoulder actuator is intact and none of its weapons fired this turn.
    /// </summary>
    private static IReadOnlyList<PartLocation> GetPunchingArms(Mech attacker, IUnit target)
    {
        var facing = attacker.Facing ?? attacker.Position!.Facing;
        var arc = attacker.Position!.Coordinates.GetFiringArc(target.Position!.Coordinates, facing);
        PartLocation[] arms = arc switch
        {
            FiringArc.Front => [PartLocation.LeftArm, PartLocation.RightArm],
            FiringArc.Left => [PartLocation.LeftArm],
            FiringArc.Right => [PartLocation.RightArm],
            _ => []
        };

        var firedFrom = attacker.GetFiredWeaponLocations();
        return arms
            .Where(arm => !firedFrom.Contains(arm) && IsIntact<ShoulderActuator>(attacker, arm))
            .ToList();
    }

    /// <summary>
    /// Kicks follow the feet and reach only the three forward hexes. Both hip actuators have to be
    /// intact; each leg can kick if none of its weapons fired this turn.
    /// </summary>
    private static IReadOnlyList<PartLocation> GetKickingLegs(Mech attacker, IUnit target)
    {
        var arc = attacker.Position!.Coordinates.GetFiringArc(target.Position!.Coordinates, attacker.Position.Facing);
        PartLocation[] legs = [PartLocation.LeftLeg, PartLocation.RightLeg];
        if (arc != FiringArc.Front || !legs.All(leg => IsIntact<HipActuator>(attacker, leg)))
            return [];

        var firedFrom = attacker.GetFiredWeaponLocations();
        return legs.Where(leg => !firedFrom.Contains(leg)).ToList();
    }

    private static bool IsIntact<TActuator>(Mech mech, PartLocation location) where TActuator : Component =>
        mech.Parts.TryGetValue(location, out var part)
        && !part.IsDestroyed
        && part.GetComponent<TActuator>() is { IsDestroyed: false };
}

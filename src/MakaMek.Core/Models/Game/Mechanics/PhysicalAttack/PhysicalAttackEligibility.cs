using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Punch and kick eligibility per docs/rules/physical-attacks.md section 3 and the physical attacks
/// PRD (Background items 9 and 10, R1.4). The elevation limits follow the corrected Different-Levels
/// table posted on issue #1504.
/// </summary>
public class PhysicalAttackEligibility : IPhysicalAttackEligibility
{
    private static readonly PhysicalAttackType[] SupportedAttackTypes =
        [PhysicalAttackType.Punch, PhysicalAttackType.Kick];

    public IReadOnlyList<PartLocation> GetEligibleLimbs(IUnit attacker, IUnit target, PhysicalAttackType attackType,
        IBattleMap map) =>
        attacker is Mech { HasDeclaredPhysicalAttack: false } ? GetReachingLimbs(attacker, target, attackType, map) : [];

    public bool CanResolve(IUnit attacker, IUnit target, PhysicalAttackType attackType, PartLocation limb,
        IBattleMap map) =>
        GetReachingLimbs(attacker, target, attackType, map).Contains(limb);

    public bool HasAnyOption(IUnit attacker, IEnumerable<IUnit> units, IBattleMap map) =>
        units.Any(target => SupportedAttackTypes.Any(type => GetEligibleLimbs(attacker, target, type, map).Count > 0));

    /// <summary>
    /// The limbs that can make this attack against this target, leaving aside whether the attacker
    /// has already declared its physical attack this turn.
    /// </summary>
    private static IReadOnlyList<PartLocation> GetReachingLimbs(IUnit attacker, IUnit target,
        PhysicalAttackType attackType, IBattleMap map)
    {
        if (attacker is not Mech mech || !CanMakePhysicalAttack(mech) || !IsValidTarget(mech, target)
            || GetEffectiveLevelDifference(mech, target, map) is not { } levelDifference)
            return [];

        var isTargetProne = ((Mech)target).IsProne;
        return attackType switch
        {
            PhysicalAttackType.Punch when levelDifference == 1 || (levelDifference == 0 && !isTargetProne)
                => GetPunchingArms(mech, target),
            PhysicalAttackType.Kick when levelDifference == 0 || (levelDifference == -1 && !isTargetProne)
                => GetKickingLegs(mech, target),
            _ => []
        };
    }

    /// <summary>
    /// A standing Mech that is still in the game. A skidding Mech may still attack; it takes a
    /// to-hit penalty instead.
    /// </summary>
    private static bool CanMakePhysicalAttack(Mech attacker) =>
        attacker is { IsDestroyed: false, IsShutdown: false, IsProne: false, Position: not null };

    /// <summary>
    /// An enemy Mech that is still standing on the map in an adjacent hex.
    /// </summary>
    private static bool IsValidTarget(Mech attacker, IUnit target) =>
        target is Mech { IsDestroyed: false, Position: not null } targetMech
        && targetMech.Owner?.Id != attacker.Owner?.Id
        && attacker.Position!.Coordinates.DistanceTo(targetMech.Position.Coordinates) == 1;

    /// <summary>
    /// How many levels the target is above the attacker, or null when either hex is missing from the
    /// map. A prone Mech rises one level above its hex, so against a prone target the difference is
    /// one higher than the raw hex difference; the height difference of the two units carries that.
    /// Standing target: 0 allows a punch and a kick, +1 a punch, -1 a kick.
    /// Prone target: +1 allows a punch, 0 a kick, anything else nothing.
    /// </summary>
    private static int? GetEffectiveLevelDifference(Mech attacker, IUnit target, IBattleMap map)
    {
        var attackerHex = map.GetHex(attacker.Position!.Coordinates);
        var targetHex = map.GetHex(target.Position!.Coordinates);
        if (attackerHex == null || targetHex == null) return null;

        return targetHex.GetLevelDifference(attackerHex) - (target.Height - attacker.Height);
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

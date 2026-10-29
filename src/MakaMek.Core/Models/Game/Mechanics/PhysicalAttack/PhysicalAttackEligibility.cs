using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Punch, kick and push eligibility per docs/rules/physical-attacks.md sections 3 and 4 and the
/// physical attacks PRD (Background items 9 and 10, R1.4). The elevation limits follow the
/// corrected Different-Levels table posted on issue #1504; level comparisons use effective
/// standing levels and the water eligibility rules follow Total Warfare p.109 and p.151
/// (issue #1600).
/// <para>
/// This is the default eligibility policy. It owns the rules that depend on the current board
/// state:
/// </para>
/// <list type="bullet">
/// <item>the punch and kick level-difference table, including the prone-target variants;</item>
/// <item>punch arcs following the torso and kick arcs following the feet;</item>
/// <item>the shoulder and hip actuator prerequisites;</item>
/// <item>the per-limb fired-weapon restrictions;</item>
/// <item>the one physical attack per turn rule (applied by <see cref="GetEligibleLimbs"/>, skipped
/// by <see cref="CanResolve"/>);</item>
/// <item>the target checks: an enemy, standing, adjacent Mech.</item>
/// </list>
/// <para>
/// Which attacks exist and which limbs they use is structural metadata in
/// <see cref="PhysicalAttackDefinitions"/>, not policy. The level table is intentionally not
/// extracted to the rules layer yet, and no separate eligibility rules interface is introduced:
/// replacing the <see cref="IPhysicalAttackEligibility"/> registration already makes the policy
/// replaceable.
/// </para>
/// </summary>
public class PhysicalAttackEligibility : IPhysicalAttackEligibility
{
    public IReadOnlyList<PartLocation> GetEligibleLimbs(IUnit attacker, IUnit target, PhysicalAttackType attackType,
        IBattleMap map) =>
        attacker is Mech { HasDeclaredPhysicalAttack: false } ? GetReachingLimbs(attacker, target, attackType, map) : [];

    public bool CanResolve(IUnit attacker, IUnit target, PhysicalAttackType attackType, PartLocation limb,
        IBattleMap map) =>
        GetReachingLimbs(attacker, target, attackType, map).Contains(limb);

    public bool HasAnyOption(IUnit attacker, IEnumerable<IUnit> units, IBattleMap map) =>
        units.Any(target => PhysicalAttackDefinitions.Supported.Any(definition =>
            GetEligibleLimbs(attacker, target, definition.AttackType, map).Count > 0));

    /// <summary>
    /// The limbs that can make this attack against this target, leaving aside whether the attacker
    /// has already declared its physical attack this turn.
    /// </summary>
    private static IReadOnlyList<PartLocation> GetReachingLimbs(IUnit attacker, IUnit target,
        PhysicalAttackType attackType, IBattleMap map)
    {
        if (attacker is not Mech mech || !CanMakePhysicalAttack(mech) || !IsValidTarget(mech, target)
            || GetEndpoint(mech, map) is not { } attackerEndpoint
            || GetEndpoint(target, map) is not { } targetEndpoint)
            return [];

        // Total Warfare p.151 (Water): a physical attack against a submerged unit must begin
        // underwater. A Mech standing in Depth 1 water keeps torso and arms above the surface, so
        // only a kick occurs entirely underwater; in Depth 2+ water the whole Mech is underwater
        // and any supported attack qualifies (issue #1600). Push also uses the arms, so it needs
        // the same Depth 1 restriction as a punch.
        if (targetEndpoint.IsSubmerged
            && (!attackerEndpoint.IsInWater
                || (!attackerEndpoint.IsSubmerged
                    && attackType is PhysicalAttackType.Punch or PhysicalAttackType.Push)))
            return [];

        var levelDifference = GetEffectiveLevelDifference(
            attackerEndpoint, targetEndpoint, attacker.Height, target.Height);
        var isTargetProne = ((Mech)target).IsProne;
        return attackType switch
        {
            PhysicalAttackType.Punch when levelDifference == 1 || (levelDifference == 0 && !isTargetProne)
                => GetPunchingArms(mech, target),
            PhysicalAttackType.Kick when levelDifference == 0 || (levelDifference == -1 && !isTargetProne)
                => GetKickingLegs(mech, target),
            PhysicalAttackType.Push => GetPushRestriction(mech, target, attackerEndpoint, targetEndpoint) is null
                ? [PartLocation.LeftArm, PartLocation.RightArm]
                : [],
            _ => []
        };
    }

    /// <summary>
    /// Reports why a Push cannot be made, or null when it can. The target has to be a standing
    /// Mech in the hex directly in front of the attacker's feet, at the exact same elevation, and
    /// the attacker needs both arms with no arm-mounted weapon fired this turn (Total Warfare,
    /// p. 148). A damaged shoulder actuator is not a blocker.
    /// </summary>
    public PhysicalAttackRestriction? GetRestriction(IUnit attacker, IUnit target,
        PhysicalAttackType attackType, IBattleMap map)
    {
        // Only Push exposes availability reasons so far; Punch and Kick stay hidden when unavailable.
        if (attackType != PhysicalAttackType.Push) return null;

        if (attacker is not Mech mech || !CanMakePhysicalAttack(mech) || mech.Position is null)
            return PhysicalAttackRestriction.NoTargetInFront;

        if (target.Position is null || target.IsDestroyed || target.Owner?.Id == attacker.Owner?.Id
            || !IsInFeetFrontHex(mech, target))
            return PhysicalAttackRestriction.NoTargetInFront;

        if (GetEndpoint(mech, map) is not { } attackerEndpoint
            || GetEndpoint(target, map) is not { } targetEndpoint)
            return PhysicalAttackRestriction.NoTargetInFront;

        return GetPushRestriction(mech, target, attackerEndpoint, targetEndpoint);
    }

    private static PhysicalAttackRestriction? GetPushRestriction(
        Mech attacker, IUnit target, UnitEndpoint attackerEndpoint, UnitEndpoint targetEndpoint)
    {
        if (!IsInFeetFrontHex(attacker, target))
            return PhysicalAttackRestriction.NoTargetInFront;

        if (target is not Mech { IsProne: false })
            return PhysicalAttackRestriction.TargetNotStandingMech;

        if (target.DeclaredPhysicalAttack?.AttackType is PhysicalAttackType.Charge or PhysicalAttackType.DFA)
            return PhysicalAttackRestriction.TargetPerformingDisplacementAttack;

        if (targetEndpoint.EffectiveStandingLevel != attackerEndpoint.EffectiveStandingLevel)
            return PhysicalAttackRestriction.ElevationMismatch;

        PartLocation[] arms = [PartLocation.LeftArm, PartLocation.RightArm];
        if (arms.Any(arm => !attacker.Parts.TryGetValue(arm, out var part) || part.IsDestroyed))
            return PhysicalAttackRestriction.ArmMissing;

        var firedFrom = attacker.GetFiredWeaponLocations();
        return arms.Any(firedFrom.Contains) ? PhysicalAttackRestriction.ArmWeaponFired : null;
    }

    /// <summary>
    /// The target must occupy the single hex directly in front of the attacker, using the feet
    /// orientation rather than the torso twist.
    /// </summary>
    private static bool IsInFeetFrontHex(Mech attacker, IUnit target) =>
        target.Position!.Coordinates
            == attacker.Position!.Coordinates.GetNeighbour(attacker.Position.Facing);

    /// <summary>
    /// Where a unit stands relative to the water line, resolved from the current map. The stored
    /// Unit.Hex is deliberately not used because replacing a map hex does not update it.
    /// </summary>
    private sealed record UnitEndpoint(int EffectiveStandingLevel, bool IsSubmerged, bool IsInWater);

    /// <summary>
    /// The effective standing level and submersion of one unit (Total Warfare p.109: water counts
    /// as negative levels). A unit is in water when it stands on the ground surface of a hex with
    /// water deeper than shallow; a unit on a bridge is never in water.
    /// </summary>
    private static UnitEndpoint? GetEndpoint(IUnit unit, IBattleMap map)
    {
        var hex = map.GetHex(unit.Position!.Coordinates);
        if (hex == null) return null;

        var surface = unit.Position.Surface;
        var (effectiveStandingLevel, isSubmerged) = hex.GetLosEndpointInfo(surface, unit.Height);
        return new UnitEndpoint(
            effectiveStandingLevel,
            isSubmerged,
            surface == HexSurface.Ground && hex.GetWaterDepth() is > 0);
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
    /// How many levels the target stands above the attacker, computed from the effective standing
    /// levels of both units so water counts as negative levels (Total Warfare p.109, issue #1600).
    /// A prone Mech rises one level above its hex, so against a prone target the difference is
    /// one higher than the raw hex difference; the height difference of the two units carries that.
    /// Standing target: 0 allows a punch and a kick, +1 a punch, -1 a kick.
    /// Prone target: +1 allows a punch, 0 a kick, anything else nothing.
    /// </summary>
    private static int GetEffectiveLevelDifference(UnitEndpoint attackerEndpoint, UnitEndpoint targetEndpoint,
        int attackerHeight, int targetHeight) =>
        targetEndpoint.EffectiveStandingLevel - attackerEndpoint.EffectiveStandingLevel
        - (targetHeight - attackerHeight);

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

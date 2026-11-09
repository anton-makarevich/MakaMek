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

    public IReadOnlyList<PhysicalAttackLimbEligibility> GetLimbEligibility(IUnit attacker, IUnit target,
        PhysicalAttackType attackType, IBattleMap map) =>
        attacker is Mech { HasDeclaredPhysicalAttack: false }
            ? Evaluate(attacker, target, attackType, map).LimbEligibility
            : [];

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
        PhysicalAttackType attackType, IBattleMap map) =>
        Evaluate(attacker, target, attackType, map).Limbs;

    /// <summary>
    /// Reports why this attack cannot be made, or null when it can. Every supported attack type
    /// reports a reason, and a null reason always means the reaching limbs are not empty. The
    /// displacement budget is checked separately by <see cref="GetDeclarationRestriction"/>.
    /// </summary>
    public PhysicalAttackRestriction? GetRestriction(IUnit attacker, IUnit target,
        PhysicalAttackType attackType, IBattleMap map) =>
        Evaluate(attacker, target, attackType, map).Restriction;

    /// <summary>
    /// Reports why this attack cannot be declared right now. Applies the intrinsic eligibility
    /// rules first and then, only for displacement attacks, the shared one-displacement-per-target
    /// budget. Resolution paths use <see cref="CanResolve"/> instead, because a declaration that
    /// reserved the target must still be able to resolve.
    /// </summary>
    public PhysicalAttackRestriction? GetDeclarationRestriction(IUnit attacker, IUnit target,
        PhysicalAttackType attackType, IBattleMap map, IEnumerable<IUnit> units)
    {
        var restriction = GetRestriction(attacker, target, attackType, map);
        if (restriction is not null) return restriction;

        return PhysicalAttackDefinitions.IsDisplacementAttack(attackType)
               && PhysicalAttackDisplacementBudget.IsReserved(target.Id, units)
            ? PhysicalAttackRestriction.TargetAlreadyDisplaced
            : null;
    }

    /// <summary>
    /// One evaluation of an attack: the first failing restriction, the limbs that can still make
    /// it and the reason for every candidate limb. A null restriction always pairs with at least
    /// one reaching limb.
    /// </summary>
    private sealed record Evaluation(
        PhysicalAttackRestriction? Restriction,
        IReadOnlyList<PartLocation> Limbs,
        IReadOnlyList<PhysicalAttackLimbEligibility> LimbEligibility)
    {
        public static Evaluation Fail(PhysicalAttackRestriction restriction,
            IReadOnlyList<PartLocation> candidateLimbs)
        {
            var limbEligibility = candidateLimbs
                .Select(limb => new PhysicalAttackLimbEligibility(limb, restriction))
                .ToList();
            return new(restriction, [], limbEligibility);
        }

        public static Evaluation Create(IReadOnlyList<PhysicalAttackLimbEligibility> limbEligibility,
            PhysicalAttackRestriction? restriction)
        {
            var limbs = limbEligibility
                .Where(limb => limb.IsAvailable)
                .Select(limb => limb.Limb)
                .ToList();
            return new(restriction, limbs, limbEligibility);
        }
    }

    private static IReadOnlyList<PartLocation> GetCandidateLimbs(PhysicalAttackType attackType) =>
        PhysicalAttackDefinitions.TryGet(attackType, out var definition) ? definition.CandidateLimbs : [];

    private static Evaluation Evaluate(IUnit attacker, IUnit target, PhysicalAttackType attackType,
        IBattleMap map)
    {
        var candidates = GetCandidateLimbs(attackType);
        return attackType switch
        {
            PhysicalAttackType.Punch or PhysicalAttackType.Kick =>
                EvaluateMelee(attacker, target, attackType, map, candidates),
            PhysicalAttackType.Push => EvaluatePush(attacker, target, map, candidates),
            _ => Evaluation.Fail(PhysicalAttackRestriction.TargetOutOfReach, candidates)
        };
    }

    /// <summary>
    /// Punch and Kick share the generic checks, the water rule and the level-difference table, in
    /// that order. The first failure wins.
    /// </summary>
    private static Evaluation EvaluateMelee(IUnit attacker, IUnit target, PhysicalAttackType attackType,
        IBattleMap map, IReadOnlyList<PartLocation> candidates)
    {
        if (attacker is not Mech mech || !CanMakePhysicalAttack(mech) || !IsValidTarget(mech, target)
            || GetEndpoint(mech, map) is not { } attackerEndpoint
            || GetEndpoint(target, map) is not { } targetEndpoint)
            return Evaluation.Fail(PhysicalAttackRestriction.TargetOutOfReach, candidates);

        if (IsBlockedByWater(attackerEndpoint, targetEndpoint, attackType))
            return Evaluation.Fail(PhysicalAttackRestriction.TargetSubmerged, candidates);

        var levelDifference = GetEffectiveLevelDifference(
            attackerEndpoint, targetEndpoint, attacker.Height, target.Height);
        var levelRestriction = GetLevelRestriction(attackType, levelDifference, ((Mech)target).IsProne);
        if (levelRestriction is not null)
            return Evaluation.Fail(levelRestriction.Value, candidates);

        return attackType switch
        {
            PhysicalAttackType.Punch => EvaluatePunchLimbs(mech, target, candidates),
            PhysicalAttackType.Kick => EvaluateKickLimbs(mech, target, candidates),
            _ => Evaluation.Fail(PhysicalAttackRestriction.TargetOutOfReach, candidates)
        };
    }

    /// <summary>
    /// The Different-Levels table. A standing target: 0 allows a punch and a kick, +1 a punch, -1
    /// a kick. A prone target shifts the table by one because the prone height is carried by the
    /// effective level difference: +1 allows a punch, 0 a kick, anything else nothing.
    /// </summary>
    private static PhysicalAttackRestriction? GetLevelRestriction(PhysicalAttackType attackType,
        int levelDifference, bool isTargetProne) =>
        attackType switch
        {
            PhysicalAttackType.Punch when levelDifference == 1
                                          || (levelDifference == 0 && !isTargetProne) => null,
            PhysicalAttackType.Punch when levelDifference == 0 => PhysicalAttackRestriction.TargetProne,
            PhysicalAttackType.Punch => PhysicalAttackRestriction.LevelDifferenceOutOfRange,
            PhysicalAttackType.Kick when levelDifference == 0
                                         || (levelDifference == -1 && !isTargetProne) => null,
            PhysicalAttackType.Kick when levelDifference == -1 => PhysicalAttackRestriction.TargetProne,
            PhysicalAttackType.Kick => PhysicalAttackRestriction.LevelDifferenceOutOfRange,
            _ => PhysicalAttackRestriction.TargetOutOfReach
        };

    /// <summary>
    /// Punch arcs follow the torso: both arms reach the forward arc, each arm only its own side arc.
    /// An arm can punch if it is intact, its shoulder actuator is intact and none of its weapons
    /// fired this turn. When no permitted arm passes, the reason of the first permitted arm in
    /// candidate-limb order is reported.
    /// </summary>
    private static Evaluation EvaluatePunchLimbs(Mech attacker, IUnit target,
        IReadOnlyList<PartLocation> candidates)
    {
        var facing = attacker.Facing ?? attacker.Position!.Facing;
        var arc = attacker.Position!.Coordinates.GetFiringArc(target.Position!.Coordinates, facing);
        PartLocation[] armsInArc = arc switch
        {
            FiringArc.Front => [PartLocation.LeftArm, PartLocation.RightArm],
            FiringArc.Left => [PartLocation.LeftArm],
            FiringArc.Right => [PartLocation.RightArm],
            _ => []
        };

        var firedFrom = attacker.GetFiredWeaponLocations();
        var limbEligibility = candidates
            .Select(arm => new PhysicalAttackLimbEligibility(
                arm,
                armsInArc.Contains(arm)
                    ? GetArmRestriction(attacker, arm, firedFrom)
                    : PhysicalAttackRestriction.TargetOutsideArc))
            .ToList();

        if (limbEligibility.Any(limb => limb.IsAvailable))
            return Evaluation.Create(limbEligibility, null);

        // Keep the row reason of a fully blocked punch identical to the pre-limb-details
        // behavior: the first in-arc arm's failure, or the arc itself when no arm reaches.
        var firstInArcFailure = limbEligibility
            .Where(limb => armsInArc.Contains(limb.Limb))
            .Select(limb => limb.Restriction)
            .FirstOrDefault(restriction => restriction is not null);
        return Evaluation.Create(limbEligibility,
            firstInArcFailure ?? PhysicalAttackRestriction.TargetOutsideArc);
    }

    private static PhysicalAttackRestriction? GetArmRestriction(Mech attacker, PartLocation arm,
        IReadOnlyCollection<PartLocation> firedFrom)
    {
        if (!IsPartIntact(attacker, arm))
            return PhysicalAttackRestriction.ArmDestroyed;

        return IsIntact<ShoulderActuator>(attacker, arm)
            ? firedFrom.Contains(arm) ? PhysicalAttackRestriction.ArmWeaponFired : null
            : PhysicalAttackRestriction.ShoulderActuatorDestroyed;
    }

    /// <summary>
    /// Kicks follow the feet and reach only the three forward hexes. Both legs and both hip
    /// actuators have to be intact; each leg can kick if none of its weapons fired this turn.
    /// </summary>
    private static Evaluation EvaluateKickLimbs(Mech attacker, IUnit target,
        IReadOnlyList<PartLocation> candidates)
    {
        var arc = attacker.Position!.Coordinates.GetFiringArc(
            target.Position!.Coordinates, attacker.Position.Facing);
        if (arc != FiringArc.Front)
            return Evaluation.Fail(PhysicalAttackRestriction.TargetOutsideArc, candidates);

        // A kick needs both legs intact, so either failing leg blocks the whole attack and every
        // candidate leg reports the same reason.
        if (candidates.Any(leg => !IsPartIntact(attacker, leg)))
            return Evaluation.Fail(PhysicalAttackRestriction.LegDestroyed, candidates);

        if (candidates.Any(leg => !IsIntact<HipActuator>(attacker, leg)))
            return Evaluation.Fail(PhysicalAttackRestriction.HipActuatorDestroyed, candidates);

        var firedFrom = attacker.GetFiredWeaponLocations();
        var limbEligibility = candidates
            .Select(leg => new PhysicalAttackLimbEligibility(
                leg,
                firedFrom.Contains(leg) ? PhysicalAttackRestriction.LegWeaponFired : null))
            .ToList();

        return Evaluation.Create(limbEligibility,
            limbEligibility.Any(limb => limb.IsAvailable)
                ? null
                : PhysicalAttackRestriction.LegWeaponFired);
    }

    private static Evaluation EvaluatePush(IUnit attacker, IUnit target, IBattleMap map,
        IReadOnlyList<PartLocation> candidates)
    {
        if (attacker is not Mech mech || !CanMakePhysicalAttack(mech))
            return Evaluation.Fail(PhysicalAttackRestriction.NoTargetInFront, candidates);

        if (target.Position is null || target.IsDestroyed || target.Owner?.Id == attacker.Owner?.Id
            || !IsInFeetFrontHex(mech, target))
            return Evaluation.Fail(PhysicalAttackRestriction.NoTargetInFront, candidates);

        if (GetEndpoint(mech, map) is not { } attackerEndpoint
            || GetEndpoint(target, map) is not { } targetEndpoint)
            return Evaluation.Fail(PhysicalAttackRestriction.NoTargetInFront, candidates);

        // The same water rule that removes the limbs has to report a reason here, otherwise the
        // UI offers a Push that cannot be selected.
        if (IsBlockedByWater(attackerEndpoint, targetEndpoint, PhysicalAttackType.Push))
            return Evaluation.Fail(PhysicalAttackRestriction.TargetSubmerged, candidates);

        var restriction = GetPushRestriction(mech, target, attackerEndpoint, targetEndpoint);
        if (restriction is not null)
            return Evaluation.Fail(restriction.Value, candidates);

        return Evaluation.Create(
            candidates
                .Select(limb => new PhysicalAttackLimbEligibility(limb, null))
                .ToList(),
            null);
    }

    /// <summary>
    /// Total Warfare p.151 (Water): a physical attack against a submerged unit must begin
    /// underwater. A Mech standing in Depth 1 water keeps torso and arms above the surface, so
    /// only a kick occurs entirely underwater; in Depth 2+ water the whole Mech is underwater
    /// and any supported attack qualifies (issue #1600). Push also uses the arms, so it needs
    /// the same Depth 1 restriction as a punch.
    /// </summary>
    private static bool IsBlockedByWater(UnitEndpoint attackerEndpoint, UnitEndpoint targetEndpoint,
        PhysicalAttackType attackType) =>
        targetEndpoint.IsSubmerged
        && (!attackerEndpoint.IsInWater
            || (!attackerEndpoint.IsSubmerged
                && attackType is PhysicalAttackType.Punch or PhysicalAttackType.Push));

    /// <summary>
    /// Reports why a Push cannot be made, or null when it can. The target has to be a standing
    /// Mech in the hex directly in front of the attacker's feet, at the exact same elevation, and
    /// the attacker needs both arms with no arm-mounted weapon fired this turn (Total Warfare,
    /// p. 148). A damaged shoulder actuator is not a blocker.
    /// </summary>
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
    /// Physical reach stays grounded for a pending DFA attacker: only line of sight uses its
    /// airborne level (docs/rules/physical-attacks.md).
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

    private static bool IsPartIntact(Mech mech, PartLocation location) =>
        mech.Parts.TryGetValue(location, out var part) && !part.IsDestroyed;

    private static bool IsIntact<TActuator>(Mech mech, PartLocation location) where TActuator : Component =>
        mech.Parts.TryGetValue(location, out var part)
        && !part.IsDestroyed
        && part.GetComponent<TActuator>() is { IsDestroyed: false };
}

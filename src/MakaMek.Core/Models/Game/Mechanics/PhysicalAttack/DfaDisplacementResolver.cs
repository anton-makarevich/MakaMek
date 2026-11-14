using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Where a successful Death From Above moves the target and the attacker. A null
/// <see cref="TargetDestination"/> means the target does not move; the attacker still lands in the
/// target's hex.
/// </summary>
/// <param name="TargetDestination">
/// The hex the target is displaced to, or null when it does not move. A null destination with a
/// surviving target means the displacement itself destroys the target.
/// </param>
/// <param name="AttackerDestination">The hex the attacker lands in.</param>
/// <param name="LevelsFallen">How many standing levels the target drops.</param>
/// <param name="TriggersAutomaticFall">
/// Whether the target's drop exceeds the deepest displacement drop that does not force a fall.
/// </param>
/// <param name="DominoEffect">
/// The resolved domino chain when the target's ideal hex was occupied. Its commands must be applied
/// before the target moves into the vacated hex.
/// </param>
public record DfaDisplacementResult(
    HexPosition? TargetDestination,
    HexPosition AttackerDestination,
    int LevelsFallen,
    bool TriggersAutomaticFall,
    DominoEffectResult? DominoEffect = null);

/// <summary>
/// One legal adjacent hex the target of a missed DFA can be displaced into.
/// </summary>
/// <param name="Coordinates">The destination coordinates.</param>
/// <param name="Surface">The resolved surface (ground or a bridge deck).</param>
/// <param name="LevelsFallen">How many standing levels the target drops.</param>
/// <param name="TriggersAutomaticFall">
/// Whether the drop exceeds the deepest displacement drop that does not force a fall.
/// </param>
public record DfaMissDisplacementCandidate(
    HexCoordinates Coordinates,
    HexSurface Surface,
    int LevelsFallen = 0,
    bool TriggersAutomaticFall = false);

/// <summary>
/// Where a DFA attacker lands, and the domino chain that may have to run before it can.
/// </summary>
/// <param name="Destination">The final landing position.</param>
/// <param name="DominoEffect">The chain that vacates the landing hex, if any.</param>
/// <param name="IsOverlapUnresolved">
/// True when the landing hex is occupied and the domino effect could not vacate it.
/// </param>
public record DfaLandingResult(
    HexPosition Destination,
    DominoEffectResult? DominoEffect = null,
    bool IsOverlapUnresolved = false);

/// <summary>
/// Applies the Death From Above displacement policy from Total Warfare p. 150.
/// <para>
/// The push vector is measured from the hex where the jumping 'Mech started its movement to the
/// target hex, not from the airborne approach hex. The target is displaced one hex further along
/// that line. If the ideal hex is impassable, off the map or prohibited, the target moves into the
/// adjacent open hex closest to the vector. An occupied ideal hex triggers the shared domino
/// effect.
/// </para>
/// <para>
/// The attacker lands in the target's original hex on a hit and on a miss, and in the declared target
/// hex on an auto-fail: the jump destination is always that hex. An occupied landing hex is resolved
/// through the same domino effect; a DFA has no side-hex fallback, so a blocked chain leaves no legal
/// landing (Total Warfare, p. 150).
/// </para>
/// </summary>
public class DfaDisplacementResolver(
    IRulesProvider rulesProvider,
    DominoEffectResolver dominoEffectResolver)
{
    /// <summary>
    /// The displacement of a successful DFA, or null when the units are not deployed.
    /// </summary>
    public DfaDisplacementResult? ResolveHit(
        IUnit attacker,
        IUnit target,
        HexCoordinates startCoordinates,
        IBattleMap map,
        IEnumerable<IUnit> units,
        Guid gameOriginId = default)
    {
        if (attacker.Position is not { } attackerPosition || target.Position is not { } targetPosition)
            return null;

        var allUnits = units.ToList();
        var attackerDestination = new HexPosition(
            targetPosition.Coordinates,
            attackerPosition.Facing,
            targetPosition.Surface);

        // A target destroyed before or on impact does not move; the attacker lands in its hex.
        if (target.IsDestroyed)
            return new DfaDisplacementResult(null, attackerDestination, 0, false);

        var destination = FindTargetDestination(
            target,
            targetPosition,
            startCoordinates,
            map,
            allUnits,
            gameOriginId);

        if (destination is null)
        {
            // A target that cannot be displaced is destroyed by the caller so the attacker can
            // still land in its hex (Total Warfare, p. 150).
            return new DfaDisplacementResult(null, attackerDestination, 0, false);
        }

        var maxDrop = rulesProvider.GetMaxDisplacementDropWithoutAutomaticFall();
        var targetLevelsFallen = GetLevelsFallen(
            map, targetPosition, destination.Coordinates, destination.Surface);

        return new DfaDisplacementResult(
            new HexPosition(
                destination.Coordinates,
                targetPosition.Facing,
                destination.Surface),
            attackerDestination,
            targetLevelsFallen,
            targetLevelsFallen > maxDrop,
            destination.Domino);
    }

    private sealed record TargetDestination(
        HexCoordinates Coordinates,
        HexSurface Surface,
        DominoEffectResult? Domino);

    /// <summary>
    /// Finds the target's displacement hex: the ideal hex along the jump line, or the closest
    /// adjacent open hex when the ideal one is off the map or impassable. An occupied ideal hex
    /// resolves through the domino effect.
    /// </summary>
    private TargetDestination? FindTargetDestination(
        IUnit target,
        HexPosition targetPosition,
        HexCoordinates startCoordinates,
        IBattleMap map,
        IReadOnlyList<IUnit> allUnits,
        Guid gameOriginId)
    {
        var allDirections = targetPosition.Coordinates.GetDirectionsByAlignmentAwayFrom(startCoordinates);
        var idealDirection = allDirections[0];

        foreach (var direction in allDirections)
        {
            var candidate = targetPosition.Coordinates.GetNeighbour(direction);
            if (!map.IsOnMap(candidate)) continue;

            var occupants = GetGroundOccupants(allUnits, candidate, target);
            if (occupants.Count != 0)
            {
                var resolved = ResolveDominoForIdealHex(
                    target,
                    candidate,
                    direction,
                    idealDirection,
                    occupants,
                    map,
                    allUnits,
                    gameOriginId);
                if (resolved is not null) return resolved;
                continue;
            }

            var entry = ForcedEntryCheck.Check(
                target,
                map,
                targetPosition.Coordinates,
                targetPosition.Surface,
                candidate);
            if (!entry.IsLegal) continue;

            return new TargetDestination(candidate, entry.Surface, null);
        }

        return null;
    }

    private TargetDestination? ResolveDominoForIdealHex(
        IUnit target,
        HexCoordinates candidate,
        HexDirection direction,
        HexDirection idealDirection,
        IReadOnlyList<IUnit> occupants,
        IBattleMap map,
        IReadOnlyList<IUnit> allUnits,
        Guid gameOriginId)
    {
        // Only the ideal hex may be occupied: a fallback hex must be genuinely open.
        if (direction != idealDirection) return null;

        var resolved = dominoEffectResolver.Resolve(
            new DominoEffectRequest
            {
                EnteringUnit = target,
                Destination = candidate,
                Direction = direction,
                Reason = DisplacementReason.DominoEffect,
                GameOriginId = gameOriginId,
                Occupants = occupants,
                ApplyEntryElevationRule = true
            },
            map,
            allUnits);

        return resolved.HasDisplacements
            ? new TargetDestination(candidate, resolved.EnteringSurface, resolved)
            : null;
    }

    /// <summary>
    /// Picks the hex the target of a missed Death From Above is displaced into: the first empty,
    /// passable adjacent hex along the jump vector, measured away from the hex where the attacker
    /// started its movement. A miss therefore displaces the target along the same line as a hit.
    /// <para>
    /// The rules give this choice to the target's controller (Total Warfare, p. 150). The first
    /// version resolves it on the server so that a miss needs no interaction; restoring the
    /// controller's choice is tracked as a follow-up. Returns null when no adjacent hex is available,
    /// which destroys the attacker.
    /// </para>
    /// </summary>
    public DfaMissDisplacementCandidate? ResolveMiss(
        IUnit target,
        HexCoordinates startCoordinates,
        IBattleMap map,
        IEnumerable<IUnit> units)
    {
        if (target.Position is not { } targetPosition) return null;

        var allUnits = units.ToList();
        var maxDrop = rulesProvider.GetMaxDisplacementDropWithoutAutomaticFall();

        // Ordered by how closely each neighbour points away from the jump origin, so the ideal hex
        // of the hit push is tried first and the rest of the ring is a deterministic fallback.
        foreach (var direction in
                 targetPosition.Coordinates.GetDirectionsByAlignmentAwayFrom(startCoordinates))
        {
            var candidate = targetPosition.Coordinates.GetNeighbour(direction);
            if (!map.IsOnMap(candidate)) continue;
            if (GetGroundOccupants(allUnits, candidate, target).Count != 0) continue;

            var entry = ForcedEntryCheck.Check(
                target,
                map,
                targetPosition.Coordinates,
                targetPosition.Surface,
                candidate);
            if (!entry.IsLegal) continue;

            var levelsFallen = GetLevelsFallen(
                map, targetPosition, candidate, entry.Surface);
            return new DfaMissDisplacementCandidate(
                candidate,
                entry.Surface,
                levelsFallen,
                levelsFallen > maxDrop);
        }

        return null;
    }

    /// <summary>
    /// Resolves the hex where the attacker grounds. An empty hex is used as is. An occupied hex
    /// runs the domino effect; a DFA has no side-hex fallback (that is the charge miss rule), so a
    /// blocked chain leaves the overlap unresolved for the caller to treat as no legal landing
    /// (Total Warfare, p. 150).
    /// </summary>
    public DfaLandingResult? ResolveLanding(
        IUnit attacker,
        HexPosition desired,
        IBattleMap map,
        IEnumerable<IUnit> units,
        Guid gameOriginId = default)
    {
        if (attacker.Position is not { } attackerPosition) return null;
        if (!map.IsOnMap(desired.Coordinates) || map.GetHex(desired.Coordinates) is null)
            return null;

        var allUnits = units.ToList();
        var occupants = GetGroundOccupants(allUnits, desired.Coordinates, attacker);
        if (occupants.Count == 0)
            return new DfaLandingResult(desired with { AirborneLevel = null });

        // The domino direction is the landing line; falling back to the attacker's facing keeps a
        // deterministic order for a landing in its own approach hex.
        var direction = desired.Coordinates == attackerPosition.Coordinates
            ? attackerPosition.Facing
            : desired.Coordinates.GetDirectionAwayFrom(attackerPosition.Coordinates);

        var domino = dominoEffectResolver.Resolve(
            new DominoEffectRequest
            {
                EnteringUnit = attacker,
                Destination = desired.Coordinates,
                Direction = direction,
                Reason = DisplacementReason.DominoEffect,
                GameOriginId = gameOriginId,
                Occupants = occupants,
                ApplyEntryElevationRule = true
            },
            map,
            allUnits);

        if (domino.HasDisplacements)
            return new DfaLandingResult(
                desired with { Surface = domino.EnteringSurface, AirborneLevel = null },
                domino);

        return new DfaLandingResult(
            desired with { AirborneLevel = null },
            IsOverlapUnresolved: true);
    }

    private static int GetLevelsFallen(
        IBattleMap map,
        HexPosition from,
        HexCoordinates toCoordinates,
        HexSurface toSurface)
    {
        var fromLevel = map.GetHex(from.Coordinates)!.GetStandingLevel(from.Surface);
        var toLevel = map.GetHex(toCoordinates)!.GetStandingLevel(toSurface);
        return Math.Max(0, fromLevel - toLevel);
    }

    /// <summary>
    /// The grounded, non-destroyed occupants of a hex. An airborne pending-DFA attacker is not a
    /// ground occupant and is excluded (Total Warfare, p. 149).
    /// </summary>
    private static List<IUnit> GetGroundOccupants(
        IReadOnlyList<IUnit> units,
        HexCoordinates coordinates,
        IUnit excludedUnit) =>
        units
            .Where(unit => !ReferenceEquals(unit, excludedUnit)
                           && unit is
                           {
                               IsDeployed: true,
                               IsDestroyed: false,
                               Position: not null,
                               IsAirborneDfaAttacker: false
                           }
                           && unit.Position.Coordinates == coordinates)
            .ToList();
}

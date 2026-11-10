using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Where a charge moves the units. A null target or attacker destination means that unit stays
/// where it is; a null result means nothing moves at all.
/// </summary>
/// <param name="TargetDestination">The hex the charged target is displaced to, if any.</param>
/// <param name="AttackerDestination">The hex the attacker advances to, if any.</param>
/// <param name="LevelsFallen">How many standing levels the target drops.</param>
/// <param name="TriggersAutomaticFall">
/// Whether the target's drop exceeds the deepest displacement drop that does not force a fall.
/// </param>
/// <param name="DominoEffect">
/// The resolved domino chain when the target's primary destination was occupied. Its commands
/// must be applied before the target moves into the vacated hex.
/// </param>
public record ChargeDisplacementResult(
    HexPosition? TargetDestination,
    HexPosition? AttackerDestination,
    int LevelsFallen,
    bool TriggersAutomaticFall,
    DominoEffectResult? DominoEffect = null);

/// <summary>
/// Applies the charge displacement policy from docs/project/physical-attacks-prd.md R6.4.
/// <para>
/// On a hit the target is pushed one hex in the direction of the charge. An occupied primary hex
/// triggers the shared domino effect. If the primary hex is off the map, blocked by elevation or
/// otherwise impossible, the resolver tries the adjacent hexes in the alternating nearest-first
/// order relative to the charge direction (+1, −1, +2, −2), excluding the attacker's approach hex.
/// If no candidate is valid both units stay put; the charge still deals damage and creates its
/// +2 piloting skill rolls.
/// </para>
/// <para>
/// On a miss the attacker lands in the first valid side hex of its forward arc, using the same
/// order. A side hex is valid when it is on the map, empty, and passes <see cref="ForcedEntryCheck"/>
/// without a deep drop.
/// </para>
/// <para>
/// When the charge automatically fails because the target left the hex, the attacker completes
/// its committed movement into that hex unless another non-destroyed unit occupies it.
/// </para>
/// </summary>
public class ChargeDisplacementResolver(IRulesProvider rulesProvider, IDiceRoller diceRoller)
{
    private readonly DominoEffectResolver _dominoEffectResolver = new(diceRoller);

    /// <summary>
    /// The displacement of a successful charge against <paramref name="target"/>, or null when
    /// nothing can move.
    /// </summary>
    public ChargeDisplacementResult? ResolveHit(
        IUnit attacker,
        IUnit target,
        IBattleMap map,
        IEnumerable<IUnit> units,
        Guid gameOriginId = default)
    {
        if (attacker.MovementTaken is not { } movement) return null;

        // The direction comes from the committed approach hex to the destination, not from the
        // attacker's current position: an earlier attack of the phase can displace the attacker
        // while the charge still resolves from its phase-entry snapshot.
        var lastTranslation = movement.GetLastTranslationIndex();
        if (lastTranslation < 0) return null;
        var approachCoordinates = movement.Segments[lastTranslation].From.Coordinates;
        var destinationCoordinates = movement.Destination.Coordinates;
        if (approachCoordinates.DistanceTo(destinationCoordinates) != 1) return null;

        // A destroyed attacker does not enter the target hex. The target is still displaced by the
        // impact if it survived (Total Warfare, p. 148).
        var attackerDestination = attacker.IsDestroyed ? null : movement.Destination;

        if (target.IsDestroyed)
            return attackerDestination is null
                ? null
                : new ChargeDisplacementResult(null, attackerDestination, 0, false);

        if (target.Position is not { } targetPosition) return null;

        var allUnits = units.ToList();
        var direction = approachCoordinates.GetDirectionToNeighbour(destinationCoordinates);
        var primaryCoordinates = targetPosition.Coordinates.GetNeighbour(direction);

        var occupants = GetOccupants(allUnits, primaryCoordinates, target);
        if (occupants.Count != 0)
        {
            var domino = ResolveDomino(
                target, primaryCoordinates, direction, occupants, allUnits, map, gameOriginId);
            if (domino is { HasDisplacements: true })
                return CreateResult(
                    target, map, primaryCoordinates, domino.EnteringSurface, attackerDestination, domino);

            return ResolveFallback(target, allUnits, map, direction, approachCoordinates, targetPosition,
                attackerDestination);
        }

        var entry = ForcedEntryCheck.Check(
            target, map, targetPosition.Coordinates, targetPosition.Surface, primaryCoordinates);
        return entry.IsLegal
            ? CreateResult(target, map, primaryCoordinates, entry.Surface, attackerDestination)
            : ResolveFallback(target, allUnits, map, direction, approachCoordinates, targetPosition,
                attackerDestination);
    }

    /// <summary>
    /// The attacker's side-hex landing after a miss, or null when no valid side hex exists.
    /// </summary>
    public ChargeDisplacementResult? ResolveMiss(
        IUnit attacker,
        IBattleMap map,
        IEnumerable<IUnit> units)
    {
        if (attacker.IsDestroyed) return null;
        if (attacker.Position is not { } attackerPosition) return null;

        var allUnits = units.ToList();
        var facing = attackerPosition.Facing;

        foreach (var sideDirection in GetAlternatingDirections(facing))
        {
            var coordinates = attackerPosition.Coordinates.GetNeighbour(sideDirection);
            if (!map.IsOnMap(coordinates)) continue;
            if (HasNonDestroyedOccupant(allUnits, coordinates, attacker)) continue;

            var entry = ForcedEntryCheck.Check(
                attacker, map, attackerPosition.Coordinates, attackerPosition.Surface, coordinates);
            if (!entry.IsLegal || entry.IsDeepDrop) continue;

            return new ChargeDisplacementResult(
                null,
                new HexPosition(coordinates, facing, entry.Surface),
                -entry.LevelDifference,
                false);
        }

        return null;
    }

    /// <summary>
    /// The attacker's committed movement into the destination hex after an automatic failure.
    /// </summary>
    public ChargeDisplacementResult? ResolveAdvance(IUnit attacker)
    {
        if (attacker.IsDestroyed) return null;
        if (attacker.MovementTaken is not { } movement) return null;
        return new ChargeDisplacementResult(null, movement.Destination, 0, false);
    }

    private ChargeDisplacementResult? ResolveFallback(
        IUnit target,
        IReadOnlyList<IUnit> allUnits,
        IBattleMap map,
        HexDirection direction,
        HexCoordinates approachCoordinates,
        HexPosition targetPosition,
        HexPosition? attackerDestination)
    {
        foreach (var fallbackDirection in GetAlternatingDirections(direction))
        {
            var coordinates = targetPosition.Coordinates.GetNeighbour(fallbackDirection);
            if (coordinates == approachCoordinates) continue;
            if (!map.IsOnMap(coordinates)) continue;
            if (HasOccupant(allUnits, coordinates, target)) continue;

            var entry = ForcedEntryCheck.Check(
                target, map, targetPosition.Coordinates, targetPosition.Surface, coordinates);
            if (!entry.IsLegal) continue;

            return CreateResult(target, map, coordinates, entry.Surface, attackerDestination);
        }

        return null;
    }

    private ChargeDisplacementResult CreateResult(
        IUnit target,
        IBattleMap map,
        HexCoordinates destinationCoordinates,
        HexSurface destinationSurface,
        HexPosition? attackerDestination,
        DominoEffectResult? domino = null)
    {
        if (target.Position is not { } targetPosition) return new ChargeDisplacementResult(
            null, attackerDestination, 0, false, domino);

        var targetLevel = map.GetHex(targetPosition.Coordinates)!
            .GetStandingLevel(targetPosition.Surface);
        var destinationLevel = map.GetHex(destinationCoordinates)!
            .GetStandingLevel(destinationSurface);
        var levelsFallen = targetLevel - destinationLevel;

        return new ChargeDisplacementResult(
            new HexPosition(destinationCoordinates, targetPosition.Facing, destinationSurface),
            attackerDestination,
            levelsFallen,
            levelsFallen > rulesProvider.GetMaxDisplacementDropWithoutAutomaticFall(),
            domino);
    }

    private DominoEffectResult ResolveDomino(
        IUnit target,
        HexCoordinates destinationCoordinates,
        HexDirection direction,
        IReadOnlyList<IUnit> occupants,
        IReadOnlyList<IUnit> allUnits,
        IBattleMap map,
        Guid gameOriginId) =>
        _dominoEffectResolver.Resolve(
            new DominoEffectRequest
            {
                EnteringUnit = target,
                Destination = destinationCoordinates,
                Direction = direction,
                Reason = DisplacementReason.DominoEffect,
                GameOriginId = gameOriginId,
                Occupants = occupants,
                ApplyEntryElevationRule = true
            },
            map,
            allUnits);

    private static List<IUnit> GetOccupants(
        IReadOnlyList<IUnit> units,
        HexCoordinates coordinates,
        IUnit displacedUnit) =>
        units
            .Where(unit => !ReferenceEquals(unit, displacedUnit)
                           && unit is { IsDeployed: true, Position: not null }
                           && unit.Position.Coordinates == coordinates)
            .ToList();

    private static bool HasOccupant(
        IReadOnlyList<IUnit> units,
        HexCoordinates coordinates,
        IUnit displacedUnit) =>
        GetOccupants(units, coordinates, displacedUnit).Count != 0;

    private static bool HasNonDestroyedOccupant(
        IReadOnlyList<IUnit> units,
        HexCoordinates coordinates,
        IUnit attacker) =>
        units.Any(unit => !ReferenceEquals(unit, attacker)
                          && unit is { IsDeployed: true, IsDestroyed: false, Position: not null }
                          && unit.Position.Coordinates == coordinates);

    private static IEnumerable<HexDirection> GetAlternatingDirections(HexDirection direction)
    {
        yield return direction.Rotate(1);
        yield return direction.Rotate(-1);
        yield return direction.Rotate(2);
        yield return direction.Rotate(-2);
    }
}

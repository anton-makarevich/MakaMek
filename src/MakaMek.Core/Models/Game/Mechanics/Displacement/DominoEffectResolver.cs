using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;

/// <summary>
/// Calculates the domino effect a forced entry triggers when the destination hex exceeds the
/// stacking limit (Total Warfare, p. 151-153).
/// <para>
/// The resolver is calculation-only: it never mutates the game state and never rolls a piloting
/// skill roll. It returns the displacement commands (far-to-near) and the Mechs that owe a
/// <see cref="PilotingSkillRollType.DominoEffect"/> roll once the displacement is applied.
/// </para>
/// <para>
/// The first link decides whether anything moves. If the entering unit cannot reach the occupied
/// destination, or the occupant cannot be forced out of it, the chain is blocked atomically: no
/// displacement command is returned and the specific failure is reported, so callers keep every
/// other effect of the triggering action.
/// </para>
/// <para>
/// A later link that is impossible (off map, prohibited terrain, a climb above the unit's
/// single-hex limit, or the iteration limit) ends the chain without rewinding it: the displacements
/// already accepted stay applied, the blocked unit keeps its hex and now shares it with the unit
/// displaced into it (Total Warfare p. 151-152). The result is
/// <see cref="DominoEffectOutcome.PartiallyResolved"/> and both units are reported as PSR candidates.
/// </para>
/// <para>
/// A displaced unit that drops two or more levels into an empty hex is recorded in
/// <see cref="DominoEffectResult.Falls"/>: it falls after the displacement instead of stepping down.
/// </para>
/// </summary>
public class DominoEffectResolver(IDiceRoller diceRoller)
{
    public const int MaxDisplacementIterations = 20;

    /// <summary>
    /// Calculates the chain triggered by <paramref name="request"/>.
    /// </summary>
    /// <param name="request">The forced entry to resolve.</param>
    /// <param name="map">The battle map.</param>
    /// <param name="units">Every unit in the game, used to detect the occupants of each hex.</param>
    public DominoEffectResult Resolve(DominoEffectRequest request, IBattleMap map, IEnumerable<IUnit> units)
    {
        var enteringUnit = request.EnteringUnit;
        if (enteringUnit.Position is not { } enteringPosition)
            return Blocked(DisplacementFailureReason.OffMap, request.Destination);

        var deployedUnits = units
            .Where(unit => unit is { IsDeployed: true, Position: not null })
            .ToList();

        var occupants = (request.Occupants ?? deployedUnits)
            .Where(unit => unit.Id != enteringUnit.Id
                           && unit.IsDeployed
                           && unit.Position is not null
                           && unit.Position.Coordinates == request.Destination)
            .ToList();

        if (occupants.Count == 0)
            return DominoEffectResult.NotRequired();

        var originSurface = request.OriginSurface ?? enteringPosition.Surface;
        var entryCheck = ForcedEntryCheck.Check(
            enteringUnit, map, enteringPosition.Coordinates, originSurface, request.Destination);

        if (!entryCheck.IsLegal)
            return Blocked(entryCheck.Failure!.Value, request.Destination, entryCheck.Surface);

        if (request.ApplyEntryElevationRule && entryCheck.IsDeepDrop)
            return AccidentalFall(request.Destination, entryCheck.Surface);

        var commands = new List<DisplaceUnitCommand>();
        var psrCandidates = new List<IUnit> { enteringUnit };
        var falls = new List<DominoFall>();

        var processed = new HashSet<Guid> { enteringUnit.Id };
        var from = request.Destination;
        var currentUnit = SelectOccupant(enteringUnit, occupants);
        var iteration = 0;

        while (true)
        {
            if (++iteration > MaxDisplacementIterations)
                return StopChain(
                    DisplacementFailureReason.IterationLimit, from, currentUnit, entryCheck.Surface,
                    commands, psrCandidates, falls);

            processed.Add(currentUnit.Id);

            var fromSurface = from == request.Destination && request.OriginSurface is { } overrideSurface
                ? overrideSurface
                : currentUnit.Position!.Surface;

            var to = from.GetNeighbour(request.Direction);
            var step = ForcedEntryCheck.Check(currentUnit, map, from, fromSurface, to);
            if (!step.IsLegal)
                return StopChain(
                    step.Failure!.Value, to, currentUnit, entryCheck.Surface,
                    commands, psrCandidates, falls);

            // Only an occupied destination turns a deep drop into an accidental fall from above:
            // an empty destination simply drops the unit to the lower level.
            var nextOccupants = GetOccupants(deployedUnits, to, processed);
            if (request.ApplyEntryElevationRule && nextOccupants.Count != 0 && step.IsDeepDrop)
                return AccidentalFall(to, entryCheck.Surface);

            commands.Add(new DisplaceUnitCommand
            {
                UnitId = currentUnit.Id,
                FromCoordinates = from.ToData(),
                ToCoordinates = to.ToData(),
                NewFacing = (int)(currentUnit.Facing ?? currentUnit.Position!.Facing),
                Surface = (int)step.Surface,
                DisplacementReason = request.Reason,
                GameOriginId = request.GameOriginId,
                Timestamp = DateTime.UtcNow
            });

            if (psrCandidates.All(candidate => candidate.Id != currentUnit.Id))
                psrCandidates.Add(currentUnit);

            // A drop of two or more levels into an empty hex is not a step down: the displaced
            // unit falls and takes normal falling damage after the displacement is applied.
            if (-step.LevelDifference >= 2)
                falls.Add(new DominoFall(currentUnit, -step.LevelDifference));

            if (nextOccupants.Count == 0) break;

            currentUnit = SelectOccupant(currentUnit, nextOccupants);
            from = to;
        }

        // Far-to-near: each command moves a unit into a hex vacated by an already-applied command.
        commands.Reverse();

        return new DominoEffectResult(
            DominoEffectOutcome.Resolved,
            commands,
            psrCandidates,
            entryCheck.Surface)
        {
            Falls = falls
        };
    }

    private static DominoEffectResult Blocked(
        DisplacementFailureReason reason,
        HexCoordinates? at,
        HexSurface enteringSurface = HexSurface.Ground) =>
        new(DominoEffectOutcome.Blocked, [], [], enteringSurface, reason, at);

    /// <summary>
    /// Ends the chain at the link <paramref name="blockedUnit"/> cannot take. When nothing has moved
    /// yet the entry itself is blocked and the chain stays atomic; otherwise the displacements
    /// already accepted are kept (no rewind) and the blocked unit joins the units that owe a
    /// domino PSR, because the unit displaced into its hex now shares it.
    /// </summary>
    private static DominoEffectResult StopChain(
        DisplacementFailureReason reason,
        HexCoordinates at,
        IUnit blockedUnit,
        HexSurface enteringSurface,
        List<DisplaceUnitCommand> commands,
        List<IUnit> psrCandidates,
        List<DominoFall> falls)
    {
        if (commands.Count == 0)
            return Blocked(reason, at, enteringSurface);

        if (psrCandidates.All(candidate => candidate.Id != blockedUnit.Id))
            psrCandidates.Add(blockedUnit);

        commands.Reverse();

        return new DominoEffectResult(
            DominoEffectOutcome.PartiallyResolved,
            commands,
            psrCandidates,
            enteringSurface,
            reason,
            at)
        {
            Falls = falls
        };
    }

    private static DominoEffectResult AccidentalFall(HexCoordinates? at, HexSurface enteringSurface) =>
        new(DominoEffectOutcome.AccidentalFallFromAbove, [], [], enteringSurface, null, at);

    /// <summary>
    /// Picks the unit dislodged from an occupied hex: an occupant of the entering unit's concrete
    /// type comes first, otherwise the choice is random (one roll of a D6). The selection is a
    /// single point so the deferred side-hex dodge can be evaluated here later.
    /// </summary>
    private IUnit SelectOccupant(IUnit enteringUnit, IReadOnlyList<IUnit> occupants)
    {
        if (occupants.Count == 1) return occupants[0];

        var sameType = occupants.Where(occupant => occupant.GetType() == enteringUnit.GetType()).ToList();
        var pool = sameType.Count > 0 ? sameType : occupants;
        if (pool.Count == 1) return pool[0];

        var roll = diceRoller.RollD6();
        // A substituted dice roller may return null in tests; fall back to the first occupant.
        var index = roll is null 
            ? 0 
            : (roll.Result - 1) % pool.Count;
        return pool[index];
    }

    private static List<IUnit> GetOccupants(
        IEnumerable<IUnit> deployedUnits,
        HexCoordinates coordinates,
        HashSet<Guid> excluded) =>
        deployedUnits
            .Where(unit => unit.Position!.Coordinates == coordinates && !excluded.Contains(unit.Id))
            .ToList();
}

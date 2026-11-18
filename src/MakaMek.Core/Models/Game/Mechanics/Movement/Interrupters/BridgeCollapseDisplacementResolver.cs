using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

/// <summary>
/// Pushes the units off a collapsing bridge deck in the direction opposite to the mover's entry.
/// </summary>
internal sealed class BridgeCollapseDisplacementResolver(DominoEffectResolver dominoEffectResolver)
{
    public void AppendDisplacementActions(
        MovementInterruptContext context,
        List<IGameAction> actions,
        PathSegmentData segment,
        HexCoordinates bridgeCoords,
        List<IUnit> unitsOnHex)
    {
        var displacementDirection = GetEntryDirection(segment).GetOppositeDirection();
        var candidates = unitsOnHex.Where(u => u.Id != context.Unit.Id).ToList();

        actions.AddRange(ResolveDisplacementChain(
            context.Unit, bridgeCoords, displacementDirection, candidates, context.Game));
    }

    private static HexDirection GetEntryDirection(PathSegmentData segment)
    {
        var fromCoords = new HexCoordinates(segment.From.Coordinates);
        var toCoords = new HexCoordinates(segment.To.Coordinates);
        return toCoords.GetDirectionToNeighbour(fromCoords);
    }

    private List<IGameAction> ResolveDisplacementChain(
        IUnit enteringUnit,
        HexCoordinates bridgeCoords,
        HexDirection displacementDirection,
        List<IUnit> candidates,
        ServerGame game)
    {
        var actions = new List<IGameAction>();
        if (candidates.Count == 0) return actions;

        var result = dominoEffectResolver.Resolve(
            new DominoEffectRequest
            {
                EnteringUnit = enteringUnit,
                Destination = bridgeCoords,
                Direction = displacementDirection,
                Reason = DisplacementReason.DominoEffect,
                GameOriginId = game.Id,
                Occupants = candidates,
                // The deck is gone: every unit drops into the same ground hex before the chain runs.
                OriginSurface = HexSurface.Ground,
                ApplyEntryElevationRule = false
            },
            game.BattleMap!,
            game.Players.SelectMany(player => player.Units));

        if (!result.HasDisplacements) return actions;

        actions.AddRange(result.Commands.Select(command => new DisplaceUnitAction(command, publish: true)));
        actions.Add(new DominoEffectRollAction(result.PsrCandidates, game.FallProcessor, result.Falls));
        return actions;
    }
}

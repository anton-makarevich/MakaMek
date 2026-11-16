using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

/// <summary>
/// Resolves a bridge collapse: the tonnage check, the collapse command, the fall of every 'Mech on
/// the deck and the displacement chain off it. Extracted from
/// <see cref="BridgeCollapseInterruptHandler"/> to keep the handler a thin dispatcher.
/// </summary>
internal sealed class BridgeCollapseResolver
{
    private readonly BridgeCollapseFallResolver _fallResolver = new();
    private readonly BridgeCollapseDisplacementResolver _displacementResolver;

    public BridgeCollapseResolver(DominoEffectResolver dominoEffectResolver)
    {
        _displacementResolver = new BridgeCollapseDisplacementResolver(dominoEffectResolver);
    }

    public MovementInterruptResult? Check(MovementInterruptContext context)
    {
        var segment = context.MoveCommand.MovementPath[context.SegmentIndex];
        if (segment.From.Coordinates == segment.To.Coordinates) return null;

        var bridgeCoords = new HexCoordinates(segment.To.Coordinates);
        var hex = context.Game.BattleMap?.GetHex(bridgeCoords);

        if (hex?.GetTerrain(MakaMekTerrains.Bridge) is not BridgeTerrain bridgeTerrain) return null;

        var unitsOnHex = GetUnitsOnHex(context, bridgeCoords);

        return context.IsLandingCheck
            ? ResolveLanding(context, segment, bridgeTerrain, bridgeCoords, unitsOnHex)
            : ResolveWalk(context, segment, bridgeTerrain, bridgeCoords, unitsOnHex);
    }

    /// <summary>
    /// Landing (jump): the unit is already on the hex after OnMoveUnit.
    /// </summary>
    private MovementInterruptResult? ResolveLanding(
        MovementInterruptContext context,
        PathSegmentData segment,
        BridgeTerrain bridgeTerrain,
        HexCoordinates bridgeCoords,
        List<IUnit> unitsOnHex)
    {
        var totalTonnage = unitsOnHex.Sum(u => u.Tonnage);
        if (totalTonnage <= bridgeTerrain.ConstructionFactor) return null;

        var actions = new List<IGameAction>
        {
            new BridgeCollapsedAction(
                CreateBridgeCommand(context, bridgeCoords, bridgeTerrain, totalTonnage), publish: true)
        };

        _fallResolver.AppendFallActions(context, actions, bridgeCoords, bridgeTerrain.Height, unitsOnHex);
        _displacementResolver.AppendDisplacementActions(context, actions, segment, bridgeCoords, unitsOnHex);

        return Stopped(actions);
    }

    /// <summary>
    /// Walk/run: the entering unit is not yet on the hex, so its tonnage is added on top of the
    /// occupants and the path is truncated at the collapsing segment.
    /// </summary>
    private MovementInterruptResult? ResolveWalk(
        MovementInterruptContext context,
        PathSegmentData segment,
        BridgeTerrain bridgeTerrain,
        HexCoordinates bridgeCoords,
        List<IUnit> unitsOnHex)
    {
        var totalTonnageWithMover = unitsOnHex.Sum(u => u.Tonnage) + context.Unit.Tonnage;
        if (totalTonnageWithMover <= bridgeTerrain.ConstructionFactor) return null;

        // Include the entering unit in the fall processing list
        unitsOnHex.Add(context.Unit);

        var truncatedCommand = CreateTruncatedCommand(context);
        var actions = new List<IGameAction>
        {
            new MoveUnitAction(truncatedCommand, publish: false),
            new BridgeCollapsedAction(
                CreateBridgeCommand(context, bridgeCoords, bridgeTerrain, totalTonnageWithMover), publish: true),
            new MoveUnitAction(
                truncatedCommand with { IsCompleted = true, GameOriginId = context.Game.Id }, publish: true)
        };

        _fallResolver.AppendFallActions(context, actions, bridgeCoords, bridgeTerrain.Height, unitsOnHex);
        _displacementResolver.AppendDisplacementActions(context, actions, segment, bridgeCoords, unitsOnHex);

        return Stopped(actions);
    }

    private static List<IUnit> GetUnitsOnHex(MovementInterruptContext context, HexCoordinates bridgeCoords) =>
        context.Game.Players
            .SelectMany(p => p.Units)
            .Where(u => u.IsDeployed && u.Position!.Coordinates == bridgeCoords)
            .ToList();

    private static BridgeCollapsedCommand CreateBridgeCommand(
        MovementInterruptContext context,
        HexCoordinates bridgeCoords,
        BridgeTerrain bridgeTerrain,
        int totalTonnage) =>
        new()
        {
            GameOriginId = context.Game.Id,
            Coordinates = bridgeCoords.ToData(),
            ConstructionFactor = bridgeTerrain.ConstructionFactor,
            TotalTonnage = totalTonnage,
            TriggeringUnitId = context.Unit.Id,
            Timestamp = DateTime.UtcNow
        };

    private static MoveUnitCommand CreateTruncatedCommand(MovementInterruptContext context)
    {
        var truncatedSegments = context.MoveCommand.MovementPath.Take(context.SegmentIndex + 1).ToList();
        var truncatedPath = new MovementPath(truncatedSegments, context.MoveCommand.MovementType)
            .WithLastSegmentEvent(new SegmentEvent(SegmentEventType.BridgeCollapse))
            .WithLastSegmentEvent(new SegmentEvent(SegmentEventType.Fall));

        return context.MoveCommand with
        {
            MovementPath = truncatedPath.ToData(),
            IsCompleted = false
        };
    }

    private static MovementInterruptResult Stopped(List<IGameAction> actions) =>
        new()
        {
            ShouldStop = true,
            DeferStepConsumption = false,
            GameActions = actions
        };
}

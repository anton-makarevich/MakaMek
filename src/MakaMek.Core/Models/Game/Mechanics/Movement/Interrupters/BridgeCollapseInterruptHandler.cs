using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

public class BridgeCollapseInterruptHandler : IMovementInterruptHandler
{
    public MovementInterruptResult? Check(MovementInterruptContext context)
    {
        var segment = context.MoveCommand.MovementPath[context.SegmentIndex];
        if (segment.From.Coordinates == segment.To.Coordinates) return null;

        var bridgeCoords = new HexCoordinates(segment.To.Coordinates);
        var hex = context.Game.BattleMap?.GetHex(bridgeCoords);

        if (hex?.GetTerrain(MakaMekTerrains.Bridge) is not BridgeTerrain bridgeTerrain) return null;

        var bridgeHeight = bridgeTerrain.Height;
        var constructionFactor = bridgeTerrain.ConstructionFactor;

        // Cache units currently on the bridge hex
        var unitsOnHex = context.Game.Players
            .SelectMany(p => p.Units)
            .Where(u => u.IsDeployed && u.Position!.Coordinates == bridgeCoords)
            .ToList();

        if (context.IsLandingCheck)
        {
            // Landing (jump): unit is already on the hex after OnMoveUnit
            var totalTonnage = unitsOnHex.Sum(u => u.Tonnage);
            if (totalTonnage <= constructionFactor) return null;

            var bridgeCommand = new BridgeCollapsedCommand
            {
                GameOriginId = context.Game.Id,
                Coordinates = bridgeCoords.ToData(),
                ConstructionFactor = constructionFactor,
                TotalTonnage = totalTonnage,
                TriggeringUnitId = context.Unit.Id,
                Timestamp = DateTime.UtcNow
            };

            var actions = new List<IGameAction>
            {
                new BridgeCollapsedAction(bridgeCommand, publish: true)
            };

            foreach (var hexUnit in unitsOnHex)
            {
                if (hexUnit is not Mech hexMech) continue;

                var fallContextData = context.Game.FallProcessor.ProcessMovementAttempt(
                    hexMech, new BridgeCollapseRollContext(bridgeHeight), context.Game,
                    hexUnit.Id == context.Unit.Id
                        ? context.MoveCommand.MovementType
                        : hexMech.MovementTaken?.MovementType ?? MovementType.StandingStill,
                    new HexPosition(bridgeCoords, hexMech.Position!.Facing));

                if (fallContextData.IsFalling)
                {
                    var fallCommand = fallContextData.ToMechFallCommand();
                    actions.Add(new ApplyFallAction(hexMech, fallCommand));
                }
                else
                {
                    context.Game.Logger.LogError(
                        "Bridge collapse should always result in a fall for unit {UnitId}", hexMech.Id);
                    throw new InvalidOperationException(
                        $"Bridge collapse should always result in a fall for unit {hexMech.Id}");
                }
            }

            // Add displacement actions after all fall actions
            var entryDirection = GetEntryDirection(segment);
            var oppositeDirection = entryDirection.GetOppositeDirection();
            var candidates = unitsOnHex.Where(u => u.Id != context.Unit.Id).ToList();
            actions.AddRange(ResolveDisplacementChain(
                context.Unit, bridgeCoords, oppositeDirection, candidates, context.Game));

            return new MovementInterruptResult
            {
                ShouldStop = true,
                DeferStepConsumption = false,
                GameActions = actions
            };
        }

        // Walk/run: entering unit is not yet on the hex
        var existingTonnage = unitsOnHex.Sum(u => u.Tonnage);
        var totalTonnageWithMover = existingTonnage + context.Unit.Tonnage;
        if (totalTonnageWithMover <= constructionFactor) return null;

        // Include the entering unit in the fall processing list
        unitsOnHex.Add(context.Unit);

        // Truncate path at bridge segment
        var truncatedSegments = context.MoveCommand.MovementPath.Take(context.SegmentIndex + 1).ToList();
        var truncatedPath = new MovementPath(truncatedSegments, context.MoveCommand.MovementType)
            .WithLastSegmentEvent(new SegmentEvent(SegmentEventType.BridgeCollapse))
            .WithLastSegmentEvent(new SegmentEvent(SegmentEventType.Fall));
        var truncatedCommand = context.MoveCommand with
        {
            MovementPath = truncatedPath.ToData(),
            IsCompleted = false
        };

        var bridgeCmd = new BridgeCollapsedCommand
        {
            GameOriginId = context.Game.Id,
            Coordinates = bridgeCoords.ToData(),
            ConstructionFactor = constructionFactor,
            TotalTonnage = totalTonnageWithMover,
            TriggeringUnitId = context.Unit.Id,
            Timestamp = DateTime.UtcNow
        };

        var walkActions = new List<IGameAction>
        {
            new MoveUnitAction(truncatedCommand, publish: false),
            new BridgeCollapsedAction(bridgeCmd, publish: true),
            new MoveUnitAction(truncatedCommand with { IsCompleted = true, GameOriginId = context.Game.Id }, publish: true)
        };

        foreach (var hexUnit in unitsOnHex)
        {
            if (hexUnit is not Mech hexMech) continue;
            var fallContextData = context.Game.FallProcessor.ProcessMovementAttempt(
                hexMech, new BridgeCollapseRollContext(bridgeHeight), context.Game,
                hexUnit.Id == context.Unit.Id
                    ? context.MoveCommand.MovementType
                    : hexMech.MovementTaken?.MovementType ?? MovementType.StandingStill,
                new HexPosition(bridgeCoords, hexMech.Position!.Facing));

            if (fallContextData.IsFalling)
            {
                var fallCommand = fallContextData.ToMechFallCommand();
                walkActions.Add(new ApplyFallAction(hexMech, fallCommand));
            }
            else
            {
                context.Game.Logger.LogError(
                    "Bridge collapse should always result in a fall for unit {UnitId}", hexMech.Id);
                throw new InvalidOperationException(
                    $"Bridge collapse should always result in a fall for unit {hexMech.Id}");
            }
        }

        // Add displacement actions after all fall actions
        var entryDirectionWalk = GetEntryDirection(segment);
        var displacementDirWalk = entryDirectionWalk.GetOppositeDirection();
        var candidatesWalk = unitsOnHex.Where(u => u.Id != context.Unit.Id).ToList();
        walkActions.AddRange(ResolveDisplacementChain(
            context.Unit, bridgeCoords, displacementDirWalk, candidatesWalk, context.Game));

        return new MovementInterruptResult
        {
            ShouldStop = true,
            DeferStepConsumption = false,
            GameActions = walkActions
        };
    }

    private static HexDirection GetEntryDirection(PathSegmentData segment)
    {
        var fromCoords = new HexCoordinates(segment.From.Coordinates);
        var toCoords = new HexCoordinates(segment.To.Coordinates);
        return toCoords.GetDirectionToNeighbour(fromCoords);
    }

    private static List<IGameAction> ResolveDisplacementChain(
        IUnit enteringUnit,
        HexCoordinates bridgeCoords,
        HexDirection displacementDirection,
        List<IUnit> candidates,
        ServerGame game)
    {
        var actions = new List<IGameAction>();
        if (candidates.Count == 0) return actions;

        var resolver = new DominoEffectResolver(game.DiceRoller);
        var result = resolver.Resolve(
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

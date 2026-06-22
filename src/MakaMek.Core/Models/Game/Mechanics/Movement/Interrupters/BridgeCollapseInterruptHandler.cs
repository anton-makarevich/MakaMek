using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

public class BridgeCollapseInterruptHandler : IMovementInterruptHandler
{
    private const int MaxDisplacementIterations = 20;

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
                        : hexMech.MovementTaken?.MovementType ?? MovementType.StandingStill);

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
            actions.AddRange(ResolveDisplacementChain(bridgeCoords, oppositeDirection, candidates, context.Game));

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
                    : hexMech.MovementTaken?.MovementType ?? MovementType.StandingStill);

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
        walkActions.AddRange(ResolveDisplacementChain(bridgeCoords, displacementDirWalk, candidatesWalk, context.Game));

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

    private static List<DisplaceUnitAction> ResolveDisplacementChain(
        HexCoordinates bridgeCoords,
        HexDirection displacementDirection,
        List<IUnit> candidates,
        ServerGame game)
    {
        var actions = new List<DisplaceUnitAction>();
        var queue = new Queue<(IUnit Unit, HexCoordinates SourceCoords)>();
        var processed = new HashSet<Guid>();
        var reservedTargets = new HashSet<HexCoordinates>();
        var iterations = 0;

        foreach (var candidate in candidates)
        {
            queue.Enqueue((candidate, bridgeCoords));
        }

        while (queue.Count > 0 && iterations < MaxDisplacementIterations)
        {
            iterations++;
            var (unit, sourceCoords) = queue.Dequeue();

            if (!processed.Add(unit.Id)) continue;

            var targetCoords = sourceCoords.GetNeighbour(displacementDirection);

            if (!game.BattleMap!.IsOnMap(targetCoords))
            {
                // Off-map: unit remains in place per rules
                continue;
            }

            // Reserve target hex - skip if another unit is already being displaced here
            if (!reservedTargets.Add(targetCoords)) continue;

            // Check if target hex is occupied by another deployed unit
            var occupant = game.Players
                .SelectMany(p => p.Units)
                .FirstOrDefault(u => u.IsDeployed && u.Id != unit.Id && u.Position!.Coordinates == targetCoords);

            if (occupant != null)
            {
                queue.Enqueue((occupant, targetCoords));
            }

            var command = new DisplaceUnitCommand
            {
                UnitId = unit.Id,
                FromCoordinates = sourceCoords.ToData(),
                ToCoordinates = targetCoords.ToData(),
                NewFacing = (int)(unit.Facing ?? HexDirection.Top),
                DisplacementReason = DisplacementReason.DominoEffect,
                GameOriginId = game.Id,
                Timestamp = DateTime.UtcNow
            };

            actions.Add(new DisplaceUnitAction(command, publish: true));
        }

        return actions;
    }
}

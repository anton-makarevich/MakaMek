using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

public class SkidInterruptHandler : IMovementInterruptHandler
{
    public MovementInterruptResult? Check(MovementInterruptContext context)
    {
        if (context.MoveCommand.MovementType != MovementType.Run) return null;

        var segment = context.MoveCommand.MovementPath[context.SegmentIndex];
        if (segment.From.Coordinates != segment.To.Coordinates) return null;

        var turnHex = context.Game.BattleMap?.GetHex(new HexCoordinates(segment.From.Coordinates));
        if (turnHex == null || !turnHex.HasHardPavement()) return null;

        if (segment.To.Coordinates == context.MoveCommand.MovementPath.Last().To.Coordinates) return null;

        if (context.Unit is not Mech mech) return null;

        var hexesMoved = CountHexesMoved(context);
        var maxSkidDistance = (int)Math.Ceiling(hexesMoved / 2.0);
        var skidResult = GenerateSkidPathSegments(context.Game, segment.From, maxSkidDistance, mech);

        var skidContext = new SkidCheckRollContext(skidResult.Segments.Count, hexesMoved);
        var skidFallContext = context.Game.FallProcessor.ProcessMovementAttempt(
            mech, skidContext, context.Game, context.MoveCommand.MovementType);

        if (!skidFallContext.IsFalling)
        {
            return new MovementInterruptResult
            {
                ShouldStop = false,
                GameActions = new List<IGameAction>
                {
                    new PublishCommandAction(skidFallContext.ToMechFallCommand())
                }
            };
        }

        return skidResult.HasCliffFall
            ? HandleSkidFailureWithCliffFall(context, mech, skidResult, skidFallContext)
            : HandleSkidFailure(context, mech, skidResult, skidFallContext);
    }

    private static int CountHexesMoved(MovementInterruptContext context)
    {
        var hexesMoved = 0;
        for (var j = 0; j < context.SegmentIndex; j++)
        {
            var prevSegment = context.MoveCommand.MovementPath[j];
            if (prevSegment.From.Coordinates != prevSegment.To.Coordinates)
                hexesMoved++;
        }
        return hexesMoved;
    }

    private static MovementInterruptResult HandleSkidFailure(
        MovementInterruptContext context,
        Mech mech,
        SkidPathResult skidResult,
        FallContextData skidFallContext)
    {
        var modifiedCommand = BuildModifiedMoveCommand(
            context, skidResult.Segments, SegmentEventType.Skid, SegmentEventType.Fall);

        var actions = BuildFallActions(
            new MoveUnitAction(modifiedCommand, publish: true),
            mech,
            skidFallContext.ToMechFallCommand(),
            skidFallContext.ToMechSkidCommand());

        return new MovementInterruptResult
        {
            ShouldStop = true,
            DeferStepConsumption = false,
            GameActions = actions
        };
    }

    private static MovementInterruptResult HandleSkidFailureWithCliffFall(
        MovementInterruptContext context,
        Mech mech,
        SkidPathResult skidResult,
        FallContextData skidFallContext)
    {
        var facingDiceRoll = skidFallContext.FallingDamageData!.FacingDiceRoll;
        var facingAfterFall = skidFallContext.FallingDamageData.FacingAfterFall;

        var cliffFallContext = context.Game.FallProcessor.ProcessMovementAttempt(
            mech,
            new CliffFallRollContext(skidResult.LevelsFallen, facingDiceRoll, facingAfterFall),
            context.Game,
            context.MoveCommand.MovementType);

        var cliffSegments = skidResult.Segments.ToList();
        cliffSegments[^1] = cliffSegments[^1] with
        {
            Events = cliffSegments[^1].Events.Append(
                new SegmentEvent(SegmentEventType.Fall)).ToArray()
        };

        var modifiedCommand = BuildModifiedMoveCommand(
            context, cliffSegments, SegmentEventType.Skid, SegmentEventType.Fall);

        var actions = BuildFallActions(
            new MoveUnitAction(modifiedCommand, publish: true),
            mech,
            skidFallContext.ToMechFallCommand(),
            skidFallContext.ToMechSkidCommand(),
            cliffFallContext.ToMechFallCommand());

        return new MovementInterruptResult
        {
            ShouldStop = true,
            DeferStepConsumption = false,
            GameActions = actions
        };
    }

    private static MoveUnitCommand BuildModifiedMoveCommand(
        MovementInterruptContext context,
        IEnumerable<PathSegment> additionalSegments,
        params SegmentEventType[] lastSegmentEvents)
    {
        var truncatedPath = new MovementPath(
            context.MoveCommand.MovementPath.Take(context.SegmentIndex + 1).ToList(),
            context.MoveCommand.MovementType);

        foreach (var eventType in lastSegmentEvents)
            truncatedPath = truncatedPath.WithLastSegmentEvent(new SegmentEvent(eventType));

        var allSegments = truncatedPath.Segments
            .Select(s => s.ToData())
            .Concat(additionalSegments.Select(s => s.ToData()))
            .ToList();

        var modifiedPath = new MovementPath(allSegments, context.MoveCommand.MovementType);

        return context.MoveCommand with
        {
            MovementPath = modifiedPath.ToData(),
            IsCompleted = true,
            GameOriginId = context.Game.Id
        };
    }

    private static List<IGameAction> BuildFallActions(
        MoveUnitAction moveAction,
        Mech mech,
        MechFallCommand firstFallCommand,
        MechSkidCommand? skidCommand,
        MechFallCommand? secondFallCommand = null)
    {
        var actions = new List<IGameAction>
        {
            moveAction,
            new ApplyFallAction(mech, firstFallCommand)
        };

        if (skidCommand != null)
            actions.Add(new ApplySkidAction(mech, skidCommand.Value));

        if (secondFallCommand != null)
            actions.Add(new ApplyFallAction(mech, secondFallCommand.Value));

        return actions;
    }

    private static SkidPathResult GenerateSkidPathSegments(ServerGame game, HexPositionData startPosition, int maxDistance, IUnit unit)
    {
        var skidPathSegments = new List<PathSegment>();
        var currentCoords = new HexCoordinates(startPosition.Coordinates);
        var skidFacing = (HexDirection)startPosition.Facing;
        var currentHex = game.BattleMap!.GetHex(currentCoords)!;
        var currentSurface = (HexSurface)startPosition.Surface;
        var remainingSkidDistance = maxDistance;

        while (remainingSkidDistance > 0)
        {
            var nextCoords = currentCoords.GetNeighbour(skidFacing);
            var nextHex = game.BattleMap?.GetHex(nextCoords);
            if (nextHex == null)
                break;

            var nextSurface = currentSurface switch
            {
                HexSurface.Bridge when nextHex.GetBridgeHeight() != null => HexSurface.Bridge,
                _ => HexSurface.Ground
            };

            if (nextSurface == HexSurface.Ground && nextHex.GetWaterDepth() >= 1)
            {
                skidPathSegments.Add(new PathSegment(
                    new HexPosition(currentCoords, skidFacing, currentSurface),
                    new HexPosition(nextCoords, skidFacing, nextSurface), [])
                {
                    Events = [new SegmentEvent(SegmentEventType.Skid)]
                });
                break;
            }

            var elevationChange = nextHex.GetElevationChange(currentHex, currentSurface, nextSurface);
            if (elevationChange < -unit.MaxLevelChangeForward)
            {
                var fromPos = new HexPosition(currentCoords, skidFacing, currentSurface);
                var toPos = new HexPosition(nextCoords, skidFacing, nextSurface);
                var skidSegment = new PathSegment(fromPos, toPos, [])
                {
                    Events = [new SegmentEvent(SegmentEventType.Skid)]
                };
                skidPathSegments.Add(skidSegment);

                return new SkidPathResult(
                    skidPathSegments,
                    true,
                    Math.Abs(elevationChange));
            }

            var movementCost = nextHex.GetEnterMovementCost(currentHex, currentSurface, nextSurface);
            var fromPos2 = new HexPosition(currentCoords, skidFacing, currentSurface);
            var toPos2 = new HexPosition(nextCoords, skidFacing, nextSurface);
            var normalSegment = new PathSegment(fromPos2, toPos2, [])
            {
                Events = [new SegmentEvent(SegmentEventType.Skid)]
            };
            skidPathSegments.Add(normalSegment);

            remainingSkidDistance -= movementCost.Sum(c => c.Value);
            currentCoords = nextCoords;
            currentHex = nextHex;
            currentSurface = nextSurface;
        }

        return new SkidPathResult(skidPathSegments, false, 0);
    }
}

using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;

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

        // Skip if this is the last hex of the path
        if (segment.To.Coordinates == context.MoveCommand.MovementPath.Last().To.Coordinates) return null;

        if (context.Unit is not Mech mech) return null;

        var hexesMoved = 0;
        for (var j = 0; j < context.SegmentIndex; j++)
        {
            var prevSegment = context.MoveCommand.MovementPath[j];
            if (prevSegment.From.Coordinates != prevSegment.To.Coordinates)
                hexesMoved++;
        }

        var maxSkidDistance = (int)Math.Ceiling(hexesMoved / 2.0);

        var skidResult = GenerateSkidPathSegments(context.Game, segment.From, maxSkidDistance, mech);

        if (skidResult.HasCliffFall)
        {
            var preCliffSkidDistance = skidResult.Segments.Count - 1;
            var skidContext = new SkidCheckRollContext(
                preCliffSkidDistance, hexesMoved, skidResult.LevelsFallen);
            var initialFallContext = context.Game.FallProcessor.ProcessMovementAttempt(
                mech, skidContext, context.Game, context.MoveCommand.MovementType);

            var facingDiceRoll = initialFallContext.FallingDamageData!.FacingDiceRoll;
            var facingAfterFall = initialFallContext.FallingDamageData.FacingAfterFall;

            var cliffFallContext = context.Game.FallProcessor.ProcessMovementAttempt(
                mech,
                new CliffFallRollContext(skidResult.LevelsFallen, facingDiceRoll, facingAfterFall),
                context.Game,
                context.MoveCommand.MovementType);

            var truncatedSegments = context.MoveCommand.MovementPath.Take(context.SegmentIndex + 1).ToList();
            var truncatedPath = new MovementPath(truncatedSegments, context.MoveCommand.MovementType);
            truncatedPath = truncatedPath
                .WithLastSegmentEvent(new SegmentEvent(SegmentEventType.Skid))
                .WithLastSegmentEvent(new SegmentEvent(SegmentEventType.Fall));

            var cliffSegments = skidResult.Segments.ToList();
            var lastCliffSegment = cliffSegments[^1];
            cliffSegments[^1] = lastCliffSegment with
            {
                Events = lastCliffSegment.Events.Append(
                    new SegmentEvent(SegmentEventType.Fall)).ToArray()
            };

            var allSegments = truncatedPath.Segments
                .Select(s => s.ToData())
                .Concat(cliffSegments.Select(s => s.ToData()))
                .ToList();

            var modifiedPath = new MovementPath(allSegments, context.MoveCommand.MovementType);
            var modifiedCommand = context.MoveCommand with
            {
                MovementPath = modifiedPath.ToData(),
                IsCompleted = true,
                GameOriginId = context.Game.Id
            };

            var initialFallCommand = initialFallContext.ToMechFallCommand();
            var skidCommand = initialFallContext.ToMechSkidCommand();
            var cliffFallCommand = cliffFallContext.ToMechFallCommand();

            var actions = new List<IGameAction>
            {
                new MoveUnitAction(modifiedCommand, publish: true),
                new ApplyFallAction(mech, initialFallCommand)
            };
            if (skidCommand != null)
                actions.Add(new ApplySkidAction(mech, skidCommand.Value));
            actions.Add(new ApplyFallAction(mech, cliffFallCommand));

            return new MovementInterruptResult
            {
                ShouldStop = true,
                DeferStepConsumption = false,
                GameActions = actions
            };
        }

        var skidContext2 = new SkidCheckRollContext(skidResult.Segments.Count, hexesMoved);
        var skidFallContext = context.Game.FallProcessor.ProcessMovementAttempt(
            mech, skidContext2, context.Game, context.MoveCommand.MovementType);

        if (skidFallContext.IsFalling)
        {
            var truncatedSegments = context.MoveCommand.MovementPath.Take(context.SegmentIndex + 1).ToList();
            var truncatedPath = new MovementPath(truncatedSegments, context.MoveCommand.MovementType);
            truncatedPath = truncatedPath
                .WithLastSegmentEvent(new SegmentEvent(SegmentEventType.Skid))
                .WithLastSegmentEvent(new SegmentEvent(SegmentEventType.Fall));

            var allSegments = truncatedPath.Segments
                .Select(s => s.ToData())
                .Concat(skidResult.Segments.Select(s => s.ToData()))
                .ToList();

            var modifiedPath = new MovementPath(allSegments, context.MoveCommand.MovementType);
            var modifiedCommand = context.MoveCommand with
            {
                MovementPath = modifiedPath.ToData(),
                IsCompleted = true,
                GameOriginId = context.Game.Id
            };

            var fallCommand = skidFallContext.ToMechFallCommand();
            var skidCommand = skidFallContext.ToMechSkidCommand();

            var actions = new List<IGameAction>
            {
                new MoveUnitAction(modifiedCommand, publish: true),
                new ApplyFallAction(mech, fallCommand)
            };
            if (skidCommand != null)
                actions.Add(new ApplySkidAction(mech, skidCommand.Value));

            return new MovementInterruptResult
            {
                ShouldStop = true,
                DeferStepConsumption = false,
                GameActions = actions
            };
        }

        var skidPsrCommand = skidFallContext.ToMechFallCommand();
        return new MovementInterruptResult
        {
            ShouldStop = false,
            GameActions = new List<IGameAction>
            {
                new PublishCommandAction(skidPsrCommand)
            }
        };
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
            if (Math.Abs(elevationChange) > unit.MaxLevelChangeForward)
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
                    Math.Abs(elevationChange),
                    nextCoords);
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

        return new SkidPathResult(skidPathSegments, false, 0, null);
    }
}

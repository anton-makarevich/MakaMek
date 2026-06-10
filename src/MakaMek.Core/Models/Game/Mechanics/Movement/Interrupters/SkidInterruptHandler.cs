using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Units.Mechs;
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
        var turnHexCoords = new HexCoordinates(segment.From.Coordinates);
        var skidFacing = (HexDirection)segment.From.Facing;

        var skidPathSegments = GenerateSkidPathSegments(context.Game, turnHexCoords, skidFacing, maxSkidDistance);
        var skidContext = new SkidCheckRollContext(skidPathSegments.Count, hexesMoved);
        var skidFallContext = context.Game.FallProcessor.ProcessMovementAttempt(
            mech, skidContext, context.Game, context.MoveCommand.MovementType);

        if (skidFallContext.IsFalling)
        {
            var truncatedSegments = context.MoveCommand.MovementPath.Take(context.SegmentIndex + 1).ToList();
            var truncatedPath = new MovementPath(truncatedSegments, context.MoveCommand.MovementType);
            truncatedPath = truncatedPath
                .WithLastSegmentEvent(new SegmentEvent(SegmentEventType.Skid))
                .WithLastSegmentEvent(new SegmentEvent(SegmentEventType.Fall));

            var allSegments = truncatedPath.Segments
                .Select(s => s.ToData())
                .Concat(skidPathSegments.Select(s => s.ToData()))
                .ToList();

            var modifiedPath = new MovementPath(allSegments, context.MoveCommand.MovementType);
            var modifiedCommand = context.MoveCommand with
            {
                MovementPath = modifiedPath.ToData(),
                IsCompleted = true
            };

            var fallCommand = skidFallContext.ToMechFallCommand();

            return new MovementInterruptResult
            {
                ShouldStop = true,
                GameActions = new List<IGameAction>
                {
                    new MoveUnitAction(modifiedCommand, publish: true),
                    new ApplyFallAction(mech, fallCommand)
                }
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

    private List<PathSegment> GenerateSkidPathSegments(ServerGame game, HexCoordinates startCoords, HexDirection skidFacing, int maxDistance)
    {
        var skidPathSegments = new List<PathSegment>();
        var currentCoords = startCoords;
        var currentHex = game.BattleMap!.GetHex(currentCoords)!;
        var remainingSkidDistance = maxDistance;

        while (remainingSkidDistance > 0)
        {
            var nextCoords = currentCoords.GetNeighbour(skidFacing);
            var nextHex = game.BattleMap?.GetHex(nextCoords);
            if (nextHex == null)
                break;

            var movementCost = nextHex.GetEnterMovementCost(currentHex);
            var fromPos = new HexPosition(currentCoords, skidFacing);
            var toPos = new HexPosition(nextCoords, skidFacing);
            var skidSegment = new PathSegment(fromPos, toPos, [])
            {
                Events = [new SegmentEvent(SegmentEventType.Skid)]
            };
            skidPathSegments.Add(skidSegment);

            remainingSkidDistance -= movementCost.Sum(c => c.Value);
            currentCoords = nextCoords;
            currentHex = nextHex;
        }

        return skidPathSegments;
    }
}

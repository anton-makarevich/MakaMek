using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

/// <summary>
/// Resolves the skid of a running 'Mech turning on hard pavement: the skid path, the skid PSR,
/// and the fall/skid commands that follow. Exposed as a <see cref="MovementInterruptContext"/>
/// extension so the handler stays a thin dispatcher.
/// </summary>
internal static class SkidInterruptExtensions
{
    extension(MovementInterruptContext context)
    {
        public MovementInterruptResult? CheckSkidInterrupt()
        {
            if (context.MoveCommand.MovementType != MovementType.Run) return null;

            var segment = context.MoveCommand.MovementPath[context.SegmentIndex];
            if (segment.From.Coordinates != segment.To.Coordinates) return null;

            var turnHex = context.Game.BattleMap?.GetHex(new HexCoordinates(segment.From.Coordinates));
            if (turnHex == null || !turnHex.HasHardPavement()) return null;

            if (segment.To.Coordinates == context.MoveCommand.MovementPath[^1].To.Coordinates) return null;

            if (context.Unit is not Mech mech) return null;

            var hexesMoved = CountHexesMoved(context);
            var maxSkidDistance = (int)Math.Ceiling(hexesMoved / 2.0);
            var skidResult = context.Game.GenerateSkidPath(segment.From, maxSkidDistance, mech);

            var skidFallContext = context.ResolveSkidFall(mech, segment, skidResult, hexesMoved);

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
        var modifiedCommand = SkidActionBuilder.BuildModifiedMoveCommand(
            context, skidResult.Segments, SegmentEventType.Skid, SegmentEventType.Fall);

        var actions = SkidActionBuilder.BuildFallActions(
            new MoveUnitAction(modifiedCommand, publish: true),
            mech,
            skidFallContext);

        return Stopped(actions);
    }

    private static MovementInterruptResult HandleSkidFailureWithCliffFall(
        MovementInterruptContext context,
        Mech mech,
        SkidPathResult skidResult,
        FallContextData skidFallContext)
    {
        var cliffFallContext = context.ResolveCliffFall(mech, skidResult, skidFallContext);

        var cliffSegments = skidResult.Segments.ToList();
        cliffSegments[^1] = cliffSegments[^1] with
        {
            Events = cliffSegments[^1].Events.Append(
                new SegmentEvent(SegmentEventType.Fall)).ToArray()
        };

        var modifiedCommand = SkidActionBuilder.BuildModifiedMoveCommand(
            context, cliffSegments, SegmentEventType.Skid, SegmentEventType.Fall);

        var actions = SkidActionBuilder.BuildFallActions(
            new MoveUnitAction(modifiedCommand, publish: true),
            mech,
            skidFallContext,
            cliffFallContext);

        return Stopped(actions);
    }

    private static MovementInterruptResult Stopped(List<IGameAction> actions) =>
        new()
        {
            ShouldStop = true,
            DeferStepConsumption = false,
            GameActions = actions
        };
}

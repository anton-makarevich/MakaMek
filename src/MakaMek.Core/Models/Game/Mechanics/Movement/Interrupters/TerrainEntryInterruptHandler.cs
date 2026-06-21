using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

public abstract class TerrainEntryInterruptHandler : IMovementInterruptHandler
{
    public MovementInterruptResult? Check(MovementInterruptContext context)
    {
        var segment = context.MoveCommand.MovementPath[context.SegmentIndex];
        if (segment.From.Coordinates == segment.To.Coordinates) return null;

        if (context.Unit is not Mech mech) return null;

        var rollContext = GetRollContext(context, segment);
        if (rollContext == null) return null;

        var fallContextData = context.Game.FallProcessor.ProcessMovementAttempt(
            mech, rollContext, context.Game, context.MoveCommand.MovementType);

        if (fallContextData.IsFalling)
        {
            var fallCommand = fallContextData.ToMechFallCommand();

            if (context.IsLandingCheck)
            {
                return new MovementInterruptResult
                {
                    ShouldStop = true,
                    GameActions =
                    [
                        new ApplyFallAction(mech, fallCommand)
                    ]
                };
            }

            var truncatedSegments = context.MoveCommand.MovementPath.Take(context.SegmentIndex + 1).ToList();
            var truncatedPath = new MovementPath(truncatedSegments, context.MoveCommand.MovementType)
                .WithLastSegmentEvent(new SegmentEvent(SegmentEventType.Fall));
            var truncatedCommand = context.MoveCommand with
            {
                MovementPath = truncatedPath.ToData(),
                IsCompleted = false
            };

            return new MovementInterruptResult
            {
                ShouldStop = true,
                GameActions =
                [
                    new MoveUnitAction(truncatedCommand, publish: false),
                    new ApplyFallAction(mech, fallCommand),
                    new FallBroadcastAction(mech, truncatedCommand)
                ]
            };
        }

        var psrCommand = fallContextData.ToMechFallCommand();
        return new MovementInterruptResult
        {
            ShouldStop = false,
            GameActions =
            [
                new PublishCommandAction(psrCommand)
            ]
        };
    }

    protected abstract PilotingSkillRollContext? GetRollContext(
        MovementInterruptContext context, PathSegmentData segment);
}

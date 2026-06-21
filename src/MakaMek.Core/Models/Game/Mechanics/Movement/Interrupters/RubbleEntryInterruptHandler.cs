using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

public class RubbleEntryInterruptHandler : IMovementInterruptHandler
{
    public MovementInterruptResult? Check(MovementInterruptContext context)
    {
        var segment = context.MoveCommand.MovementPath[context.SegmentIndex];
        if (segment.From.Coordinates == segment.To.Coordinates) return null;

        var destinationHex = context.Game.BattleMap?.GetHex(new HexCoordinates(segment.To.Coordinates));
        if (destinationHex?.GetTerrain(MakaMekTerrains.Rubble) == null) return null;

        if (context.Unit is not Mech mech) return null;

        var fallContextData = context.Game.FallProcessor.ProcessMovementAttempt(
            mech, new RubbleEntryRollContext(), context.Game, context.MoveCommand.MovementType);

        if (fallContextData.IsFalling)
        {
            var fallCommand = fallContextData.ToMechFallCommand();

            if (context.IsLandingCheck)
            {
                return new MovementInterruptResult
                {
                    ShouldStop = true,
                    GameActions = new List<IGameAction>
                    {
                        new ApplyFallAction(mech, fallCommand)
                    }
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
            GameActions = new List<IGameAction>
            {
                new PublishCommandAction(psrCommand)
            }
        };
    }
}

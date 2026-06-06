using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;

public class WaterEntryInterruptHandler : IMovementInterruptHandler
{
    public MovementInterruptResult? Check(MovementInterruptContext context)
    {
        var segment = context.MoveCommand.MovementPath[context.SegmentIndex];
        if (segment.From.Coordinates == segment.To.Coordinates) return null;

        var destinationHex = context.Game.BattleMap?.GetHex(new HexCoordinates(segment.To.Coordinates));
        if (destinationHex?.GetTerrain(MakaMekTerrains.Water) is not WaterTerrain { Height: <= -1 } water) return null;

        if (context.Unit is not Mech mech) return null;

        var waterDepth = -1 * water.Height;
        var fallContextData = context.Game.FallProcessor.ProcessMovementAttempt(
            mech, new EnteringDeepWaterRollContext(waterDepth), context.Game, context.MoveCommand.MovementType);

        if (fallContextData.IsFalling)
        {
            var truncatedSegments = context.MoveCommand.MovementPath.Take(context.SegmentIndex + 1).ToList();
            var truncatedPath = new MovementPath(truncatedSegments, context.MoveCommand.MovementType)
                .WithLastSegmentEvent(new SegmentEvent(SegmentEventType.Fall));
            var truncatedCommand = context.MoveCommand with
            {
                MovementPath = truncatedPath.ToData(),
                IsCompleted = false
            };

            var fallCommand = fallContextData.ToMechFallCommand();

            return new MovementInterruptResult
            {
                ShouldStop = true,
                GameActions =
                [
                    new MoveUnitAction(truncatedCommand, publish: false),
                    new ApplyFallAction(mech, fallCommand),
                    new WaterFallBroadcastAction(mech, truncatedCommand)
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

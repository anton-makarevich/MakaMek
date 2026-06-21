using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

public class WaterEntryInterruptHandler : IMovementInterruptHandler
{
    public MovementInterruptResult? Check(MovementInterruptContext context)
    {
        var segment = context.MoveCommand.MovementPath[context.SegmentIndex];
        if (segment.From.Coordinates == segment.To.Coordinates) return null;

        var destinationHex = context.Game.BattleMap?.GetHex(new HexCoordinates(segment.To.Coordinates));
        if (destinationHex?.GetTerrain(MakaMekTerrains.Water) is not WaterTerrain { Height: <= -1 } water) return null;

        var sourceHex = context.Game.BattleMap?.GetHex(new HexCoordinates(segment.From.Coordinates));
        if (sourceHex is not null && destinationHex.IsOnRoadOrBridge(sourceHex, (HexSurface)segment.From.Surface, (HexSurface)segment.To.Surface)) return null;

        if (context.Unit is not Mech mech) return null;

        var waterDepth = -1 * water.Height;
        var fallContextData = context.Game.FallProcessor.ProcessMovementAttempt(
            mech, new EnteringDeepWaterRollContext(waterDepth), context.Game, context.MoveCommand.MovementType);

        if (fallContextData.IsFalling)
        {
            var fallCommand = fallContextData.ToMechFallCommand();

            if (context.IsLandingCheck)
            {
                // Jump landing: movement already complete, no path truncation or broadcast needed
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

using System;
using System.Collections.Generic;
using System.Linq;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Units;
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

            var canStandup = mech.CanStandup();

            var broadcastCommand = truncatedCommand with
            {
                GameOriginId = context.Game.Id,
                IsCompleted = !canStandup
            };

            var fallCommand = fallContextData.ToMechFallCommand();

            var actions = new List<IGameAction>
            {
                new MoveUnitAction(truncatedCommand, publish: false),
                new PublishCommandAction(broadcastCommand),
                new ApplyFallAction(mech, fallCommand)
            };

            if (!canStandup && mech.Position != null)
            {
                var completionCommand = truncatedCommand with
                {
                    IsCompleted = true,
                    MovementPath = MovementPath.CreateSingleSegmentPath(mech.Position, truncatedCommand.MovementType).ToData()
                };
                actions.Add(new MoveUnitAction(completionCommand, publish: false));
            }

            return new MovementInterruptResult
            {
                ShouldStop = true,
                DeferStepConsumption = canStandup,
                GameActions = actions
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

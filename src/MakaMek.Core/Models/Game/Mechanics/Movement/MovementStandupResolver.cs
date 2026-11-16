using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;

/// <summary>
/// Processes a standup attempt: the error path, the piloting skill roll and the resulting fall or
/// standup. Extracted from <see cref="Phases.MovementPhase"/>.
/// </summary>
internal sealed class MovementStandupResolver(ServerGame game)
{
    public void Process(TryStandupCommand tryStandUpCommand)
    {
        var player = game.Players.FirstOrDefault(p => p.Id == tryStandUpCommand.PlayerId);

        if (player?.Units.FirstOrDefault(u => u.Id == tryStandUpCommand.UnitId) is not Mech unit)
        {
            game.CommandPublisher.PublishCommand(new ErrorCommand
            {
                GameOriginId = game.Id,
                IdempotencyKey = tryStandUpCommand.IdempotencyKey,
                ErrorCode = ErrorCode.InvalidGameState,
                Timestamp = DateTime.UtcNow
            });
            game.Logger.LogWarning("Unit not found");
            return;
        }

        game.CommandPublisher.PublishCommand(tryStandUpCommand with
        {
            GameOriginId = game.Id
        });

        // Check if the unit can stand up (has sufficient MP, pilot is conscious, etc.)
        if (!unit.CanStandup() || unit.Position == null)
        {
            return; // Cannot stand up
        }

        // Use the FallProcessor to process the standup attempt and get context data
        var fallContextData = game.FallProcessor.ProcessMovementAttempt(
            unit, new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt), game,
            MovementType.StandingStill);

        if (fallContextData.IsFalling)
        {
            // Standup failed - fall
            var action = new ApplyFallAction(unit, fallContextData.ToMechFallCommand());
            foreach (var command in action.Process(game))
                game.CommandPublisher.PublishCommand(command);
            return;
        }

        // Standup succeeded - stand up
        var standUpCommand = fallContextData.ToMechStandUpCommand(
            tryStandUpCommand.NewFacing, tryStandUpCommand.MovementTypeAfterStandup);
        if (standUpCommand == null) return;
        game.OnMechStandUp(standUpCommand.Value);
        game.CommandPublisher.PublishCommand(standUpCommand);
    }
}

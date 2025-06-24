using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public class MovementPhase(ServerGame game) : MainGamePhase(game)
{
    public override void HandleCommand(IGameCommand command)
    {
        switch (command)
        {
            case MoveUnitCommand moveCommand:
                HandleUnitAction(command, moveCommand.PlayerId);
                break;
            case TryStandupCommand standupCommand:
                ProcessStandupCommand(standupCommand);  //HandleUnitAction moves to the new unit, should not happen here
                break;
        }
    }

    protected override void ProcessCommand(IGameCommand command)
    {
        switch (command)
        {
            case MoveUnitCommand moveCommand:
                ProcessMoveCommand(moveCommand);
                break;
        }
    }

    private void ProcessMoveCommand(MoveUnitCommand moveCommand)
    {
        var broadcastCommand = moveCommand;
        broadcastCommand.GameOriginId = Game.Id;
        Game.OnMoveUnit(moveCommand);
        Game.CommandPublisher.PublishCommand(broadcastCommand);
    }

    private void ProcessStandupCommand(TryStandupCommand standupCommand)
    {
        // Find the unit
        var player = Game.Players.FirstOrDefault(p => p.Id == standupCommand.PlayerId);

        if (player?.Units.FirstOrDefault(u => u.Id == standupCommand.UnitId) is not Mech unit) return;

        // Use the FallProcessor to process the standup attempt and get context data
        var fallContextData = Game.FallProcessor.ProcessStandupAttempt(unit, Game);
        
        // Create and publish the appropriate command based on the result
        if (fallContextData.IsFalling)
        {
            // Standup failed - create and publish a fall command
            var fallCommand = fallContextData.ToMechFallCommand();
            Game.CommandPublisher.PublishCommand(fallCommand);
            Game.OnMechFalling(fallCommand);
        }
        else
        {
            // Standup succeeded - create and publish a standup command
            var standUpCommand = fallContextData.ToMechStandUpCommand();
            if (standUpCommand == null) return;
            Game.CommandPublisher.PublishCommand(standUpCommand);
            Game.OnMechStandUp(standUpCommand.Value);
        }
    }

    public override PhaseNames Name => PhaseNames.Movement;
}

using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Units;
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
        // Check if PSR is required for jumping with damaged gyro
        var player = Game.Players.FirstOrDefault(p => p.Id == moveCommand.PlayerId);
        // Find the unit
        var unit = player?.Units.FirstOrDefault(u => u.Id == moveCommand.UnitId) as Mech;
        if (unit?.IsPsrForJumpRequired() == true && moveCommand.MovementType == MovementType.Jump)
        {
            ProcessJumpWithDamagedGyro(moveCommand, unit);
            return;
        }

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

        // Check if unit can stand up (has sufficient MP, pilot is conscious, etc.)
        if (!unit.CanStandup())
        {
            return; // Cannot stand up
        }

        // Use the FallProcessor to process the standup attempt and get context data
        var fallContextData = Game.FallProcessor.ProcessMovementAttempt(unit, FallReasonType.StandUpAttempt, Game);
        
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

    private void ProcessJumpWithDamagedGyro(MoveUnitCommand moveCommand, Unit? unit)
    {
        // Use the FallProcessor to process the jump attempt with damaged gyro
        if (unit is not Mech mech) return;
        var fallContextData = Game.FallProcessor.ProcessMovementAttempt(mech, FallReasonType.JumpWithDamagedGyro, Game);
        
        // Create and publish the appropriate command based on the result
        if (fallContextData.IsFalling)
        {
            // Jump failed - create and publish a fall command
            var fallCommand = fallContextData.ToMechFallCommand();
            Game.CommandPublisher.PublishCommand(fallCommand);
            Game.OnMechFalling(fallCommand);
        }
        else
        {
            // Jump succeeded - process the movement normally
            var broadcastCommand = moveCommand;
            broadcastCommand.GameOriginId = Game.Id;
            Game.OnMoveUnit(moveCommand);
            Game.CommandPublisher.PublishCommand(broadcastCommand);
        }
    }

    public override PhaseNames Name => PhaseNames.Movement;
}

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

        var broadcastCommand = moveCommand;
        broadcastCommand.GameOriginId = Game.Id;
        Game.OnMoveUnit(moveCommand);
        Game.CommandPublisher.PublishCommand(broadcastCommand);

        if (unit?.IsPsrForJumpRequired() != true || moveCommand.MovementType != MovementType.Jump) return;
        ProcessJumpWithDamage(unit);
    }

    private void ProcessStandupCommand(TryStandupCommand tryStandUpCommand)
    {
        // Find the unit
        var player = Game.Players.FirstOrDefault(p => p.Id == tryStandUpCommand.PlayerId);

        if (player?.Units.FirstOrDefault(u => u.Id == tryStandUpCommand.UnitId) is not Mech unit) return;

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
            var standUpCommand = fallContextData.ToMechStandUpCommand(tryStandUpCommand.NewFacing);
            if (standUpCommand == null) return;
            Game.CommandPublisher.PublishCommand(standUpCommand);
            Game.OnMechStandUp(standUpCommand.Value);
        }

        if (unit.GetMovementPoints(tryStandUpCommand.MovementTypeAfterStandup) > 0) return;
        var moveCommand = new MoveUnitCommand
        {
            MovementType = tryStandUpCommand.MovementTypeAfterStandup,
            GameOriginId = Game.Id,
            PlayerId = tryStandUpCommand.PlayerId,
            UnitId = tryStandUpCommand.UnitId,
            MovementPath = []
        };
        Game.CommandPublisher.PublishCommand(moveCommand);
    }

    private void ProcessJumpWithDamage(Unit? unit)
    {
        // Use the FallProcessor to process the jump attempt with damaged gyro
        if (unit is not Mech mech) return;
        var fallContextData = Game.FallProcessor.ProcessMovementAttempt(mech, FallReasonType.JumpWithDamage, Game);
        
        if (!fallContextData.IsFalling) return;
        // Jump failed - create and publish a fall command
        var fallCommand = fallContextData.ToMechFallCommand();
        Game.CommandPublisher.PublishCommand(fallCommand);
        Game.OnMechFalling(fallCommand);
    }

    public override PhaseNames Name => PhaseNames.Movement;
}

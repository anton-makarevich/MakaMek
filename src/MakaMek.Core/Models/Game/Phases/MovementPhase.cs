using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
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
                HandleUnitAction(command, standupCommand.PlayerId);
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
            case TryStandupCommand standupCommand:
                ProcessStandupCommand(standupCommand);
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
        var unit = player?.Units.FirstOrDefault(u => u.Id == standupCommand.UnitId);
        
        if (unit == null) return;

        // Calculate PSR for standup attempt, TODO pass rollType to calculate modifiers
        var psrBreakdown = Game.PilotingSkillCalculator.GetPsrBreakdown(unit, []);
        
        // Roll 2D6
        var diceResults = Game.DiceRoller.Roll2D6();
        var rollTotal = diceResults.Sum(d => d.Result);
        
        // Check if successful (roll >= target number)
        var isSuccessful = rollTotal >= psrBreakdown.ModifiedPilotingSkill;
        
        var pilotingSkillRollData = new PilotingSkillRollData
        {
            RollType = PilotingSkillRollType.StandupAttempt,
            DiceResults = diceResults.Select(d => d.Result).ToArray(),
            IsSuccessful = isSuccessful,
            PsrBreakdown = psrBreakdown
        };

        // If successful, stand the mech up
        if (!isSuccessful || unit is not Mech) return;
        // Send result command to all clients
        var resultCommand = new MechStandedUpCommand
        {
            GameOriginId = Game.Id,
            Timestamp = DateTime.UtcNow,
            UnitId = unit.Id,
            PilotingSkillRoll = pilotingSkillRollData,
            IsSuccessful = isSuccessful
        };

        Game.CommandPublisher.PublishCommand(resultCommand);
            
        Game.OnMechStandedUp(resultCommand);
    }

    public override PhaseNames Name => PhaseNames.Movement;
}

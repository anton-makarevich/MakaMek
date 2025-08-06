using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public class EndPhase(ServerGame game) : GamePhase(game)
{
    private readonly HashSet<Guid> _playersEndedTurn = new();

    public override void Enter()
    {
        // Clear the set of players who have ended their turn
        _playersEndedTurn.Clear();

        // Process consciousness recovery rolls for unconscious pilots
        ProcessConsciousnessRecoveryRolls();
    }

    public override void HandleCommand(IGameCommand command)
    {
        if (command is not TurnEndedCommand turnEndedCommand) return;
        
        // Verify the player is in the game
        var player = Game.Players.FirstOrDefault(p => p.Id == turnEndedCommand.PlayerId);
        if (player == null) return;
        
        // Record that this player has ended their turn
        _playersEndedTurn.Add(turnEndedCommand.PlayerId);
        
        // Broadcast the command to all clients
        var broadcastCommand = turnEndedCommand;
        broadcastCommand.GameOriginId = Game.Id;
        // Call the OnTurnEnded method on the BaseGame class
        Game.OnTurnEnded(turnEndedCommand);
        Game.CommandPublisher.PublishCommand(broadcastCommand);
        
        if (HaveAllPlayersEndedTurn())
        {
            // All players have ended their turns, start a new turn
            Game.IncrementTurn();
            
            // Transition to the next phase using the phase manager
            Game.TransitionToNextPhase(Name);
        }
    }
    
    private bool HaveAllPlayersEndedTurn()
    {
        // Check if all players in the game have ended their turn
        return Game.AlivePlayers.All(player => _playersEndedTurn.Contains(player.Id));
    }

    /// <summary>
    /// Processes consciousness recovery rolls for unconscious pilots
    /// Recovery attempts are made for pilots who became unconscious in previous turns
    /// </summary>
    private void ProcessConsciousnessRecoveryRolls()
    {
        foreach (var player in Game.AlivePlayers)
        {
            foreach (var unit in player.AliveUnits)
            {
                if (unit.Pilot == null) continue;

                // Only attempt recovery for pilots who became unconscious in previous turns
                if (unit.Pilot.IsConscious ||
                    unit.Pilot.IsDead ||
                    unit.Pilot.UnconsciousInTurn == null ||
                    unit.Pilot.UnconsciousInTurn >= Game.Turn)
                {
                    continue;
                }

                var recoveryCommand = Game.ConsciousnessCalculator.MakeRecoveryConsciousnessRoll(unit.Pilot);
                if (recoveryCommand == null) continue;

                var broadcastCommand = recoveryCommand.Value;
                broadcastCommand.GameOriginId = Game.Id;
                Game.OnPilotConsciousnessRoll(broadcastCommand);
                Game.CommandPublisher.PublishCommand(broadcastCommand);
            }
        }
    }

    public override PhaseNames Name => PhaseNames.End;
}

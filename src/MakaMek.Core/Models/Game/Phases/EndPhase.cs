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

    public override PhaseNames Name => PhaseNames.End;
}

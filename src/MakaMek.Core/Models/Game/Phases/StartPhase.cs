using Sanet.MakaMek.Core.Models.Game.Commands;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public class StartPhase(ServerGame game) : GamePhase(game)
{
    public override void HandleCommand(IGameCommand command)
    {
        switch (command)
        {
            case JoinGameCommand joinGameCommand:
                var broadcastJoinCommand = joinGameCommand;
                broadcastJoinCommand.GameOriginId = Game.Id;
                Game.OnPlayerJoined(joinGameCommand);
                Game.CommandPublisher.PublishCommand(broadcastJoinCommand);
                break;
            case UpdatePlayerStatusCommand playerStatusCommand:
                var broadcastStatusCommand = playerStatusCommand;
                broadcastStatusCommand.GameOriginId = Game.Id;
                Game.OnPlayerStatusUpdated(playerStatusCommand);
                Game.CommandPublisher.PublishCommand(broadcastStatusCommand);
                TryTransitionToNextPhase();
                break;
        }
    }

    private bool AllPlayersReady()
    {
        return Game.Players.Count > 0 && 
               Game.Players.Count(p => p.Status == PlayerStatus.Playing) == Game.Players.Count;
    }

    public override PhaseNames Name => PhaseNames.Start;

    public void TryTransitionToNextPhase()
    {
        // Check if all players are ready AND the map is set
        if (AllPlayersReady() && Game.BattleMap != null)
        {
            Game.TransitionToNextPhase(Name);
        }
    }
}

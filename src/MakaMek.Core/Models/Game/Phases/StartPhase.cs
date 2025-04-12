using Sanet.MakaMek.Core.Models.Game.Commands;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Data.Units;

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
            case RequestGameLobbyStatusCommand requestCommand:
                // Send information about all currently joined players to the requesting client
                //SendLobbyStatusToClients();
                break;
        }
    }

    private void SendLobbyStatusToClient()
    {
        // For each player in the game, send a JoinGameCommand to the requesting client
        foreach (var player in Game.Players)
        {
            // Create units data from player's units
            var unitDataList = new List<UnitData>();
            foreach (var unit in player.Units)
            {
                // Assuming there's a way to convert a unit to UnitData
                // This might need adjustment based on your actual implementation
                unitDataList.Add(unit.ToData());
            }
            
            // Create and send a JoinGameCommand for this player
            var joinCommand = new JoinGameCommand
            {
                PlayerId = player.Id,
                PlayerName = player.Name,
                Tint = player.Tint,
                Units = unitDataList,
                GameOriginId = Game.Id,
                Timestamp = DateTime.UtcNow
            };
            
            // Send directly to the requesting client
            Game.CommandPublisher.PublishCommand(joinCommand);
        }
    }

    private bool AllPlayersReady()
    {
        return Game.Players.Count > 0 && 
               Game.Players.Count(p => p.Status == PlayerStatus.Ready) == Game.Players.Count;
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

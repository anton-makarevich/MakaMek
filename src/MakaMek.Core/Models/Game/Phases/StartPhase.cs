using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
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
                Game.Logger.LogInformation("JoinGameCommand received: {PlayerName}", joinGameCommand.PlayerName);
                var broadcastJoinCommand = joinGameCommand with { GameOriginId = Game.Id };
                Game.OnPlayerJoined(joinGameCommand);
                Game.CommandPublisher.PublishCommand(broadcastJoinCommand);
                break;
            case UpdatePlayerStatusCommand playerStatusCommand:
                Game.Logger.LogInformation("UpdatePlayerStatusCommand received: {PlayerId} - {PlayerStatus}", playerStatusCommand.PlayerId, playerStatusCommand.PlayerStatus);
                var broadcastStatusCommand = playerStatusCommand with { GameOriginId = Game.Id };
                Game.OnPlayerStatusUpdated(playerStatusCommand);
                Game.CommandPublisher.PublishCommand(broadcastStatusCommand);
                TryTransitionToNextPhase();
                break;
            case RequestGameLobbyStatusCommand:
                // Send information about all currently joined players to the requesting client
                SendLobbyStatusToClients();
                break;
        }
    }

    private void SendLobbyStatusToClients()
    {
        // For each player in the game, send a JoinGameCommand to the requesting client
        foreach (var player in Game.Players)
        {
            // Create units data from player's units
            var unitDataList = new List<UnitData>();
            var pilotAssignments = new List<PilotAssignmentData>();
            foreach (var unit in player.Units)
            {
                unitDataList.Add(unit.ToData());
                pilotAssignments.Add(new PilotAssignmentData
                {
                    UnitId = unit.Id,
                    PilotData = unit.Pilot!.ToData()
                });
            }
            
            // Create and send a JoinGameCommand for this player
            var joinCommand = new JoinGameCommand
            {
                PlayerId = player.Id,
                PlayerName = player.Name,
                Tint = player.Tint,
                Units = unitDataList,
                GameOriginId = Game.Id,
                Timestamp = DateTime.UtcNow,
                PilotAssignments = pilotAssignments
            };
            
            // Send it to the requesting client
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

using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.ViewModels.Setup;

/// <summary>
/// Applies the lobby commands the host receives while the setup screen is open: player-status updates
/// and joins (local echoes and remote players). Kept out of the hosting view model so it only forwards
/// the command without knowing the lobby command surface.
/// </summary>
public sealed class LobbyCommandHandler
{
    private readonly IGameManager _gameManager;
    private readonly GameSetupSession _session;

    public LobbyCommandHandler(IGameManager gameManager, GameSetupSession session)
    {
        _gameManager = gameManager;
        _session = session;
    }

    public Task HandleAsync(IGameCommand command, Action<string> notify)
    {
        switch (command)
        {
            // Handle player joining (potentially echo of local or a remote player)
            case UpdatePlayerStatusCommand statusCmd:
                var playerWithStatusUpdate = _session.Players.FirstOrDefault(p => p.Player.Id == statusCmd.PlayerId);
                if (playerWithStatusUpdate != null && statusCmd.GameOriginId == _gameManager.ServerGameId)
                {
                    // Update player status
                    playerWithStatusUpdate.Player.Status = statusCmd.PlayerStatus;
                    playerWithStatusUpdate.RefreshStatus();
                    notify(nameof(HostingSetupController.CanStartGame));
                    notify(nameof(HostingSetupController.CanChangeHostMode));
                }
                break;

            case JoinGameCommand joinCmd:
                var existingPlayerVm = _session.Players.FirstOrDefault(p => p.Player.Id == joinCmd.PlayerId);
                if (existingPlayerVm != null)
                {
                    // Player exists - likely the echo for a local player who just clicked Join
                    if (existingPlayerVm.IsLocalPlayer && joinCmd.GameOriginId == _gameManager.ServerGameId)
                    {
                        // Server accepted the join request
                        existingPlayerVm.Player.Status = PlayerStatus.Joined;
                        existingPlayerVm.RefreshStatus();
                        notify(nameof(HostingSetupController.CanStartGame));
                        notify(nameof(HostingSetupController.CanChangeHostMode));
                    }
                    // Else: Remote player sending join again? Ignore.
                }
                else
                {
                    // Player doesn't exist - must be a remote player joining
                    var remotePlayer = new Player(joinCmd.PlayerId,
                        joinCmd.PlayerName,
                        PlayerControlType.Remote,
                        joinCmd.Tint);
                    var remotePlayerVm = new PlayerViewModel(
                        remotePlayer,
                        isLocalPlayer: false, // Mark as remote
                        _ => { }, // No join action needed for remote
                        _ => { }, // No set ready action needed for remote
                        _ => Task.CompletedTask, // No show units action needed for remote
                        onUnitChanged: () => notify(nameof(HostingSetupController.CanStartGame)));

                    remotePlayerVm.AddUnits(joinCmd.Units, joinCmd.PilotAssignments); // Add units received from command
                    _session.Players.Add(remotePlayerVm);
                    notify(nameof(NewGameViewModel.CanAddPlayer));
                    notify(nameof(HostingSetupController.CanStartGame));
                }
                break;
        }

        return Task.CompletedTask;
    }
}


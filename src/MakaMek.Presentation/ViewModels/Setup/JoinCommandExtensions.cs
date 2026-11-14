using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.ViewModels.Setup;

/// <summary>
/// Applies the lobby commands the join screen receives: player-status updates, remote joins, the
/// lobby map preview and the navigation to the battle map once the game starts. Expressed as
/// extensions so the controller only forwards the command.
/// </summary>
public static class JoinCommandExtensions
{
    extension(JoinSetupController controller)
    {
        public async Task HandleLobbyCommand(IGameCommand command, SetupDependencies deps)
        {
            switch (command)
            {
                case UpdatePlayerStatusCommand statusCmd:
                    var playerWithStatusUpdate = deps.Session.Players.FirstOrDefault(p => p.Player.Id == statusCmd.PlayerId);
                    if (playerWithStatusUpdate != null) // Simplified check for join view
                    {
                        playerWithStatusUpdate.Player.Status = statusCmd.PlayerStatus;
                        playerWithStatusUpdate.RefreshStatus();
                    }
                    break;

                case JoinGameCommand joinCmd:
                    var existingPlayerVm = deps.Session.Players.FirstOrDefault(p => p.Player.Id == joinCmd.PlayerId);
                    if (existingPlayerVm == null) // Add if it's a new remote player
                    {
                        var newRemotePlayer = new Player(joinCmd.PlayerId,
                            joinCmd.PlayerName,
                            PlayerControlType.Remote,
                            joinCmd.Tint);
                        var remotePlayerViewModel = new PlayerViewModel(
                            newRemotePlayer,
                            isLocalPlayer: false,
                            _ => { }, // Remote players don't publish join
                            _ => { }); // No callback for ready state
                        remotePlayerViewModel.AddUnits(joinCmd.Units, joinCmd.PilotAssignments); // Add units received from command
                        deps.Session.Players.Add(remotePlayerViewModel);
                    }
                    else if (existingPlayerVm.IsLocalPlayer)
                    {
                        // Handle join confirmation for local player
                        existingPlayerVm.Player.Status = PlayerStatus.Joined;
                        existingPlayerVm.RefreshStatus();
                    }
                    break;

                case SetBattleMapCommand setBattleMapCommand:
                    // The ClientGame has already applied the map; show it as a lobby preview.
                    // Navigation to the battle map happens when the game actually starts.
                    var lobbyPreviewMap = deps.MapFactory.CreateFromData(setBattleMapCommand.MapData);
                    await controller.ApplyLobbyPreview(lobbyPreviewMap);
                    return;

                case ChangePhaseCommand phaseCommand:
                    // The game start signal: navigate to the battle map once the phase leaves Start
                    if (phaseCommand.Phase == PhaseNames.Start) break;

                    var navigationService = controller.NavigationServiceAccessor?.Invoke();
                    var battleMapViewModel = navigationService?.GetViewModel<BattleMapViewModel>();
                    if (battleMapViewModel == null)
                    {
                        throw new Exception("BattleMapViewModel is not registered");
                    }
                    battleMapViewModel.Game = deps.Session.LocalGame;

                    // The lobby view is done: stop listening to game commands so this VM is not
                    // invoked again during gameplay (re-navigating would recreate the battle map
                    // view and detach its subscriptions). A new join re-subscribes in JoinRoom.
                    if (controller.CommandStreamHandler is { } handler) deps.CommandPublisher.Unsubscribe(handler);

                    await navigationService!.NavigateToViewModelAsync(battleMapViewModel);
                    return;
            }

            // Refresh bindings
            controller.NotifyExternal(nameof(NewGameViewModel.Players));
        }

        private async Task ApplyLobbyPreview(BattleMap? map)
        {
            await controller.Preview.UpdateAsync(map);
            controller.NotifyExternal(nameof(JoinSetupController.PreviewMap));
            controller.NotifyExternal(nameof(JoinSetupController.PreviewImage));
            controller.NotifyExternal(nameof(JoinSetupController.HasLobbyMapPreview));
        }
    }
}

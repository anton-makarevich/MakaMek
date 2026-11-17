using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.Models.Logger;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.Transport.SignalR.Client.Relay;

namespace Sanet.MakaMek.Presentation.ViewModels.Setup;

/// <summary>
/// The join screen's transport workflow (LAN/online connect, retry, disconnect), the clipboard
/// room-code autofill support and the lobby commands. Expressed as extensions over
/// <see cref="JoinSetupController"/> so the controller stays a small observable facade.
/// </summary>
public static class JoinLifecycleExtensions
{
    extension(JoinSetupController controller)
    {
        public Task ConnectToServer(SetupDependencies deps)
        {
            if (controller.CanConnectAccessor?.Invoke() != true) return Task.CompletedTask;
            return controller.ConnectToLan(
                deps,
                controller.ServerIpAccessor?.Invoke() ?? string.Empty,
                controller.ServerAddressAccessor?.Invoke() ?? string.Empty);
        }

        public Task JoinRoom(SetupDependencies deps)
        {
            if (controller.CanJoinAccessor?.Invoke() != true) return Task.CompletedTask;
            return controller.JoinOnline(deps, controller.RoomCodeAccessor?.Invoke() ?? string.Empty);
        }

        public Task RetryConnection(SetupDependencies deps)
        {
            if (!controller.CanRetryConnection) return Task.CompletedTask;
            return controller.Retry(deps);
        }

        public async Task ConnectToLan(SetupDependencies deps, string serverIp, string serverAddress)
        {
            controller.InvalidatePendingClipboardRead();

            try
            {
                await controller.ConnectCore(deps, serverIp, serverAddress);
            }
            finally
            {
                controller.RaiseConnectionStateChanged();
            }
        }

        private async Task ConnectCore(SetupDependencies deps, string serverIp, string serverAddress)
        {
            try
            {
                await deps.GameConnector.ConnectToLan(serverAddress);
                if (!deps.GameConnector.IsConnected) return;

                deps.CommandPublisher.Subscribe(controller.CommandStreamHandler!);
                controller.SubscribeToOnlineStatus(deps);
                deps.Session.CreateAndInitializeLocalGame(deps.CommandPublisher);

                deps.Session.LocalGame!.Logger.LogAttemptedToConnectToServerIp(serverIp);
                deps.Session.LocalGame.RequestLobbyStatus(new RequestGameLobbyStatusCommand
                {
                    GameOriginId = deps.Session.LocalGame.Id
                });
            }
            catch (Exception ex)
            {
                if (controller.CommandStreamHandler is { } handler) deps.CommandPublisher.Unsubscribe(handler);
                deps.Session.LocalGame?.Logger.LogError(ex, "Error connecting to server: {Message}", ex.Message);
                await deps.GameConnector.Disconnect();
            }
        }

        public async Task JoinOnline(SetupDependencies deps, string roomCode)
        {
            controller.InvalidatePendingClipboardRead();

            controller.IsJoining = true;
            controller.JoinError = null;

            CancellationTokenSource? joinCts = null;
            try
            {
                joinCts = new CancellationTokenSource();
                controller.ActiveJoinCts = joinCts;

                // A new device joins without a session token; the Hub mints a device session.
                await deps.GameConnector.JoinOnline(roomCode, sessionToken: null, joinCts.Token);
                if (!controller.IsOnlineMode)
                {
                    // A newer join superseded this one; clean up any connection it left behind
                    if (!ReferenceEquals(controller.ActiveJoinCts, joinCts))
                        await deps.GameConnector.Disconnect();
                    return;
                }
                if (!deps.GameConnector.IsConnected)
                {
                    controller.JoinError = GetJoinErrorText(deps, deps.GameConnector.OnlineError);
                    return;
                }

                deps.CommandPublisher.Subscribe(controller.CommandStreamHandler!);
                controller.SubscribeToOnlineStatus(deps);
                deps.Session.CreateAndInitializeLocalGame(deps.CommandPublisher, deps.GameConnector.ConnectedHostGameId);

                deps.Session.LocalGame!.RequestLobbyStatus(new RequestGameLobbyStatusCommand
                {
                    GameOriginId = deps.Session.LocalGame.Id
                });
            }
            catch (OperationCanceledException)
            {
                deps.Logger.LogInformation("Online game join was cancelled");
            }
            catch (Exception ex)
            {
                if (controller.CommandStreamHandler is { } handler) deps.CommandPublisher.Unsubscribe(handler);
                deps.Logger.LogError(ex, "Error joining online game");
                await deps.GameConnector.Disconnect();
                controller.JoinError = GetJoinErrorText(deps, deps.GameConnector.OnlineError);
            }
            finally
            {
                // Only clear the token source we own; a newer join may have replaced it
                if (ReferenceEquals(controller.ActiveJoinCts, joinCts))
                {
                    controller.ActiveJoinCts?.Dispose();
                    controller.ActiveJoinCts = null;
                }
                controller.IsJoining = false;
                controller.RaiseConnectionStateChanged();
            }
        }

        public async Task Retry(SetupDependencies deps)
        {
            controller.IsJoining = true;
            controller.JoinError = null;
            try
            {
                await deps.GameConnector.Reconnect();
                if (!deps.GameConnector.IsConnected)
                    controller.JoinError = GetJoinErrorText(deps, deps.GameConnector.OnlineError);
            }
            catch (Exception ex)
            {
                deps.Logger.LogError(ex, "Error retrying connection");
                controller.JoinError = GetJoinErrorText(deps, deps.GameConnector.OnlineError);
            }
            finally
            {
                controller.IsJoining = false;
                controller.RaiseConnectionStateChanged();
            }
        }

        public async Task Disconnect(SetupDependencies deps)
        {
            controller.UnsubscribeFromOnlineStatus(deps);
            if (controller.ActiveJoinCts != null)
                await controller.ActiveJoinCts.CancelAsync();
            if (deps.Session.LocalGame != null)
            {
                deps.Session.LocalGame.Dispose();
                deps.Session.LocalGame = null;
            }
            if (controller.CommandStreamHandler is { } handler) deps.CommandPublisher.Unsubscribe(handler);
            await deps.GameConnector.Disconnect();
            controller.RaiseConnectionStateChanged();
        }

        private void SubscribeToOnlineStatus(SetupDependencies deps) =>
            controller.ConnectionStatus.Subscribe(
                deps.GameConnector.OnlineConnectionStatus,
                deps.DispatcherService.Scheduler);

        internal void UnsubscribeFromOnlineStatus(SetupDependencies deps) =>
            controller.ConnectionStatus.Subscribe(null, deps.DispatcherService.Scheduler);
    }

    private static string GetJoinErrorText(SetupDependencies deps, RelayClientError? error)
    {
        var key = error?.Code switch
        {
            RelayClientErrorCode.RoomNotFound => "Join_InvalidCode",
            RelayClientErrorCode.RoomExpired => "Join_RoomExpired",
            RelayClientErrorCode.HostNotReady => "Join_HostNotReady",
            RelayClientErrorCode.RoomFull => "Join_RoomFull",
            RelayClientErrorCode.HubAtCapacity => "Join_HubAtCapacity",
            RelayClientErrorCode.RateLimited => "Join_RateLimited",
            RelayClientErrorCode.NetworkError or RelayClientErrorCode.Timeout => "Join_ConnectionFailed",
            RelayClientErrorCode.ConfigurationError => "Join_ConfigurationError",
            _ => "Join_Failed"
        };
        return deps.LocalizationService.GetString(key);
    }
}






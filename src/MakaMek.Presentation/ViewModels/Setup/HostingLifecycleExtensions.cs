using AsyncAwaitBestPractices;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MVVM.Core.Services;

namespace Sanet.MakaMek.Presentation.ViewModels.Setup;

/// <summary>
/// The hosting lifecycle and hosting texts, expressed as extensions over
/// <see cref="HostingSetupController"/> so the controller stays a small observable facade and these
/// transport-heavy dependencies are not part of its coupling.
/// </summary>
public static class HostingLifecycleExtensions
{
    extension(HostingSetupController controller)
    {
        /// <summary>
        /// Subscribes to the lobby map changes.
        /// </summary>
        public void SubscribeToMapChanges(SetupDependencies deps) =>
            GetOrCreateBroadcaster(controller, deps).Subscribe();

        /// <summary>
        /// Resets the hosting display state for the default mode of this platform.
        /// </summary>
        public void ResetHostingState(SetupDependencies deps)
        {
            var defaultMode = deps.GameManager.CanStartLanServer ? HostMode.Lan : HostMode.Online;
            if (controller.CurrentHostMode != defaultMode)
            {
                controller.ApplyHostMode(defaultMode);
            }
            controller.SetMultiplayerEnabled(false);
            controller.ClearHostingState();
        }

        /// <summary>
        /// Cancels any pending lobby initialization and stops hosting unless the game already started.
        /// </summary>
        public void Detach(SetupDependencies deps)
        {
            controller.CancelPendingInitialization();
            // Once the game has started, the hosting session belongs to the running game;
            // stopping it here would disconnect the host's relay/LAN transport mid-game.
            if (controller.IsMultiplayerEnabled && !deps.GameManager.IsGameStarted)
            {
                deps.GameManager.StopHosting().SafeFireAndForget(
                    ex => deps.Logger.LogError(ex, "Error stopping hosting on detach"));
            }
            controller.UnsubscribeFromOnlineStatus(deps);
            GetOrCreateBroadcaster(controller, deps).Unsubscribe();
            if (controller.CommandStreamHandler is { } handler) deps.CommandPublisher.Unsubscribe(handler);
        }

        public async Task InitializeLobbyAndSubscribe(SetupDependencies deps, CancellationToken cancellationToken)
        {
            if (controller.IsOnlineMode)
            {
                await InitializeOnlineLobbyAndSubscribe(controller, deps, cancellationToken);
                return;
            }

            await InitializeLanLobbyAndSubscribe(controller, deps, cancellationToken);
        }

        private async Task InitializeLanLobbyAndSubscribe(
            SetupDependencies deps,
            CancellationToken cancellationToken)
        {
            await deps.GameManager.InitializeLobby(cancellationToken);
            if (cancellationToken.IsCancellationRequested || controller.IsDisposed)
            {
                controller.SetMultiplayerEnabled(false);
                return;
            }

            controller.SubscribeAndCreateLocalGame(deps);

            // Update server IP initially if needed
            controller.NotifyChanged(nameof(HostingSetupController.CanStartLanServer));
        }

        private async Task InitializeOnlineLobbyAndSubscribe(
            SetupDependencies deps,
            CancellationToken cancellationToken)
        {
            // Probe the active hub up front so its name/status render immediately and
            // in parallel with the room-creation round trips below, instead of leaving
            // the online section empty until InitializeLobbyOnline completes.
            controller.ResolveActiveHubAndProbe(deps, cancellationToken).SafeFireAndForget(
                ex => deps.Logger.LogError(ex, "Error probing active hub"));

            await deps.GameManager.InitializeLobbyOnline(cancellationToken);
            if (cancellationToken.IsCancellationRequested || controller.IsDisposed)
                return;

            if (deps.GameManager.OnlineError != null || deps.GameManager.RoomCode == null)
            {
                controller.RoomCode = null;
                controller.HostingError = deps.GameManager.OnlineError?.Message
                    ?? deps.LocalizationService.GetString("Hosting_Failed");
                return;
            }

            controller.RoomCode = deps.GameManager.RoomCode;
            controller.HostingError = null;

            controller.SubscribeToOnlineStatus(deps);
            controller.SubscribeAndCreateLocalGame(deps);
        }

        private async Task ResolveActiveHubAndProbe(
            SetupDependencies deps,
            CancellationToken cancellationToken)
        {
            var probe = new HostingHubProbe(
                deps.HubConfigurationProvider,
                deps.RelayRoomClient,
                deps.Logger);
            var entry = await probe.ResolveActiveHubAsync(
                cancellationToken,
                () => controller.IsOnlineMode,
                () => controller.IsDisposed);
            controller.ActiveHub = entry;
            if (entry != null)
            {
                await entry.RefreshStatusAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Restarts hosting for the currently selected mode. No-op while any player has joined.
        /// </summary>
        public async Task CancelAndRestartServerAsync(SetupDependencies deps)
        {
            if (controller.HasJoinedPlayers || controller.IsDisposed) return;

            var currentInitCts = controller.InitCts;
            if (currentInitCts is not null)
            {
                await currentInitCts.CancelAsync();
                if (controller.HasJoinedPlayers) return; // A player joined while cancellation was pending; keep the live lobby
                // A concurrent restart may have replaced the source while we were awaiting;
                // only dispose and replace it if it is still the active one.
                if (!ReferenceEquals(controller.InitCts, currentInitCts)) return;
            }
            currentInitCts?.Dispose();
            controller.InitCts = new CancellationTokenSource();

            try
            {
                controller.IsHostingActive = true;
                try
                {
                    await controller.InitializeLobbyAndSubscribe(deps, controller.InitCts.Token);
                    // Multiplayer is only considered enabled once hosting completed
                    // without errors, cancellation, or disposal; a failed or cancelled
                    // initialization keeps the enable command available for retry.
                    controller.SetMultiplayerEnabled(controller.HostingError == null
                        && !controller.InitCts.IsCancellationRequested
                        && !controller.IsDisposed);
                }
                catch (OperationCanceledException)
                {
                    deps.Logger.LogDebug("Lobby initialization cancelled by superseded restart or detach/dispose");
                    controller.SetMultiplayerEnabled(false);
                }
                catch (Exception ex)
                {
                    deps.Logger.LogError(ex, "Error initializing lobby during restart");
                    controller.SetMultiplayerEnabled(false);
                }
            }
            finally
            {
                controller.IsHostingActive = false;
            }
        }

        public void RestartLocalLobbyInitialization(SetupDependencies deps)
        {
            controller.NotifyChanged(nameof(HostingSetupController.CanStartLanServer));
            controller.NotifyChanged(nameof(HostingSetupController.IsHostModeSelectorVisible));
            controller.CancelPendingInitialization();
            controller.InitCts = new CancellationTokenSource();
            var cancellationToken = controller.InitCts.Token;
            controller.InitializeLocalLobbyAndSubscribe(deps, cancellationToken).SafeFireAndForget(
                ex => deps.Logger.LogError(ex, "Error initializing local lobby"));
        }

        /// <summary>
        /// Starts the hosted game and navigates to it, reporting a room-lock failure as a hosting error.
        /// </summary>
        public async Task StartGameAsync(
            INavigationService navigationService,
            SetupDependencies deps,
            MapConfigViewModel mapConfig)
        {
            var outcome = await GetOrCreateBroadcaster(controller, deps)
                .StartGameAsync(navigationService, controller.IsOnlineMode, deps.Session.LocalGame);
            if (outcome.Started || outcome.Error == null) return;
            controller.HostingError = outcome.Error;
        }

        /// <summary>
        /// The localized status text for the current hosting state.
        /// </summary>
        public string GetHostingStatusText(SetupDependencies deps)
        {
            if (controller.IsHostingActive)
                return deps.LocalizationService.GetString("Hosting_Starting");
            if (controller.HostingError != null)
                return controller.HostingError;
            if (controller.IsMultiplayerEnabled && controller.RoomCode != null)
                return string.Format(deps.LocalizationService.GetString("Hosting_RoomReady"), controller.RoomCode);
            if (controller.IsMultiplayerEnabled && controller.IsLanMode && deps.GameManager.IsLanServerRunning)
                return deps.LocalizationService.GetString("Hosting_LanEnabled");
            if (controller.IsMultiplayerEnabled)
                return deps.LocalizationService.GetString("Hosting_Starting");
            return string.Empty;
        }

        /// <summary>
        /// The localized status text for the last room-code copy attempt.
        /// </summary>
        public string GetCopyRoomCodeStatusText(SetupDependencies deps) =>
            controller.RoomCodeCopySucceeded switch
            {
                true => deps.LocalizationService.GetString("Network_CopyRoomCode_Success"),
                false => deps.LocalizationService.GetString("Network_CopyRoomCode_Failed"),
                _ => string.Empty
            };

        /// <summary>
        /// The server host shown while LAN hosting is running.
        /// </summary>
        public string GetServerIpAddress(SetupDependencies deps)
        {
            var serverUrl = deps.GameManager.GetLanServerAddress();
            if (string.IsNullOrEmpty(serverUrl))
                return "LAN Disabled..."; // Indicate status
            try
            {
                // Extract host from the URL
                var uri = new Uri(serverUrl);
                return $"{uri.Host}"; // Display only Host name/IP
            }
            catch
            {
                return "Invalid Address";
            }
        }

        private async Task InitializeLocalLobbyAndSubscribe(
            SetupDependencies deps,
            CancellationToken cancellationToken)
        {
            // The local game must only be created once the manager has finished
            // resetting and created the new server game, so it binds to the correct
            // server game id.
            var initialized = await deps.GameManager.InitializeLocalLobby(cancellationToken);
            if (!initialized || cancellationToken.IsCancellationRequested || controller.IsDisposed)
                return;

            controller.SubscribeAndCreateLocalGame(deps);
        }

        private void CancelPendingInitialization()
        {
            controller.InitCts?.Cancel();
            controller.InitCts?.Dispose();
            controller.InitCts = null;
        }

        private void SubscribeToOnlineStatus(SetupDependencies deps) =>
            controller.ConnectionStatus.Subscribe(
                deps.GameManager.OnlineConnectionStatus,
                deps.DispatcherService.Scheduler);

        internal void UnsubscribeFromOnlineStatus(SetupDependencies deps) =>
            controller.ConnectionStatus.Subscribe(null, deps.DispatcherService.Scheduler);

        private void SubscribeAndCreateLocalGame(SetupDependencies deps) =>
            new LocalGameConnector(deps.CommandPublisher, deps.GameManager, deps.Session)
                .SubscribeAndCreateLocalGame(controller.CommandStreamHandler!);

        private static HostingMapBroadcaster GetOrCreateBroadcaster(
            HostingSetupController target,
            SetupDependencies deps) =>
            target.Broadcaster ??= new HostingMapBroadcaster(
                deps.GameManager,
                target.MapConfig,
                deps.LocalizationService);
    }
}




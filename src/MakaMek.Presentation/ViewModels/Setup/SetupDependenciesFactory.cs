using Microsoft.Extensions.Logging;
using Sanet.Localization;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Services;
using Sanet.Transport.SignalR.Client.Relay;

namespace Sanet.MakaMek.Presentation.ViewModels.Setup;

/// <summary>
/// Builds the coordinator dependency bundle for a setup screen from the composition root. The bundle is
/// fully populated for both screens, so neither screen has to pass placeholders for the collaborators
/// only the other screen uses. The setup session and the screen logger are supplied per screen because
/// each screen owns its own lobby state and log category.
/// </summary>
public sealed class SetupDependenciesFactory(
    IGameManager gameManager,
    ICommandPublisher commandPublisher,
    IDispatcherService dispatcherService,
    IRelayHubConfigurationProvider hubConfigurationProvider,
    IRelayRoomClient relayRoomClient,
    IClipboardService clipboardService,
    ILocalizationService localizationService,
    IMapPreviewRenderer mapPreviewRenderer,
    IBattleMapFactory mapFactory,
    IGameConnector gameConnector,
    HostingHubProbe hostingHubProbe,
    RoomCodeClipboardReader clipboardReader,
    LobbyMapPreview mapPreview)
{
    /// <summary>
    /// Creates the bundle for one setup screen.
    /// </summary>
    /// <param name="session">The screen's lobby session.</param>
    /// <param name="logger">The screen's logger, used for the lifecycle failures it reports.</param>
    public SetupDependencies Create(GameSetupSession session, ILogger logger) => new(
        gameManager,
        commandPublisher,
        dispatcherService,
        hubConfigurationProvider,
        relayRoomClient,
        clipboardService,
        localizationService,
        logger,
        mapPreviewRenderer,
        mapFactory,
        gameConnector,
        session,
        hostingHubProbe,
        clipboardReader,
        mapPreview);
}

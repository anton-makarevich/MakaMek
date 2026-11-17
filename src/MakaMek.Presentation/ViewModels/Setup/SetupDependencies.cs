using Microsoft.Extensions.Logging;
using Sanet.Localization;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Services;
using Sanet.MakaMek.Services;
using Sanet.Transport.SignalR.Client.Relay;

namespace Sanet.MakaMek.Presentation.ViewModels.Setup;

/// <summary>
/// The collaborators both setup coordinators need, grouped so a coordinator does not have to take the
/// whole catalogue of services as separate constructor parameters.
/// </summary>
public sealed record SetupDependencies(
    IGameManager GameManager,
    ICommandPublisher CommandPublisher,
    IDispatcherService DispatcherService,
    IRelayHubConfigurationProvider HubConfigurationProvider,
    IRelayRoomClient RelayRoomClient,
    IClipboardService ClipboardService,
    ILocalizationService LocalizationService,
    ILogger Logger,
    IMapPreviewRenderer MapPreviewRenderer,
    IBattleMapFactory MapFactory,
    IGameConnector GameConnector,
    GameSetupSession Session);

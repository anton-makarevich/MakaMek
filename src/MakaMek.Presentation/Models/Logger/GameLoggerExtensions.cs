using Microsoft.Extensions.Logging;

namespace Sanet.MakaMek.Presentation.Models.Logger;

public static partial class GameLoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "Attempted to connect to {serverIp}", SkipEnabledCheck = true)]
    public static partial void LogAttemptedToConnectToServerIp(this ILogger logger, string serverIp);

    [LoggerMessage(LogLevel.Error, "Error preloading content", SkipEnabledCheck = true)]
    public static partial void LogErrorPreloadingContent(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error showing transport closed dialog", SkipEnabledCheck = true)]
    public static partial void LogErrorShowingTransportClosedDialog(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error processing game ended command", SkipEnabledCheck = true)]
    public static partial void LogErrorProcessingGameEndedCommand(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "PDF map export failed", SkipEnabledCheck = true)]
    public static partial void LogPdfMapExportFailed(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error navigating to end game", SkipEnabledCheck = true)]
    public static partial void LogErrorNavigatingToEndGame(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "ConnectionStatus stream error", SkipEnabledCheck = true)]
    public static partial void LogConnectionStatusStreamError(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error updating map", SkipEnabledCheck = true)]
    public static partial void LogErrorUpdatingMap(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error generating initial map", SkipEnabledCheck = true)]
    public static partial void LogErrorGeneratingInitialMap(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error loading available maps", SkipEnabledCheck = true)]
    public static partial void LogErrorLoadingAvailableMaps(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error loading map from file", SkipEnabledCheck = true)]
    public static partial void LogErrorLoadingMapFromFile(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "{Message}", SkipEnabledCheck = true)]
    public static partial void LogErrorInBackgroundOperation(this ILogger logger, string message, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error loading available units", SkipEnabledCheck = true)]
    public static partial void LogErrorLoadingAvailableUnits(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error adding default player", SkipEnabledCheck = true)]
    public static partial void LogErrorAddingDefaultPlayer(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error generating preview for map '{MapName}'", SkipEnabledCheck = true)]
    public static partial void LogErrorGeneratingPreviewForMap(this ILogger logger, string mapName, Exception exception);

    [LoggerMessage(LogLevel.Error, "Failed to export map", SkipEnabledCheck = true)]
    public static partial void LogFailedToExportMap(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Failed to clear cache", SkipEnabledCheck = true)]
    public static partial void LogFailedToClearCache(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Failed to reload asset providers", SkipEnabledCheck = true)]
    public static partial void LogFailedToReloadAssetProviders(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Warning, "state.ResumeMovementAfterFall: unit {UnitId} not found among alive units", SkipEnabledCheck = true)]
    public static partial void LogResumeMovementAfterFallUnitNotFound(this ILogger logger, Guid unitId);

    [LoggerMessage(LogLevel.Warning, "Resume movement after fall ignored: unit {UnitId} is not controlled by the active player", SkipEnabledCheck = true)]
    public static partial void LogResumeMovementAfterFallUnitNotControlled(this ILogger logger, Guid unitId);

    [LoggerMessage(LogLevel.Warning, "Resume movement after fall ignored: command unit {CommandUnit} does not match movement state's selected unit {StateUnit}", SkipEnabledCheck = true)]
    public static partial void LogResumeMovementAfterFallUnitMismatch(this ILogger logger, Guid commandUnit, Guid stateUnit);

    [LoggerMessage(LogLevel.Warning, "Resume movement after fall: unit {UnitId} is {Reason}, ending movement", SkipEnabledCheck = true)]
    public static partial void LogResumeMovementAfterFallEndingMovement(this ILogger logger, Guid unitId, string reason);

    [LoggerMessage(LogLevel.Warning, "Resume movement after fall: no movement path in state for unit {UnitId}, rebuilding at current position", SkipEnabledCheck = true)]
    public static partial void LogResumeMovementAfterFallNoPath(this ILogger logger, Guid unitId);

    [LoggerMessage(LogLevel.Warning, "state.ResumeMovementAfterStandup: unit {UnitId} not found among alive units", SkipEnabledCheck = true)]
    public static partial void LogResumeMovementAfterStandupUnitNotFound(this ILogger logger, Guid unitId);

    [LoggerMessage(LogLevel.Warning, "Resume movement after fall ignored: unit {UnitId} is not a mech", SkipEnabledCheck = true)]
    public static partial void LogResumeMovementAfterFallNotMech(this ILogger logger, Guid unitId);

    [LoggerMessage(LogLevel.Error, "Error loading default player from cache", SkipEnabledCheck = true)]
    public static partial void LogErrorLoadingDefaultPlayerFromCache(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error saving default player to cache", SkipEnabledCheck = true)]
    public static partial void LogErrorSavingDefaultPlayerToCache(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Failed to load asset providers", SkipEnabledCheck = true)]
    public static partial void LogFailedToLoadAssetProviders(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Cannot toggle provider {ProviderId}", SkipEnabledCheck = true)]
    public static partial void LogCannotToggleProvider(this ILogger logger, string providerId, Exception exception);

    [LoggerMessage(LogLevel.Error, "Failed to toggle provider {ProviderId}", SkipEnabledCheck = true)]
    public static partial void LogFailedToToggleProvider(this ILogger logger, string providerId, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Cannot remove provider {ProviderId}", SkipEnabledCheck = true)]
    public static partial void LogCannotRemoveProvider(this ILogger logger, string providerId, Exception exception);

    [LoggerMessage(LogLevel.Error, "Failed to remove provider {ProviderId}", SkipEnabledCheck = true)]
    public static partial void LogFailedToRemoveProvider(this ILogger logger, string providerId, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Cannot update provider {ProviderId}", SkipEnabledCheck = true)]
    public static partial void LogCannotUpdateProvider(this ILogger logger, string providerId, Exception exception);

    [LoggerMessage(LogLevel.Error, "Failed to update provider {ProviderId}", SkipEnabledCheck = true)]
    public static partial void LogFailedToUpdateProvider(this ILogger logger, string providerId, Exception exception);

    [LoggerMessage(LogLevel.Error, "Failed to add asset provider", SkipEnabledCheck = true)]
    public static partial void LogFailedToAddAssetProvider(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error refreshing hub status", SkipEnabledCheck = true)]
    public static partial void LogErrorRefreshingHubStatus(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Failed to add hub {HubName}", SkipEnabledCheck = true)]
    public static partial void LogFailedToAddHub(this ILogger logger, string hubName, Exception exception);

    [LoggerMessage(LogLevel.Error, "Hub status probe failed for hub {HubName}", SkipEnabledCheck = true)]
    public static partial void LogHubStatusProbeFailed(this ILogger logger, string hubName, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error stopping hosting on detach", SkipEnabledCheck = true)]
    public static partial void LogErrorStoppingHostingOnDetach(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error probing active hub", SkipEnabledCheck = true)]
    public static partial void LogErrorProbingActiveHub(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Debug, "Lobby initialization cancelled by superseded restart or detach/dispose", SkipEnabledCheck = true)]
    public static partial void LogLobbyInitializationCancelled(this ILogger logger);

    [LoggerMessage(LogLevel.Error, "Error initializing lobby during restart", SkipEnabledCheck = true)]
    public static partial void LogErrorInitializingLobbyDuringRestart(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error initializing local lobby", SkipEnabledCheck = true)]
    public static partial void LogErrorInitializingLocalLobby(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error connecting to server: {Message}", SkipEnabledCheck = true)]
    public static partial void LogErrorConnectingToServer(this ILogger logger, string message, Exception exception);

    [LoggerMessage(LogLevel.Information, "Online game join was cancelled", SkipEnabledCheck = true)]
    public static partial void LogOnlineGameJoinCancelled(this ILogger logger);

    [LoggerMessage(LogLevel.Error, "Error joining online game", SkipEnabledCheck = true)]
    public static partial void LogErrorJoiningOnlineGame(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error retrying connection", SkipEnabledCheck = true)]
    public static partial void LogErrorRetryingConnection(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Debug, "Map preview generation cancelled - a newer map superseded this preview", SkipEnabledCheck = true)]
    public static partial void LogMapPreviewGenerationCancelled(this ILogger logger);
}

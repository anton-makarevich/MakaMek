using Microsoft.Extensions.Logging;

namespace Sanet.MakaMek.Core.Services.Logger;

public static partial class ServicesLoggerExtensions
{
    [LoggerMessage(LogLevel.Error, "Error reading cached file '{CacheKey}'", SkipEnabledCheck = true)]
    public static partial void LogErrorReadingCachedFile(this ILogger logger, string cacheKey, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error saving file to cache '{CacheKey}'", SkipEnabledCheck = true)]
    public static partial void LogErrorSavingFileToCache(this ILogger logger, string cacheKey, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error deleting cached file '{File}'", SkipEnabledCheck = true)]
    public static partial void LogErrorDeletingCachedFile(this ILogger logger, string file, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error clearing cache", SkipEnabledCheck = true)]
    public static partial void LogErrorClearingCache(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error removing file from cache '{CacheKey}'", SkipEnabledCheck = true)]
    public static partial void LogErrorRemovingFileFromCache(this ILogger logger, string cacheKey, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error reading version for cached file '{CacheKey}'", SkipEnabledCheck = true)]
    public static partial void LogErrorReadingVersionForCachedFile(this ILogger logger, string cacheKey, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error in command subscriber", SkipEnabledCheck = true)]
    public static partial void LogErrorInCommandSubscriber(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error disposing publisher", SkipEnabledCheck = true)]
    public static partial void LogErrorDisposingPublisher(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Debug, "Publishing command {MessageType} (origin {GameOriginId}) to {PublisherCount} publisher(s)", SkipEnabledCheck = true)]
    public static partial void LogPublishingCommandToPublishers(this ILogger logger, string messageType, Guid gameOriginId, int publisherCount);

    [LoggerMessage(LogLevel.Error, "Error publishing to {PublisherType}", SkipEnabledCheck = true)]
    public static partial void LogErrorPublishingToPublisher(this ILogger logger, string publisherType, Exception exception);

    [LoggerMessage(LogLevel.Debug, "Publishing command {MessageType} (origin {GameOriginId}) to {PublisherType}", SkipEnabledCheck = true)]
    public static partial void LogPublishingCommandToPublisher(this ILogger logger, string messageType, Guid gameOriginId, string publisherType);

    [LoggerMessage(LogLevel.Debug, "No command receive callback registered; local command {MessageType} not dispatched", SkipEnabledCheck = true)]
    public static partial void LogNoCommandReceiveCallbackRegistered(this ILogger logger, string messageType);

    [LoggerMessage(LogLevel.Error, "Error dispatching local command {MessageType}", SkipEnabledCheck = true)]
    public static partial void LogErrorDispatchingLocalCommand(this ILogger logger, string messageType, Exception exception);

    [LoggerMessage(LogLevel.Debug, "Received transport message {MessageType} from publisher {PublisherType}", SkipEnabledCheck = true)]
    public static partial void LogReceivedTransportMessage(this ILogger logger, string messageType, string publisherType);

    [LoggerMessage(LogLevel.Error, "Unknown command type: {CommandType}", SkipEnabledCheck = true)]
    public static partial void LogUnknownCommandType(this ILogger logger, string commandType, Exception exception);

    [LoggerMessage(LogLevel.Error, "JSON error deserializing command: {CommandType}", SkipEnabledCheck = true)]
    public static partial void LogJsonErrorDeserializingCommand(this ILogger logger, string commandType, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error processing command: {CommandType}", SkipEnabledCheck = true)]
    public static partial void LogErrorProcessingCommand(this ILogger logger, string commandType, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Failed to remove {PublisherType} during cleanup", SkipEnabledCheck = true)]
    public static partial void LogFailedToRemovePublisherDuringCleanup(this ILogger logger, string publisherType, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Failed to dispose {PublisherType} during cleanup", SkipEnabledCheck = true)]
    public static partial void LogFailedToDisposePublisherDuringCleanup(this ILogger logger, string publisherType, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Failed to load relay hub configurations from cache", SkipEnabledCheck = true)]
    public static partial void LogFailedToLoadRelayHubConfigurationsFromCache(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Failed to save relay hub configurations to cache", SkipEnabledCheck = true)]
    public static partial void LogFailedToSaveRelayHubConfigurationsToCache(this ILogger logger, Exception exception);
}

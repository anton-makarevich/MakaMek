using System.Net;
using Microsoft.Extensions.Logging;

namespace Sanet.MakaMek.Assets.Models.Logger;

public static partial class GameLoggerExtensions
{
    [LoggerMessage(LogLevel.Warning, "Failed to load asset provider configurations from cache", SkipEnabledCheck = true)]
    public static partial void LogFailedToLoadAssetProviderConfigurationsFromCache(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Failed to save asset provider configurations to cache", SkipEnabledCheck = true)]
    public static partial void LogFailedToSaveAssetProviderConfigurationsToCache(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error enumerating {ResourceKind} resources from provider {ProviderType}", SkipEnabledCheck = true)]
    public static partial void LogErrorEnumeratingResources(this ILogger logger, string resourceKind, string providerType, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error loading {ResourceKind} '{ResourceId}' from provider {ProviderType}", SkipEnabledCheck = true)]
    public static partial void LogErrorLoadingResource(this ILogger logger, string resourceKind, string resourceId, string providerType, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Duplicate {ResourceKind} '{Key}' found; overwriting with the value from a lower provider", SkipEnabledCheck = true)]
    public static partial void LogDuplicateResourceFound(this ILogger logger, string resourceKind, object key);

    [LoggerMessage(LogLevel.Warning, "Unit '{Model}' has unusable sprite data: {SpriteError}", SkipEnabledCheck = true)]
    public static partial void LogUnitHasUnusableSpriteData(this ILogger logger, string model, string? spriteError);

    [LoggerMessage(LogLevel.Error, "Error loading MMTX package", SkipEnabledCheck = true)]
    public static partial void LogErrorLoadingMmtxPackage(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Information, "Loaded terrain biome '{BiomeId}' version {Version}", SkipEnabledCheck = true)]
    public static partial void LogLoadedTerrainBiome(this ILogger logger, string biomeId, string version);

    [LoggerMessage(LogLevel.Warning, "Duplicate terrain biome '{BiomeId}' found; replacing previously loaded assets", SkipEnabledCheck = true)]
    public static partial void LogDuplicateTerrainBiome(this ILogger logger, string biomeId);

    [LoggerMessage(LogLevel.Information, "Cache version mismatch for {ResourceId}: cached {CachedVersion} vs current {CurrentVersion}.", SkipEnabledCheck = true)]
    public static partial void LogCacheVersionMismatch(this ILogger logger, string resourceId, string cachedVersion, string currentVersion);

    [LoggerMessage(LogLevel.Warning, "Failed to download file from {ResourceId}: {StatusCode}", SkipEnabledCheck = true)]
    public static partial void LogFailedToDownloadFile(this ILogger logger, string resourceId, HttpStatusCode statusCode);

    [LoggerMessage(LogLevel.Information, "Serving stale cached content for {ResourceId} due to download failure", SkipEnabledCheck = true)]
    public static partial void LogServingStaleCachedContent(this ILogger logger, string resourceId);

    [LoggerMessage(LogLevel.Error, "Error caching file from {ResourceId}", SkipEnabledCheck = true)]
    public static partial void LogErrorCachingFile(this ILogger logger, string resourceId, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error downloading file from {ResourceId}", SkipEnabledCheck = true)]
    public static partial void LogErrorDownloadingFile(this ILogger logger, string resourceId, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Failed to fetch {ListingDescription} from {ListingUrl}: {StatusCode}", SkipEnabledCheck = true)]
    public static partial void LogFailedToFetchListing(this ILogger logger, string listingDescription, string listingUrl, HttpStatusCode statusCode);

    [LoggerMessage(LogLevel.Warning, "Failed to deserialize {ListingDescription} response", SkipEnabledCheck = true)]
    public static partial void LogFailedToDeserializeListing(this ILogger logger, string listingDescription);

    [LoggerMessage(LogLevel.Error, "Error caching {CachedListingDescription} for {ListingUrl}", SkipEnabledCheck = true)]
    public static partial void LogErrorCachingListing(this ILogger logger, string cachedListingDescription, string listingUrl, Exception exception);

    [LoggerMessage(LogLevel.Information, "Found {Count} resources in {ListingDescription} at {ListingUrl}", SkipEnabledCheck = true)]
    public static partial void LogFoundResources(this ILogger logger, int count, string listingDescription, string listingUrl);

    [LoggerMessage(LogLevel.Error, "Error loading {ListingDescription}", SkipEnabledCheck = true)]
    public static partial void LogErrorLoadingListing(this ILogger logger, string listingDescription, Exception exception);

    [LoggerMessage(LogLevel.Information, "Using cached {CachedListingDescription} for offline resource discovery", SkipEnabledCheck = true)]
    public static partial void LogUsingCachedListing(this ILogger logger, string cachedListingDescription);

    [LoggerMessage(LogLevel.Error, "Error loading cached {CachedListingDescription}", SkipEnabledCheck = true)]
    public static partial void LogErrorLoadingCachedListing(this ILogger logger, string cachedListingDescription, Exception exception);
}

namespace Sanet.MakaMek.Core.Services;

/// <summary>
/// Interface for caching files from remote sources to improve performance and reduce network requests
/// </summary>
public interface IFileCachingService
{
    /// <summary>
    /// Checks if a cached file exists and returns its content if available
    /// </summary>
    /// <param name="cacheKey">Unique identifier for the cached file</param>
    /// <returns>Cached file content as stream if found, null otherwise</returns>
    Task<Stream?> TryGetCachedFile(string cacheKey);

    /// <summary>
    /// Saves a file to the cache with the specified key
    /// </summary>
    /// <param name="cacheKey">Unique identifier for the cached file</param>
    /// <param name="content">File content to cache</param>
    /// <returns>Task representing the async operation</returns>
    Task SaveToCache(string cacheKey, Stream content);

    /// <summary>
    /// Clears all cached files
    /// </summary>
    /// <returns>Task representing the async operation</returns>
    Task ClearCache();

    /// <summary>
    /// Checks if a cached file exists without loading its content
    /// </summary>
    /// <param name="cacheKey">Unique identifier for the cached file</param>
    /// <returns>True if the file exists in cache, false otherwise</returns>
    Task<bool> IsCached(string cacheKey);

    /// <summary>
    /// Removes a specific file from the cache
    /// </summary>
    /// <param name="cacheKey">Unique identifier for the cached file to remove</param>
    /// <returns>Task representing the async operation</returns>
    Task RemoveFromCache(string cacheKey);
}

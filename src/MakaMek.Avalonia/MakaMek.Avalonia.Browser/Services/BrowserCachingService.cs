using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Sanet.MakaMek.Core.Services;

namespace Sanet.MakaMek.Avalonia.Browser.Services;

/// <summary>
/// Browser-based caching service using IndexedDB for persistent storage in WASM applications
/// This implementation uses JavaScript interop to store cached files in IndexedDB,
/// ensuring data persists across browser sessions.
/// </summary>
[SupportedOSPlatform("browser")]
public partial class BrowserCachingService : IFileCachingService
{
    /// <summary>
    /// Generates a safe cache key using SHA256 hash (same as FileSystemCachingService)
    /// </summary>
    /// <param name="originalKey">The original cache key (usually a URL)</param>
    /// <returns>SHA256 hash of the original key</returns>
    private static string GetHashedCacheKey(string originalKey)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(originalKey));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static bool _isInitialized;
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static async Task EnsureInitialized()
    {
        if (_isInitialized) return;

        await InitLock.WaitAsync();
        try
        {
            if (!_isInitialized)
            {
                await JSHost.ImportAsync("cacheStorage", "../cacheStorage.js");
                _isInitialized = true;
            }
        }
        finally
        {
            InitLock.Release();
        }
    }

    /// <summary>
    /// Checks if a cached file exists and returns its content if available
    /// </summary>
    /// <param name="cacheKey">Unique identifier for the cached file</param>
    /// <returns>Cached file content as a byte array if found, null otherwise</returns>
    public async Task<byte[]?> TryGetCachedFile(string cacheKey)
    {
        if (string.IsNullOrEmpty(cacheKey))
            return null;

        try
        {
            await EnsureInitialized();
            var jsObject = await GetFromCacheAsObjectJs(GetHashedCacheKey(cacheKey));
            var result = UnwrapByteArrayJs(jsObject);
            // JS returns empty array when not found, treat as null
            return result.Length == 0 ? null : result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading cached file '{cacheKey}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Saves a file to the cache with the specified key
    /// </summary>
    /// <param name="cacheKey">Unique identifier for the cached file</param>
    /// <param name="content">File content to cache as a byte array</param>
    /// <param name="version">Optional version metadata to store alongside the cached content</param>
    /// <returns>Task representing the async operation</returns>
    public async Task SaveToCache(string cacheKey, byte[] content, string? version = null)
    {
        if (string.IsNullOrEmpty(cacheKey) || content.Length == 0)
            return;

        try
        {
            await EnsureInitialized();
            var hashedKey = GetHashedCacheKey(cacheKey);

            if (version != null)
            {
                // Save content and version atomically in a single transaction
                await SaveToCacheWithVersionJs(hashedKey, content, version);
            }
            else
            {
                // Save content only when no version is provided
                await SaveToCacheJs(hashedKey, content);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving file to cache '{cacheKey}': {ex.Message}");
        }
    }

    /// <summary>
    /// Clears all cached files
    /// </summary>
    /// <returns>Task representing the async operation</returns>
    public async Task ClearCache()
    {
        try
        {
            await EnsureInitialized();
            await ClearCacheJs();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if a cached file exists without loading its content
    /// </summary>
    /// <param name="cacheKey">Unique identifier for the cached file</param>
    /// <returns>True if the file exists in cache, false otherwise</returns>
    public async Task<bool> IsCached(string cacheKey)
    {
        if (string.IsNullOrEmpty(cacheKey))
            return false;

        try
        {
            await EnsureInitialized();
            return await IsCachedJs(GetHashedCacheKey(cacheKey));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking cache for '{cacheKey}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Removes a specific file from the cache
    /// </summary>
    /// <param name="cacheKey">Unique identifier for the cached file to remove</param>
    /// <returns>Task representing the async operation</returns>
    public async Task RemoveFromCache(string cacheKey)
    {
        if (string.IsNullOrEmpty(cacheKey))
            return;

        try
        {
            await EnsureInitialized();
            var hashedKey = GetHashedCacheKey(cacheKey);
            await RemoveFromCacheJs(hashedKey);
            await RemoveFromCacheJs(hashedKey + ":version");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing file from cache '{cacheKey}': {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the version metadata for a cached file
    /// </summary>
    /// <param name="cacheKey">Unique identifier for the cached file</param>
    /// <returns>Version string if found, null otherwise</returns>
    public async Task<string?> GetCacheVersion(string cacheKey)
    {
        if (string.IsNullOrEmpty(cacheKey))
            return null;

        try
        {
            await EnsureInitialized();
            var versionKey = GetHashedCacheKey(cacheKey) + ":version";
            var jsObject = await GetVersionFromCacheAsObjectJs(versionKey);
            var result = UnwrapStringJs(jsObject);
            return string.IsNullOrEmpty(result) ? null : result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading version for cached file '{cacheKey}': {ex.Message}");
            return null;
        }
    }
}

// JavaScript interop methods using JSObject workaround for byte arrays
public partial class BrowserCachingService
{
    // Get byte array as JSObject reference (workaround for Task<byte[]> limitation)
    [JSImport("getFromCache", "cacheStorage")]
    [return: JSMarshalAs<JSType.Promise<JSType.Object>>()]
    private static partial Task<JSObject> GetFromCacheAsObjectJs(string cacheKey);

    // Unwrap JSObject back to byte array
    [JSImport("unwrapByteArray", "cacheStorage")]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>()]
    private static partial byte[] UnwrapByteArrayJs(JSObject byteArrayObject);

    // Save byte array to cache
    [JSImport("saveToCache", "cacheStorage")]
    private static partial Task SaveToCacheJs(string cacheKey, byte[] data);

    // Save byte array and version to cache in a single transaction
    [JSImport("saveToCacheWithVersion", "cacheStorage")]
    private static partial Task SaveToCacheWithVersionJs(string cacheKey, byte[] data, string version);

    // Check if cached
    [JSImport("isCached", "cacheStorage")]
    private static partial Task<bool> IsCachedJs(string cacheKey);

    // Remove from cache
    [JSImport("removeFromCache", "cacheStorage")]
    private static partial Task RemoveFromCacheJs(string cacheKey);

    // Clear all cache
    [JSImport("clearCache", "cacheStorage")]
    private static partial Task ClearCacheJs();

    // Get version as JSObject reference (workaround for Task<string> limitation)
    [JSImport("getVersionFromCacheAsObject", "cacheStorage")]
    [return: JSMarshalAs<JSType.Promise<JSType.Object>>()]
    private static partial Task<JSObject> GetVersionFromCacheAsObjectJs(string cacheKey);

    // Unwrap JSObject back to string
    [JSImport("unwrapString", "cacheStorage")]
    [return: JSMarshalAs<JSType.String>]
    private static partial string UnwrapStringJs(JSObject stringObject);
}

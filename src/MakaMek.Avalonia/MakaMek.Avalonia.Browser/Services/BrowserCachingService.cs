using System.Collections.Concurrent;
using Sanet.MakaMek.Core.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sanet.MakaMek.Avalonia.Browser.Services;

/// <summary>
/// Browser-based caching service using in-memory storage for WASM applications
/// Note: This implementation uses in-memory storage as a fallback since browser APIs
/// may not be directly accessible. In a production environment, this could be enhanced
/// with proper browser storage APIs through JavaScript interop.
/// </summary>
public class BrowserCachingService : IFileCachingService
{
    private readonly ConcurrentDictionary<string, byte[]> _cache = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Checks if a cached file exists and returns its content if available
    /// </summary>
    /// <param name="cacheKey">Unique identifier for the cached file</param>
    /// <returns>Cached file content as stream if found, null otherwise</returns>
    public async Task<Stream?> TryGetCachedFile(string cacheKey)
    {
        if (string.IsNullOrEmpty(cacheKey))
            return null;

        await _semaphore.WaitAsync();
        try
        {
            if (_cache.TryGetValue(cacheKey, out var cachedData))
            {
                return new MemoryStream(cachedData);
            }
            return null;
        }
        catch (Exception ex)
        {
            // Log error but return null to gracefully handle cache misses
            Console.WriteLine($"Error reading cached file '{cacheKey}': {ex.Message}");
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Saves a file to the cache with the specified key
    /// </summary>
    /// <param name="cacheKey">Unique identifier for the cached file</param>
    /// <param name="content">File content to cache</param>
    /// <returns>Task representing the async operation</returns>
    public async Task SaveToCache(string cacheKey, Stream content)
    {
        if (string.IsNullOrEmpty(cacheKey) || content == null)
            return;

        await _semaphore.WaitAsync();
        try
        {
            // Read stream content
            content.Position = 0;
            using var memoryStream = new MemoryStream();
            await content.CopyToAsync(memoryStream);
            var bytes = memoryStream.ToArray();

            // Store in memory cache
            _cache.AddOrUpdate(cacheKey, bytes, (key, oldValue) => bytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving file to cache '{cacheKey}': {ex.Message}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Clears all cached files
    /// </summary>
    /// <returns>Task representing the async operation</returns>
    public async Task ClearCache()
    {
        await _semaphore.WaitAsync();
        try
        {
            _cache.Clear();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing cache: {ex.Message}");
        }
        finally
        {
            _semaphore.Release();
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

        await _semaphore.WaitAsync();
        try
        {
            return _cache.ContainsKey(cacheKey);
        }
        finally
        {
            _semaphore.Release();
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

        await _semaphore.WaitAsync();
        try
        {
            _cache.TryRemove(cacheKey, out _);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing file from cache '{cacheKey}': {ex.Message}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Disposes the semaphore
    /// </summary>
    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}

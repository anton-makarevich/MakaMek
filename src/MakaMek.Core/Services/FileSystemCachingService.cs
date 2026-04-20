using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1873

namespace Sanet.MakaMek.Core.Services;

/// <summary>
/// File-system-based caching service for desktop and mobile platforms
/// </summary>
public class FileSystemCachingService : IFileCachingService
{
    private readonly string _cacheDirectory;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger<FileSystemCachingService> _logger;

    /// <summary>
    /// Initializes a new instance of FileSystemCachingService
    /// </summary>
    public FileSystemCachingService(ILoggerFactory loggerFactory)
    {
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "MakaMek", 
            "Cache");
        
        _logger = loggerFactory.CreateLogger<FileSystemCachingService>();
        
        EnsureCacheDirectoryExists();
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

        var filePath = GetCacheFilePath(cacheKey);
        
        try
        {
            if (!File.Exists(filePath))
                return null;

            // Read file content as a byte array
            return await File.ReadAllBytesAsync(filePath);
        }
        catch (Exception ex)
        {
            // Log error but return null to gracefully handle cache misses
            _logger.LogError(ex, "Error reading cached file '{CacheKey}'", cacheKey);
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
        if (string.IsNullOrEmpty(cacheKey))
            return;

        var filePath = GetCacheFilePath(cacheKey);
        
        await _semaphore.WaitAsync();
        try
        {
            EnsureCacheDirectoryExists();
            
            // Create a temporary file first, then move it to avoid corruption
            var tempFilePath = filePath + ".tmp";
            
            await using (var fileStream = File.Create(tempFilePath))
            {
                await fileStream.WriteAsync(content);
                await fileStream.FlushAsync();
            }
            
            // Atomically move the temp file to the final location
            File.Move(tempFilePath, filePath, overwrite: true);

            var versionFilePath = GetVersionFilePath(cacheKey);
            
            // Keep version metadata in sync with the cached content
            if (version != null)
            {
                
                var tempVersionFilePath = versionFilePath + ".tmp";
                await File.WriteAllTextAsync(tempVersionFilePath, version);
                File.Move(tempVersionFilePath, versionFilePath, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't throw to avoid impacting normal operations
            _logger.LogError(ex, "Error saving file to cache '{CacheKey}'", cacheKey);
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
            if (!Directory.Exists(_cacheDirectory))
                return;

            var files = Directory.GetFiles(_cacheDirectory, "*", SearchOption.AllDirectories);
            
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    // Log error but continue with other files
                    _logger.LogError(ex, "Error deleting cached file '{File}'", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
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
    public Task<bool> IsCached(string cacheKey)
    {
        if (string.IsNullOrEmpty(cacheKey))
            return Task.FromResult(false);

        var filePath = GetCacheFilePath(cacheKey);
        return Task.FromResult(File.Exists(filePath));
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

        var filePath = GetCacheFilePath(cacheKey);
        var versionFilePath = GetVersionFilePath(cacheKey);
        
        await _semaphore.WaitAsync();
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            if (File.Exists(versionFilePath))
            {
                File.Delete(versionFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing file from cache '{CacheKey}'", cacheKey);
        }
        finally
        {
            _semaphore.Release();
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

        var versionFilePath = GetVersionFilePath(cacheKey);
        
        try
        {
            if (!File.Exists(versionFilePath))
                return null;

            var version = await File.ReadAllTextAsync(versionFilePath);
            return string.IsNullOrEmpty(version) ? null : version;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading version for cached file '{CacheKey}'", cacheKey);
            return null;
        }
    }

    /// <summary>
    /// Generates a safe file path for the given cache key
    /// </summary>
    /// <param name="cacheKey">The cache key to convert to a file path</param>
    /// <returns>Safe file path for the cache key</returns>
    private string GetCacheFilePath(string cacheKey)
    {
        // Create a hash of the cache key to ensure safe file names
        var hashString = GetHashedKey(cacheKey);

        return Path.Combine(_cacheDirectory, $"{hashString}.cache");
    }

    /// <summary>
    /// Generates a safe file path for the version file of the given cache key
    /// </summary>
    /// <param name="cacheKey">The cache key to convert to a file path</param>
    /// <returns>Safe file path for the version file</returns>
    private string GetVersionFilePath(string cacheKey)
    {
        var hashString = GetHashedKey(cacheKey);
        return Path.Combine(_cacheDirectory, $"{hashString}.version");
    }

    /// <summary>
    /// Generates a hashed key from the cache key
    /// </summary>
    private static string GetHashedKey(string cacheKey)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Ensures the cache directory exists
    /// </summary>
    private void EnsureCacheDirectoryExists()
    {
        if (!Directory.Exists(_cacheDirectory))
        {
            Directory.CreateDirectory(_cacheDirectory);
        }
    }

    /// <summary>
    /// Disposes the semaphore
    /// </summary>
    public void Dispose()
    {
        _semaphore.Dispose();
    }
}

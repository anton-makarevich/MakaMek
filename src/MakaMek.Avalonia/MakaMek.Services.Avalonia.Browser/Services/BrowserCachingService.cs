using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Sanet.MakaMek.Services.Avalonia.Browser.Services;

[SupportedOSPlatform("browser")]
public partial class BrowserCachingService : IFileCachingService
{
    private readonly ILogger<BrowserCachingService> _logger;

    public BrowserCachingService(ILogger<BrowserCachingService> logger)
    {
        _logger = logger;
    }

    private static string GetHashedCacheKey(string originalKey)
    {
        var hashBytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(originalKey));
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

    public async Task<byte[]?> TryGetCachedFile(string cacheKey)
    {
        if (string.IsNullOrEmpty(cacheKey))
            return null;

        try
        {
            await EnsureInitialized();
            var jsObject = await GetFromCacheAsObjectJs(GetHashedCacheKey(cacheKey));
            var result = UnwrapByteArrayJs(jsObject);
            return result.Length == 0 ? null : result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading cached file '{CacheKey}'", cacheKey);
            return null;
        }
    }

    public async Task SaveToCache(string cacheKey, byte[] content, string? version = null)
    {
        if (string.IsNullOrEmpty(cacheKey) || content.Length == 0)
            return;

        try
        {
            await EnsureInitialized();
            var hashedKey = GetHashedCacheKey(cacheKey);
            await SaveToCacheJs(hashedKey, content, version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file to cache '{CacheKey}'", cacheKey);
        }
    }

    public async Task ClearCache()
    {
        try
        {
            await EnsureInitialized();
            await ClearCacheJs();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
        }
    }

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
            _logger.LogError(ex, "Error checking cache for '{CacheKey}'", cacheKey);
            return false;
        }
    }

    public async Task RemoveFromCache(string cacheKey)
    {
        if (string.IsNullOrEmpty(cacheKey))
            return;

        try
        {
            await EnsureInitialized();
            var hashedKey = GetHashedCacheKey(cacheKey);
            await RemoveFromCacheJs(hashedKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cached file '{CacheKey}'", cacheKey);
        }
    }

    public async Task<string?> GetCacheVersion(string cacheKey)
    {
        if (string.IsNullOrEmpty(cacheKey))
            return null;

        try
        {
            await EnsureInitialized();
            var baseKey = GetHashedCacheKey(cacheKey);
            if (!await IsCachedJs(baseKey))
                return null;

            var versionKey = baseKey + ":version";
            var jsObject = await GetVersionFromCacheAsObjectJs(versionKey);
            var result = UnwrapStringJs(jsObject);
            return string.IsNullOrEmpty(result) ? null : result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading version for cached file '{CacheKey}'", cacheKey);
            return null;
        }
    }
}

public partial class BrowserCachingService
{
    [JSImport("getFromCache", "cacheStorage")]
    [return: JSMarshalAs<JSType.Promise<JSType.Object>>()]
    private static partial Task<JSObject> GetFromCacheAsObjectJs(string cacheKey);

    [JSImport("unwrapByteArray", "cacheStorage")]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>()]
    private static partial byte[] UnwrapByteArrayJs(JSObject byteArrayObject);

    [JSImport("saveToCache", "cacheStorage")]
    private static partial Task SaveToCacheJs(string cacheKey, byte[] data, string? version = null);

    [JSImport("isCached", "cacheStorage")]
    private static partial Task<bool> IsCachedJs(string cacheKey);

    [JSImport("removeFromCache", "cacheStorage")]
    private static partial Task RemoveFromCacheJs(string cacheKey);

    [JSImport("clearCache", "cacheStorage")]
    private static partial Task ClearCacheJs();

    [JSImport("getVersionFromCacheAsObject", "cacheStorage")]
    [return: JSMarshalAs<JSType.Promise<JSType.Object>>()]
    private static partial Task<JSObject> GetVersionFromCacheAsObjectJs(string cacheKey);

    [JSImport("unwrapString", "cacheStorage")]
    [return: JSMarshalAs<JSType.String>]
    private static partial string UnwrapStringJs(JSObject stringObject);
}

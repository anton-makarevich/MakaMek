using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Services.Avalonia.Browser.Services;

[SupportedOSPlatform("browser")]
public partial class BrowserCachingService : IFileCachingService
{
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
            Console.WriteLine($"Error reading cached file '{cacheKey}': {ex.Message}");
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
            Console.WriteLine($"Error saving file to cache '{cacheKey}': {ex.Message}");
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
            Console.WriteLine($"Error clearing cache: {ex.Message}");
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
            Console.WriteLine($"Error checking cache for '{cacheKey}': {ex.Message}");
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
            Console.WriteLine($"Error removing file from cache '{cacheKey}': {ex.Message}");
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
            Console.WriteLine($"Error reading version for cached file '{cacheKey}': {ex.Message}");
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

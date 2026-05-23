namespace Sanet.MakaMek.Services;

public interface IFileCachingService
{
    Task<byte[]?> TryGetCachedFile(string cacheKey);
    Task SaveToCache(string cacheKey, byte[] content, string? version = null);
    Task ClearCache();
    Task<bool> IsCached(string cacheKey);
    Task RemoveFromCache(string cacheKey);
    Task<string?> GetCacheVersion(string cacheKey);
}

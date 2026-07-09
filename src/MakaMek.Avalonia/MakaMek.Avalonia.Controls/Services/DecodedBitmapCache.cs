using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Avalonia.Controls.Services;

/// <summary>
/// Wraps <see cref="ITerrainAssetService"/> with a decoded <see cref="Bitmap"/> cache.
/// On a cache miss during render, returns null and kicks off an async load,
/// guarding against duplicate in-flight loads for the same key.
/// Calls the invalidation action when an async load completes.
/// </summary>
public class DecodedBitmapCache
{
    private readonly ITerrainAssetService _assetService;
    private readonly Action _invalidateAction;
    private readonly ConcurrentDictionary<string, Bitmap?> _cache = new();
    private readonly HashSet<string> _inFlight = [];

    public ITerrainAssetService AssetService => _assetService;

    /// <summary>
    /// Returns true if the given key already has a cached value (including null).
    /// </summary>
    public bool IsCached(string key) => _cache.ContainsKey(key);

    /// <summary>
    /// Sets a cached value directly (used by prefetch).
    /// </summary>
    public void SetCached(string key, Bitmap? bitmap) => _cache[key] = bitmap;

    public DecodedBitmapCache(ITerrainAssetService assetService, Action invalidateAction)
    {
        _assetService = assetService;
        _invalidateAction = invalidateAction;
    }

    /// <summary>
    /// Gets a cached decoded bitmap for the given key, or null if not yet loaded.
    /// On first miss, initiates an async load (guarded against duplicates).
    /// </summary>
    public Bitmap? GetOrSchedule(string key, Func<Task<byte[]?>> fetch)
    {
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        lock (_inFlight)
        {
            if (!_inFlight.Add(key))
                return null;
        }

        _ = LoadAsync(key, fetch);
        return null;
    }

    private async Task LoadAsync(string key, Func<Task<byte[]?>> fetch)
    {
        try
        {
            var bytes = await fetch();
            Bitmap? bitmap = null;
            if (bytes != null)
            {
                using var stream = new MemoryStream(bytes);
                bitmap = new Bitmap(stream);
            }
            _cache[key] = bitmap;
            _invalidateAction();
        }
        catch
        {
            _cache[key] = null;
        }
    }

    /// <summary>
    /// Removes and disposes all cached bitmaps. Call on map unload/switch.
    /// </summary>
    public void Clear()
    {
        foreach (var value in _cache.Values)
            value?.Dispose();
        _cache.Clear();
        lock (_inFlight) { _inFlight.Clear(); }
    }

    /// <summary>
    /// Removes a specific cache key (e.g. when a hex's terrain changes).
    /// </summary>
    public void InvalidateKey(string key)
    {
        if (_cache.TryRemove(key, out var existing))
            existing?.Dispose();
    }

    // Cache key builders
    public static string BaseKey(string biomeId) => $"base:{biomeId}";
    public static string EdgeKey(string biomeId, HexDirection direction, string edgeType, int q, int r) =>
        $"edge:{biomeId}:{(int)direction}:{edgeType}:{q}:{r}";
    public static string WaterKey(string biomeId, byte canonicalMask, int rotationSteps) =>
        $"water:{biomeId}:{canonicalMask}:{rotationSteps}";
    public static string RoadKey(string biomeId, byte canonicalMask, int rotationSteps) =>
        $"road:{biomeId}:{canonicalMask}:{rotationSteps}";
    public static string OverlayKey(string biomeId, string terrainType) =>
        $"overlay:{biomeId}:{terrainType}";
}

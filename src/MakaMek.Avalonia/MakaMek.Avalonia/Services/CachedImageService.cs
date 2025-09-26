using System;
using System.Collections.Concurrent;
using System.IO;
using Avalonia.Media.Imaging;
using Sanet.MakaMek.Core.Services;

namespace Sanet.MakaMek.Avalonia.Services;

/// <summary>
/// Image service that retrieves images from the UnitCachingService instead of Avalonia's asset system
/// This supports loading images from MMUX packages
/// </summary>
public class CachedImageService : IImageService<Bitmap>
{
    private readonly UnitCachingService _unitCachingService;
    private readonly ConcurrentDictionary<string, Bitmap?> _bitmapCache = new();

    public CachedImageService(UnitCachingService unitCachingService)
    {
        _unitCachingService = unitCachingService;
    }

    /// <summary>
    /// Gets an image for the specified asset type and name
    /// </summary>
    /// <param name="assetType">Type of asset (e.g., "units/mechs")</param>
    /// <param name="assetName">Name of the asset (unit model)</param>
    /// <returns>Bitmap if found, null otherwise</returns>
    public Bitmap? GetImage(string assetType, string assetName)
    {
        // For unit images, we use the asset name as the model identifier
        if (assetType.Equals("units/mechs", StringComparison.OrdinalIgnoreCase))
        {
            var cacheKey = $"{assetType}/{assetName}";
            return _bitmapCache.GetOrAdd(cacheKey, LoadUnitImage(assetName));
        }

        // For non-unit assets, return null (could be extended to support other asset types)
        return null;
    }

    /// <summary>
    /// Loads a unit image from the caching service and converts it to a Bitmap
    /// </summary>
    /// <param name="model">The unit model identifier</param>
    /// <returns>Bitmap if found, null otherwise</returns>
    private Bitmap? LoadUnitImage(string model)
    {
        try
        {
            var imageBytes = _unitCachingService.GetUnitImage(model);
            if (imageBytes == null) return null;

            using var stream = new MemoryStream(imageBytes);
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            // Log error but return null to gracefully handle missing images
            Console.WriteLine($"Error loading image for unit '{model}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Non-generic interface implementation
    /// </summary>
    /// <param name="assetType">Type of asset</param>
    /// <param name="assetName">Name of the asset</param>
    /// <returns>Image object (Bitmap) if found, null otherwise</returns>
    object? IImageService.GetImage(string assetType, string assetName)
    {
        return GetImage(assetType, assetName);
    }

    /// <summary>
    /// Clears the bitmap cache (useful for memory management)
    /// </summary>
    public void ClearCache()
    {
        foreach (var bitmap in _bitmapCache.Values)
        {
            bitmap?.Dispose();
        }
        _bitmapCache.Clear();
    }
}

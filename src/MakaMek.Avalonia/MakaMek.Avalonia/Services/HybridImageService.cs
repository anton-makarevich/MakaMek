using System;
using Avalonia.Media.Imaging;
using Sanet.MakaMek.Core.Services;

namespace Sanet.MakaMek.Avalonia.Services;

/// <summary>
/// Hybrid image service that routes requests to appropriate underlying services based on asset type
/// - Terrain images: routed to AvaloniaAssetImageService (Avalonia assets)
/// - Unit images: routed to CachedImageService (MMUX packages)
/// </summary>
public class HybridImageService : IImageService<Bitmap>
{
    private readonly AvaloniaAssetImageService _avaloniaAssetImageService;
    private readonly CachedImageService _cachedImageService;

    public HybridImageService(AvaloniaAssetImageService avaloniaAssetImageService, CachedImageService cachedImageService)
    {
        _avaloniaAssetImageService = avaloniaAssetImageService;
        _cachedImageService = cachedImageService;
    }

    /// <summary>
    /// Gets an image for the specified asset type and name, routing to appropriate service
    /// </summary>
    /// <param name="assetType">Type of asset (e.g., "terrain", "units/mechs")</param>
    /// <param name="assetName">Name of the asset</param>
    /// <returns>Bitmap if found, null otherwise</returns>
    public Bitmap? GetImage(string assetType, string assetName)
    {
        // Route unit images to cached service (MMUX packages)
        if (assetType.Equals("units/mechs", StringComparison.OrdinalIgnoreCase))
        {
            return _cachedImageService.GetImage(assetType, assetName);
        }

        // Route all other asset types (terrain, etc.) to Avalonia asset service
        return _avaloniaAssetImageService.GetImage(assetType, assetName);
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
}

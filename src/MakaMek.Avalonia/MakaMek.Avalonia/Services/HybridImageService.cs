using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Services;
using Sanet.MakaMek.Services.Avalonia;

namespace Sanet.MakaMek.Avalonia.Services;

/// <summary>
/// Hybrid image service that routes requests to appropriate underlying services based on asset type
/// - Terrain images: routed to TerrainCachingService (MMTX packages)
/// - Unit images: routed to CachedImageService (MMUX packages)
/// - Other assets: routed to AvaloniaAssetImageService (Avalonia assets)
/// </summary>
public class HybridImageService : IImageService<Bitmap>
{
    private readonly AvaloniaAssetImageService _avaloniaAssetImageService;
    private readonly CachedImageService _cachedImageService;
    private readonly ITerrainAssetService? _terrainAssetService;

    public HybridImageService(
        AvaloniaAssetImageService avaloniaAssetImageService,
        CachedImageService cachedImageService,
        ITerrainAssetService? terrainAssetService = null)
    {
        _avaloniaAssetImageService = avaloniaAssetImageService;
        _cachedImageService = cachedImageService;
        _terrainAssetService = terrainAssetService;
    }

    /// <summary>
    /// Gets an image for the specified asset type and name, routing to the appropriate service
    /// </summary>
    /// <param name="assetType">Type of asset (e.g., "terrain", "units/mechs")</param>
    /// <param name="assetName">Name of the asset</param>
    /// <returns>Bitmap if found, null otherwise</returns>
    public async Task<Bitmap?> GetImage(string assetType, string assetName)
    {
        // Route unit images to cached service (MMUX packages)
        if (assetType.Equals("units/mechs", StringComparison.OrdinalIgnoreCase))
        {
            return await _cachedImageService.GetImage(assetType, assetName);
        }

        // Route terrain images to terrain asset service (MMTX packages)
        if (assetType.StartsWith("terrains/", StringComparison.OrdinalIgnoreCase))
        {
            return await GetTerrainImage(assetType, assetName);
        }

        // Route all other asset types to Avalonia asset service
        return await _avaloniaAssetImageService.GetImage(assetType, assetName);
    }

    private async Task<Bitmap?> GetTerrainImage(string assetType, string assetName)
    {
        if (_terrainAssetService == null)
        {
            // Fallback to Avalonia assets if terrain service not available
            return await _avaloniaAssetImageService.GetImage(assetType, assetName);
        }

        // Parse asset type: terrains/base, terrains/overlays, terrains/edges
        var subType = assetType["terrains/".Length..];
        
        try
        {
            byte[]? imageBytes = null;
            
            // For now, use default theme "classic" - could be made configurable later
            const string defaultTheme = "classic";
            
            switch (subType.ToLowerInvariant())
            {
                case "base":
                    imageBytes = await _terrainAssetService.GetBaseTerrainImage(defaultTheme);
                    break;
                case "overlays":
                    imageBytes = await _terrainAssetService.GetTerrainOverlayImage(defaultTheme, assetName);
                    break;
                // edges require direction - handled separately via GetEdgeImage
            }
            
            if (imageBytes == null)
            {
                // Fallback to Avalonia assets
                return await _avaloniaAssetImageService.GetImage(assetType, assetName);
            }
            
            using var stream = new MemoryStream(imageBytes);
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading terrain image '{assetName}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Non-generic interface implementation
    /// </summary>
    /// <param name="assetType">Type of asset</param>
    /// <param name="assetName">Name of the asset</param>
    /// <returns>Image object (Bitmap) if found, null otherwise</returns>
    async Task<object?> IImageService.GetImage(string assetType, string assetName)
    {
        return await GetImage(assetType, assetName);
    }
}

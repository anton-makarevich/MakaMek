using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Sanet.MakaMek.Core.Services;

namespace Sanet.MakaMek.Avalonia.Services;

/// <summary>
/// Image service that retrieves images from the UnitCachingService instead of Avalonia's asset system
/// This supports loading images from MMUX packages
/// </summary>
public class CachedImageService : IImageService<Bitmap>
{
    private readonly IUnitCachingService _unitCachingService;

    public CachedImageService(IUnitCachingService unitCachingService)
    {
        _unitCachingService = unitCachingService;
    }

    /// <summary>
    /// Gets an image for the specified asset type and name
    /// </summary>
    /// <param name="assetType">Type of asset (e.g., "units/mechs")</param>
    /// <param name="assetName">Name of the asset (unit model)</param>
    /// <returns>Bitmap if found, null otherwise</returns>
    public async Task<Bitmap?> GetImage(string assetType, string assetName)
    {
        // For unit images, we use the asset name as the model identifier
        if (assetType.Equals("units/mechs", StringComparison.OrdinalIgnoreCase))
        {
            return  await LoadUnitImage(assetName);
        }

        // For non-unit assets, return null (could be extended to support other asset types)
        return null;
    }

    /// <summary>
    /// Loads a unit image from the caching service and converts it to a Bitmap
    /// </summary>
    /// <param name="model">The unit model identifier</param>
    /// <returns>Bitmap if found, null otherwise</returns>
    private async Task<Bitmap?> LoadUnitImage(string model)
    {
        try
        {
            var imageBytes = await _unitCachingService.GetUnitImage(model);
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
    async Task<object?> IImageService.GetImage(string assetType, string assetName)
    {
        return await GetImage(assetType, assetName);
    }
}

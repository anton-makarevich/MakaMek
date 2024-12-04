namespace Sanet.MekForge.Core.Services;

/// <summary>
/// Service for loading and caching images
/// </summary>
public interface IImageService
{
    /// <summary>
    /// Gets an image for the specified asset type and name
    /// </summary>
    /// <param name="assetType">Type of asset (e.g., "terrain", "unit")</param>
    /// <param name="assetName">Name of the asset (e.g., "clear", "woods")</param>
    /// <returns>Image object (platform specific)</returns>
    object? GetImage(string assetType, string assetName);
}

public interface IImageService<T>:IImageService
{
    new T? GetImage(string assetType, string assetName);
}

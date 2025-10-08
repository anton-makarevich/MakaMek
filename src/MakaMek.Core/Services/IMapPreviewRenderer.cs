namespace Sanet.MakaMek.Core.Services;

using Sanet.MakaMek.Core.Models.Map;

/// <summary>
/// Interface for rendering map preview images
/// </summary>
public interface IMapPreviewRenderer
{
    /// <summary>
    /// Generates a preview image for the provided battle map.
    /// </summary>
    /// <param name="map">The battle map instance to render.</param>
    /// <param name="previewWidth">Width of the preview image in pixels</param>
    /// <param name="previewHeight">Height of the preview image in pixels</param>
    /// <returns>Preview image as a platform-specific object (e.g., Bitmap for Avalonia)</returns>
    object? GeneratePreview(
        BattleMap map,
        int previewWidth = 300,
        int previewHeight = 300);
}


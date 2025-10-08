namespace Sanet.MakaMek.Core.Services;

/// <summary>
/// Interface for rendering map preview images
/// </summary>
public interface IMapPreviewRenderer
{
    /// <summary>
    /// Generates a preview image of a map based on the given parameters
    /// </summary>
    /// <param name="width">Map width in hexes</param>
    /// <param name="height">Map height in hexes</param>
    /// <param name="forestCoverage">Forest coverage percentage (0-100)</param>
    /// <param name="lightWoodsProbability">Probability of light woods vs heavy woods (0-100)</param>
    /// <param name="previewWidth">Width of the preview image in pixels</param>
    /// <param name="previewHeight">Height of the preview image in pixels</param>
    /// <returns>Preview image as a platform-specific object (e.g., Bitmap for Avalonia)</returns>
    object? GeneratePreview(
        int width,
        int height,
        int forestCoverage,
        int lightWoodsProbability,
        int previewWidth = 300,
        int previewHeight = 300);
}


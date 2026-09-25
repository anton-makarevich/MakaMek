using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Map.Models.Highlights;

/// <summary>
/// Interface for different types of hex highlights.
/// Supports polymorphic, collection-based highlighting with multiple simultaneous types.
/// </summary>
public interface IHexHighlightType
{
    /// <summary>
    /// The render order for this highlight type. Lower values are rendered first (underneath).
    /// </summary>
    int RenderOrder { get; }
    
    /// <summary>
    /// The name of this highlight type.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Localized text describing this highlight for UI display.
    /// </summary>
    string Render(ILocalizationService localizationService);
}

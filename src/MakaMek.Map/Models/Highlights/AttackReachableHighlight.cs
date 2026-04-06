using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Map.Models.Highlights;

/// <summary>
/// Highlight for hexes within weapon range during attack phase.
/// Rendered with light red/orange stroke/fill.
/// </summary>
public record AttackReachableHighlight(IReadOnlyList<string> WeaponNames) : IHexHighlightType
{
    public int RenderOrder => 1;
    public string Name => nameof(AttackReachableHighlight);

    public string Render(ILocalizationService localizationService) =>
        string.Join(", ", WeaponNames);
}

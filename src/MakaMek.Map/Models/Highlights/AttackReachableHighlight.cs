using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Map.Models.Highlights;

/// <summary>
/// Highlight for hexes within weapon range during attack phase.
/// Rendered with light red/orange stroke/fill.
/// </summary>
public record AttackReachableHighlight(
    IReadOnlyList<string> WeaponNames,
    AttackRangeBand RangeBand = AttackRangeBand.Medium,
    string? TacticalText = null) : IHexHighlightType
{
    public int RenderOrder => 1;
    public string Name => nameof(AttackReachableHighlight);
    public string BoundaryOutlineColor => RangeBand switch
    {
        AttackRangeBand.Short => "#66E3FF",
        AttackRangeBand.Medium => "#FFB347",
        AttackRangeBand.Mixed => "#C084FC",
        _ => "#FF8C69"
    };

    public string Render(ILocalizationService localizationService) =>
        TacticalText ?? string.Join(", ", WeaponNames);
}

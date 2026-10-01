using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Map.Models.Highlights;

/// <summary>
/// Highlight for hexes within weapon range during attack phase.
///
/// The band says which weapons can reach the hex; the renderer decides what that looks like, so
/// the colors stay in Colors.axaml rather than in the model.
/// </summary>
/// <param name="WeaponNames">The weapons that can reach this hex.</param>
/// <param name="RangeBand">Which range the reaching weapons are at.</param>
/// <param name="TacticalText">Per target detail shown instead of the weapon list, when there is any.</param>
public record AttackReachableHighlight(
    IReadOnlyList<string> WeaponNames,
    AttackRangeBand RangeBand = AttackRangeBand.Medium,
    string? TacticalText = null) : IHexHighlightType
{
    public int RenderOrder => 1;
    public string Name => nameof(AttackReachableHighlight);

    public string Render(ILocalizationService localizationService) =>
        TacticalText ?? string.Join(", ", WeaponNames);
}

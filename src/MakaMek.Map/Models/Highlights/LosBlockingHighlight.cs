using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Map.Models.Highlights;

/// <summary>
/// Highlight for hexes that are blocked from line of sight during weapon targeting.
/// Rendered with dark red stroke and semi-transparent fill.
/// </summary>
public record LosBlockingHighlight(LineOfSightBlockReason Reason, HexCoordinates? BlockingHex = null) : IHexHighlightType
{
    public int RenderOrder => 2;
    public string Name => nameof(LosBlockingHighlight);

    /// <summary>
    /// The reason why the line of sight is blocked at this hex.
    /// </summary>
    public LineOfSightBlockReason Reason { get; } = Reason;

    /// <summary>
    /// The hex that blocks LOS to the highlighted hex.
    /// </summary>
    public HexCoordinates? BlockingHex { get; } = BlockingHex;

    public string Render(ILocalizationService localizationService) =>
        Reason switch
        {
            LineOfSightBlockReason.InvalidCoordinates =>
                localizationService.GetString("HexHighlight_LosBlocked_InvalidCoordinates"),
            LineOfSightBlockReason.Elevation when BlockingHex != null =>
                string.Format(localizationService.GetString("HexHighlight_LosBlocked_Elevation"), BlockingHex),
            LineOfSightBlockReason.InterveningTerrain when BlockingHex != null =>
                string.Format(localizationService.GetString("HexHighlight_LosBlocked_InterveningTerrain"), BlockingHex),
            LineOfSightBlockReason.Elevation or LineOfSightBlockReason.InterveningTerrain =>
                localizationService.GetString("HexHighlight_LosBlocked_InvalidCoordinates"),
            _ => throw new ArgumentOutOfRangeException(nameof(Reason), Reason, null)
        };
}

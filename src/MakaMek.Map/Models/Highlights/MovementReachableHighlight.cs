namespace Sanet.MakaMek.Map.Models.Highlights;

/// <summary>
/// Highlight for hexes reachable during movement phase.
/// Rendered with light blue stroke/fill.
/// </summary>
public record MovementReachableHighlight : IHexHighlightType
{
    public int RenderOrder => 0;
    public string Name => nameof(MovementReachableHighlight);
}

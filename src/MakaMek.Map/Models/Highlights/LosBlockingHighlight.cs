namespace Sanet.MakaMek.Map.Models.Highlights;

/// <summary>
/// Highlight for hexes that block the line of sight during weapon targeting.
/// Rendered with dark red stroke and semi-transparent fill.
/// </summary>
public record LosBlockingHighlight(LineOfSightBlockReason Reason) : IHexHighlightType
{
    public int RenderOrder => 2;
    public string Name => nameof(LosBlockingHighlight);
    
    /// <summary>
    /// The reason why the line of sight is blocked at this hex.
    /// </summary>
    public LineOfSightBlockReason Reason { get; } = Reason;
}

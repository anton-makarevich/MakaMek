namespace Sanet.MakaMek.Map.Models.Highlights;

/// <summary>
/// Highlight for hexes that block line of sight during weapon targeting.
/// Rendered with dark red stroke and semi-transparent fill.
/// </summary>
public record LosBlockingHighlight : IHexHighlightType
{
    public int RenderOrder => 2;
    public string Name => nameof(LosBlockingHighlight);
    
    /// <summary>
    /// The reason why line of sight is blocked at this hex.
    /// </summary>
    public LineOfSightBlockReason? Reason { get; }
    
    public LosBlockingHighlight(LineOfSightBlockReason reason)
    {
        Reason = reason;
    }
}

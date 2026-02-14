namespace Sanet.MakaMek.Map.Data;

public record PathSegmentData
{
    public required HexPositionData From { get; init; }
    public required HexPositionData To { get; init; }
    public required int Cost { get; init; }
    public bool IsReversed { get; init; }
}
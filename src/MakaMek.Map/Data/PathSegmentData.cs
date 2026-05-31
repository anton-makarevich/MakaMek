using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;

namespace Sanet.MakaMek.Map.Data;

public record PathSegmentData
{
    public required HexPositionData From { get; init; }
    public required HexPositionData To { get; init; }
    public required IReadOnlyList<MovementCost> Costs { get; init; }
    public bool IsReversed { get; init; }
    public int ElevationChange { get; init; }
    public SegmentEvent[] Events { get; init; } = [];
}
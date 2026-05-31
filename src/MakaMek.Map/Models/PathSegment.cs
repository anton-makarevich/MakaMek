using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models.MovementCosts;

namespace Sanet.MakaMek.Map.Models;

public record PathSegment(HexPosition From, HexPosition To, IReadOnlyList<MovementCost> Costs, bool IsReversed = false, int ElevationChange = 0, SegmentEvent[]? Events = null)
{
    public SegmentEvent[] Events { get; init; } = [.. Events ?? []];

    public int Cost => Costs.Sum(c => c.Value);

    public PathSegment(PathSegmentData data)
        : this(new HexPosition(data.From), new HexPosition(data.To), data.Costs, data.IsReversed, data.ElevationChange, data.Events)
    {
    }

    public PathSegmentData ToData() => new()
    {
        From = From.ToData(),
        To = To.ToData(),
        Costs = Costs,
        IsReversed = IsReversed,
        ElevationChange = ElevationChange,
        Events = [.. Events]
    };
};

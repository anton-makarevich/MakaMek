using Sanet.MakaMek.Map.Data;

namespace Sanet.MakaMek.Map.Models;

public record PathSegment(HexPosition From, HexPosition To, int Cost, bool IsReversed = false, int ElevationChange = 0, SegmentEvent[]? Events = null)
{
    public SegmentEvent[] Events { get; init; } = [.. Events ?? []];

    public PathSegment(PathSegmentData data)
        : this(new HexPosition(data.From), new HexPosition(data.To), data.Cost, data.IsReversed, data.ElevationChange, data.Events)
    {
    }

    public PathSegmentData ToData() => new()
    {
        From = From.ToData(),
        To = To.ToData(),
        Cost = Cost,
        IsReversed = IsReversed,
        ElevationChange = ElevationChange,
        Events = [.. Events]
    };
};

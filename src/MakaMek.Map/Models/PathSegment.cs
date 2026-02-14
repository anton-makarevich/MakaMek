using Sanet.MakaMek.Map.Data;

namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Represents a segment of a path with movement cost
/// </summary>
public record PathSegment(HexPosition From, HexPosition To, int Cost, bool IsReversed = false)
{
    public PathSegment(PathSegmentData data)
        : this(new HexPosition(data.From), new HexPosition(data.To), data.Cost, data.IsReversed)
    {
    }

    public PathSegmentData ToData() => new()
    {
        From = From.ToData(),
        To = To.ToData(),
        Cost = Cost,
        IsReversed = IsReversed
    };
};

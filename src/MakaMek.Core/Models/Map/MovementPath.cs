using Sanet.MakaMek.Core.Data.Game;

namespace Sanet.MakaMek.Core.Models.Map;

public class MovementPath
{
    private IReadOnlyList<HexCoordinates>? _hexes;
    public MovementPath(IEnumerable<PathSegment> segments)
    {
        Segments = segments.ToList();
    }
    
    public MovementPath(IEnumerable<PathSegmentData> segments)
    {
        Segments = segments.Select(s => new PathSegment(s)).ToList();
    }

    public IReadOnlyList<PathSegment> Segments { get; }
    
    public int TotalCost => Segments.Sum(s => s.Cost);
    
    public int HexesTraveled => Hexes.Count;
    
    public int DistanceCovered => Start == null || Destination == null 
        ? 0 
        : Start.Coordinates.DistanceTo(Destination.Coordinates);
    
    public HexPosition? Start => Segments.Count == 0 ? null : Segments[0].From;
    
    public HexPosition? Destination => Segments.Count == 0 ? null : Segments[^1].To;
    
    public IReadOnlyList<HexCoordinates> Hexes => Start == null 
        ? []
        : _hexes ??= new List<HexCoordinates> { Start.Coordinates }
            .Concat(Segments.Select(s => s.To.Coordinates))
            .Distinct()
            .ToList();
    
    public IReadOnlyList<PathSegmentData> ToData() => Segments.Select(s => s.ToData()).ToList();
    
    public int TurnsTaken => Segments.Count(s => s.From.Coordinates == s.To.Coordinates);
    
    /// <summary>
    /// Creates a new MovementPath with all facings reversed (for backward movement)
    /// </summary>
    public MovementPath ReverseFacing()
    {
        var reversedSegments = Segments.Select(segment => new PathSegment(
            segment.From with { Facing = segment.From.Facing.GetOppositeDirection() },
            segment.To with { Facing = segment.To.Facing.GetOppositeDirection() },
            segment.Cost
        )).ToList();
        return new MovementPath(reversedSegments);
    }
}
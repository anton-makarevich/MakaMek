using Sanet.MakaMek.Core.Data.Game;

namespace Sanet.MakaMek.Core.Models.Map;

public class MovementPath
{
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
    
    public int DistanceCovered => Start.Coordinates.DistanceTo(Destination.Coordinates);
    
    public HexPosition Start => Segments[0].From;
    
    public HexPosition Destination => Segments[^1].To;
    
    public IReadOnlyList<HexCoordinates> Hexes => 
        new List<HexCoordinates> { Start.Coordinates }
            .Concat(Segments.Select(s => s.To.Coordinates).ToList())
            .Distinct()
            .ToList();
    
    public IReadOnlyList<PathSegmentData> ToData() => Segments.Select(s => s.ToData()).ToList();
    
    public int TurnsTaken => Segments.Count(s => s.From.Coordinates == s.To.Coordinates);
}
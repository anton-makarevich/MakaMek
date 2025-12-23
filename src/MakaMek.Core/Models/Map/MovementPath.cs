using Sanet.MakaMek.Core.Data.Game;

namespace Sanet.MakaMek.Core.Models.Map;

public class MovementPath : IEquatable<MovementPath>
{
    public MovementPath(IEnumerable<PathSegment> segments, bool isJump = false)
    {
        Segments = segments.ToList();
        Start = Segments.Count == 0 ? null : Segments[0].From;
        Destination = Segments.Count == 0 ? null : Segments[^1].To;
        IsJump = isJump;
    }
    
    public MovementPath(IEnumerable<PathSegmentData> segments, bool isJump = false)
    {
        Segments = segments.Select(s => new PathSegment(s)).ToList();
        Start = Segments.Count == 0 ? null : Segments[0].From;
        Destination = Segments.Count == 0 ? null : Segments[^1].To;
        IsJump = isJump;
    }

    /// <summary>
    /// Constructor for cache lookups
    /// </summary>
    internal MovementPath(HexPosition start, HexPosition destination, bool isJump)
    {
        Start = start;
        Destination = destination;
        IsJump = isJump;
        Segments = new List<PathSegment>();
    }

    public IReadOnlyList<PathSegment> Segments { get; }
    
    public int TotalCost => Segments.Sum(s => s.Cost);
    
    public int HexesTraveled => Hexes.Count;
    
    public int DistanceCovered => Start == null || Destination == null 
        ? 0 
        : Start.Coordinates.DistanceTo(Destination.Coordinates);
    
    public HexPosition? Start { get; }
    
    public HexPosition? Destination { get; }

    private bool IsJump { get; }
    
    public IReadOnlyList<HexCoordinates> Hexes => Start == null 
        ? []
        : field ??= new List<HexCoordinates> { Start.Coordinates }
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
        return new MovementPath(reversedSegments, IsJump);
    }

    public bool Equals(MovementPath? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return EqualityComparer<HexPosition?>.Default.Equals(Start, other.Start) 
               && EqualityComparer<HexPosition?>.Default.Equals(Destination, other.Destination) 
               && IsJump == other.IsJump;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((MovementPath)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Start, Destination, IsJump);
    }
}
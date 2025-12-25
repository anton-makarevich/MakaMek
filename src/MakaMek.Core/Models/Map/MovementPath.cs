using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Map;

public class MovementPath : IEquatable<MovementPath>
{
    public MovementPath(IEnumerable<PathSegment> segments, MovementType movementType)
    {
        Segments = segments.ToList();
        if (Segments.Count > 0)
        {
            Start = Segments[0].From;
            Destination = Segments[^1].To;

        }

        Hexes = Start == null
            ? []
            : new List<HexCoordinates> { Start.Coordinates }
                .Concat(Segments.Select(s => s.To.Coordinates))
                .Distinct()
                .ToList();
        HexesTraveled = Math.Max(0, Hexes.Count - 1);
        MovementType = movementType;
        IsJump = movementType == MovementType.Jump;
        TotalCost = Segments.Sum(s => s.Cost);
        TurnsTaken = Segments.Count(s => s.From.Facing != s.To.Facing);
    }

    public MovementPath(IEnumerable<PathSegmentData> segments, MovementType movementType) : this(
        segments.Select(s => new PathSegment(s)), movementType)
    {
    }
    
    /// <summary>
    /// Constructor for cache lookups
    /// </summary>
    internal MovementPath(HexPosition start, HexPosition destination, bool isJump)
    {
        Start = start;
        Destination = destination;
        IsJump = isJump;
        Segments = [];
        Hexes = [];
    }

    public IReadOnlyList<PathSegment> Segments { get; }
    
    public int TotalCost { get; }

    public int HexesTraveled { get; }

    public int DistanceCovered => Start == null || Destination == null 
        ? 0 
        : Start.Coordinates.DistanceTo(Destination.Coordinates);
    
    public HexPosition? Start { get; }
    
    public HexPosition? Destination { get; }
    
    public MovementType MovementType { get; }

    private bool IsJump { get; }
    
    public IReadOnlyList<HexCoordinates> Hexes { get; } 
    
    public IReadOnlyList<PathSegmentData> ToData() => Segments.Select(s => s.ToData()).ToList();
    
    public int TurnsTaken { get; }
    
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
        return new MovementPath(reversedSegments, MovementType);
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
        return obj.GetType() == GetType() && Equals((MovementPath)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Start, Destination, IsJump);
    }
    
    public static MovementPath CreateStandingStillPath(HexPosition position)
    {
        return new MovementPath(new List<PathSegment>
        {
            new(position, position, 0)
        }, MovementType.StandingStill);
    }
}
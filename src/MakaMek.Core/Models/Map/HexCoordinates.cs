using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Exceptions;

namespace Sanet.MakaMek.Core.Models.Map;

/// <summary>
/// Represents coordinates in a hexagonal grid using axial coordinate system
/// </summary>
public record HexCoordinates
{
    public const double HexWidth = 100;
    public const double HexHeight = 86.6;
    private const double HexHorizontalSpacing = HexWidth * 0.75;

    /// <summary>
    /// Q coordinate (column)
    /// </summary>
    public int Q { get; init; }
    
    /// <summary>
    /// R coordinate (row)
    /// </summary>
    public int R { get; init; }

    /// <summary>
    /// Gets the cube coordinate S (derived from Q and R)
    /// </summary>
    public int S { get; }

    // Cached cube coordinates
    public int X { get; }
    public int Y { get; }
    public int Z { get; }
    
    /// <summary>
    /// Gets the X coordinate in pixels for rendering
    /// </summary>
    public double H => Q * HexHorizontalSpacing;

    /// <summary>
    /// Gets the Y coordinate in pixels for rendering
    /// </summary>
    public double V => R * HexHeight - (Q % 2 == 0 ? 0 : HexHeight * 0.5);

    public HexCoordinates(HexCoordinateData data) : this(data.Q, data.R) { }
    public HexCoordinates(int q, int r)
    {
        Q = q;
        R = r;

        S = -Q - R;
        
        // Precompute cube coordinates (X, Y, Z)
        X = Q;
        Z = R - (Q + (Q & 1)) / 2; // Handles staggered row adjustment
        Y = -X - Z;
    }

    // Offsets for the six directions, adjusted for even/odd column rows
    private static readonly (int dQ, int dR)[] OddRowDirections =
    [
        (0, -1), // Direction 0: top
        (1, -1), // Direction 1: top-right
        (1, 0),  // Direction 2: bottom-right
        (0, 1),  // Direction 3: bottom
        (-1, 0), // Direction 4: bottom-left
        (-1, -1) // Direction 5: top-left
    ];

    private static readonly (int dQ, int dR)[] EvenRowDirections =
    [
        (0, -1), // Direction 0: top
        (1, 0),  // Direction 1: top-right
        (1, 1),  // Direction 2: bottom-right
        (0, 1),  // Direction 3: bottom
        (-1, 1), // Direction 4: bottom-left
        (-1, 0)  // Direction 5: top-left
    ];

    public HexCoordinates Neighbor(HexDirection direction)
    {
        var directions = (Q % 2 == 0) ? EvenRowDirections : OddRowDirections;
        var (dQ, dR) = directions[(int)direction % 6];
        return new HexCoordinates(Q + dQ, R + dR);
    }
    
    public HexDirection GetDirectionToNeighbour(HexCoordinates neighbour)
    {
        var dQ = neighbour.Q - Q;
        var dR = neighbour.R - R;

        var directions = (Q % 2 == 0) ? EvenRowDirections : OddRowDirections;

        for (var i = 0; i < directions.Length; i++)
        {
            if (directions[i].Item1 == dQ && directions[i].Item2 == dR)
            {
                return (HexDirection)i;
            }
        }

        throw new WrongHexException(neighbour, "Neighbour is not adjacent to center.");
    }
    
    /// <summary>
    /// Returns adjacent hex coordinates in all six directions
    /// </summary>
    public IEnumerable<HexCoordinates> GetAdjacentCoordinates()
    {
        var directions = (Q % 2 == 0) ? EvenRowDirections : OddRowDirections;
        foreach (var (dQ, dR) in directions)
        {
            yield return new HexCoordinates(Q + dQ, R + dR);
        }
    }

    /// <summary>
    /// Calculates distance to another hex
    /// </summary>
    public int DistanceTo(HexCoordinates other)
    {
        // Use Manhattan distance in cube space
        return Math.Max(Math.Abs(X - other.X), Math.Max(Math.Abs(Y - other.Y), Math.Abs(Z - other.Z)));
    }

    /// <summary>
    /// Returns all hex coordinates within the specified range (inclusive)
    /// Requires optimisation to remove DistanceTo
    /// </summary>
    public IEnumerable<HexCoordinates> GetCoordinatesInRange(int range)
    {
        for (var dQ = -range; dQ <= range; dQ++)
        {
            for (var dR = -range; dR <= range; dR++)
            {
                var candidate = new HexCoordinates(Q + dQ, R + dR);
                if (DistanceTo(candidate) <= range)
                {
                    yield return candidate;
                }
            }
        }
    }
    
    /// <summary>
    /// Gets coordinates of hexes that form a line between two points.
    /// Returns a list of LineOfSightSegments, where each segment may contain one or two hexes.
    /// When a segment contains two hexes with AreOptionsEqual=true, they represent equally valid
    /// options for the line of sight, and the defender should choose which one to use.
    /// </summary>
    public List<LineOfSightSegment> LineTo(HexCoordinates target)
    {
        if (Equals(target))
        {
            return [new LineOfSightSegment(target)];
        }

        var result = new List<LineOfSightSegment>();
        var current = this;
        result.Add(new LineOfSightSegment(current));

        // Get the direction vector in cube coordinates
        var dx = target.X - X;
        var dz = target.Z - Z;

        // Get the primary direction and its two adjacent directions
        var mainDir = GetMainDirection(dx, dz);
        var leftDir = (mainDir + 5) % 6;  // Counter-clockwise
        var rightDir = (mainDir + 1) % 6;  // Clockwise

        while (!current.Equals(target))
        {
            // Check the three possible next hexes (left, center, right)
            var (next, additional, areEqual) = GetNextHexInLine(current, target, mainDir, leftDir, rightDir);

            if (additional != null)
            {
                if (areEqual)
                {
                    // When options are equal, create one segment with both hexes
                    result.Add(new LineOfSightSegment(next, additional));
                }
                else
                {
                    // When options are not equal, create two separate segments
                    var nextSegment = new LineOfSightSegment(next);
                    var additionalSegment = new LineOfSightSegment(additional);
                    if (!result.Contains(nextSegment))
                        result.Add(new LineOfSightSegment(next));
                    if (!result.Contains(additionalSegment))
                        result.Add(new LineOfSightSegment(additional));
                }
            }
            else
            {
                var nextSegment = new LineOfSightSegment(next);
                if (!result.Contains(nextSegment))
                    result.Add(new LineOfSightSegment(next));
            }

            current = next;
        }

        return result;
    }

    private (HexCoordinates next, HexCoordinates? additional, bool areEqual) GetNextHexInLine(HexCoordinates current, HexCoordinates target, int mainDir, int leftDir, int rightDir)
    {
        // Calculate vectors to potential next hexes
        var mainNext = current.Neighbor((HexDirection)mainDir);
        var leftNext = current.Neighbor((HexDirection)leftDir);
        var rightNext = current.Neighbor((HexDirection)rightDir);

        // Calculate distances from this to next and from next to target
        var mainToNext = GetActualDistance(this, mainNext);
        var leftToNext = GetActualDistance(this, leftNext);
        var rightToNext = GetActualDistance(this, rightNext);

        var mainToTarget = GetActualDistance(mainNext, target);
        var leftToTarget = GetActualDistance(leftNext, target);
        var rightToTarget = GetActualDistance(rightNext, target);

        // Calculate total distances
        var mainTotal = mainToNext + mainToTarget;
        var leftTotal = leftToNext + leftToTarget;
        var rightTotal = rightToNext + rightToTarget;

        const double epsilon = 0.05; //Adjusting epsilon we can control how close the line
                                     //should be to the "corners" of the hexes    
        
        // First check if left path's total distance is equal or better
        if (leftTotal <= mainTotal + epsilon && leftTotal <= rightTotal + epsilon)
        {
            // If left total equals main total, we have two options
            if (Math.Abs(leftTotal - mainTotal) < epsilon)
            {
                // If distances to next are equal, it's a divided line
                var areEqual = Math.Abs(leftToNext - mainToNext) < epsilon;
                return (leftNext, mainNext, areEqual);
            }
            
            // If left total equals right total, we have two options
            if (Math.Abs(leftTotal - rightTotal) < epsilon)
            {
                // If distances to next are equal, it's a divided line
                var areEqual = Math.Abs(leftToNext - rightToNext) < epsilon;
                return (leftNext, rightNext, areEqual);
            }
            
            return (leftNext, null, false);
        }

        // Then check if right path's total distance equals main
        if (rightTotal <= mainTotal + epsilon)
        {
            // If right total equals main total, we have two options
            if (Math.Abs(rightTotal - mainTotal) < epsilon)
            {
                // If distances to next are equal, it's a divided line
                var areEqual = Math.Abs(rightToNext - mainToNext) < epsilon;
                return (rightNext, mainNext, areEqual);
            }
            return (rightNext, null, false);
        }

        return (mainNext, null, false);
    }

    private double GetActualDistance(HexCoordinates from, HexCoordinates to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var dz = to.Z - from.Z;
        
        // Divide by 2 because in cube coordinates, moving one hex changes two coordinates by 1
        return Math.Sqrt((dx * dx + dy * dy + dz * dz) / 2.0);
    }

    private int GetMainDirection(int dx, int dz)
    {
        // Convert cube coordinates difference to angle
        var angle = Math.Atan2(3.0 / 2 * dx, -Math.Sqrt(3) * (dz + dx / 2.0));
        
        // Convert angle to direction (0-5)
        var dir = (int)Math.Round(angle / (Math.PI / 3));
        
        // Normalize to 0-5 range
        return ((dir % 6) + 6) % 6;
    }
    
    public HexCoordinateData ToData() => new(Q, R);

    public override int GetHashCode()
    {
        return HashCode.Combine(Q, R);
    }

    public override string ToString()
    {
        return $"{Q:D2}{R:D2}";
    }

    public IEnumerable<HexCoordinates> GetHexesInFiringArc(HexDirection facing, FiringArc arc, int range)
    {
        var result = new HashSet<HexCoordinates>();
        
        // Get the cube direction vector for the facing direction
        var facingVector = GetCubeDirectionVector(facing);
        
        foreach (var hex in GetCoordinatesInRange(range))
        {
            if (hex == this) continue; // Skip the center hex
            
            // Get the vector to the target hex in cube coordinates
            var dx = hex.X - this.X;
            var dy = hex.Y - this.Y;
            var dz = hex.Z - this.Z;

            // Check if the hex is in the arc using dot product
            if (IsInArc(dx, dy, dz, facingVector.dx, facingVector.dy, facingVector.dz, arc))
            {
                result.Add(hex);
            }
        }

        return result;
    }

    /// <summary>
    /// Determines if a target hex is within a specific firing arc from this hex
    /// </summary>
    /// <param name="targetCoordinates">The coordinates of the target hex</param>
    /// <param name="facing">The direction the unit is facing</param>
    /// <param name="arc">The firing arc to check</param>
    /// <returns>True if the target is within the specified arc, false otherwise</returns>
    public bool IsInFiringArc(HexCoordinates targetCoordinates, HexDirection facing, FiringArc arc)
    {
        // Get the cube direction vector for the facing direction
        var facingVector = GetCubeDirectionVector(facing);
        
        // Get the vector to the target hex in cube coordinates
        var dx = targetCoordinates.X - this.X;
        var dy = targetCoordinates.Y - this.Y;
        var dz = targetCoordinates.Z - this.Z;

        // Use the private IsInArc method to check if the target is in the arc
        return IsInArc(dx, dy, dz, facingVector.dx, facingVector.dy, facingVector.dz, arc);
    }

    private (int dx, int dy, int dz) GetCubeDirectionVector(HexDirection dir)
    {
        return dir switch
        {
            HexDirection.Top => (0, 1, -1),         // +y, -z
            HexDirection.TopRight => (1, 0, -1),    // +x, -z
            HexDirection.BottomRight => (1, -1, 0), // +x, -y
            HexDirection.Bottom => (0, -1, 1),      // -y, +z
            HexDirection.BottomLeft => (-1, 0, 1),  // -x, +z
            HexDirection.TopLeft => (-1, 1, 0),     // -x, +y
            _ => throw new ArgumentException("Invalid direction", nameof(dir))
        };
    }

    private bool IsInArc(int dx, int dy, int dz, int fdx, int fdy, int fdz, FiringArc arc)
    {
        // Calculate dot product between the target vector and facing vector
        var dot = dx * fdx + dy * fdy + dz * fdz;
        var targetLength = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        var facingLength = Math.Sqrt(fdx * fdx + fdy * fdy + fdz * fdz);
        
        // Calculate angle in radians
        var cosAngle = dot / (targetLength * facingLength);
        // Handle floating point precision issues
        cosAngle = Math.Max(-1.0, Math.Min(1.0, cosAngle));
        var angle = Math.Acos(cosAngle);
        var degrees = angle * (180 / Math.PI);

        const double epsilon = 0.0001; // Small value to handle floating point comparisons

        // For side arcs, determine if the hex is to the left or right of facing direction
        // Using cross-product to determine which side the hex is on
        var cross = fdx * dy - dx * fdy;

        return arc switch
        {
            // Forward arc: -60° to +60° inclusive
            FiringArc.Front => degrees <= 60 + epsilon,
            // Left arc: -60° to -120° exclusive of forward boundary but inclusive of rear boundary
            FiringArc.Left => degrees is > 60 + epsilon and <= 120 + epsilon && cross > 0,
            // Right arc: +60° to +120° exclusive of forward boundary but inclusive of rear boundary
            FiringArc.Right => degrees is > 60 + epsilon and <= 120 + epsilon && cross < 0,
            // Rear arc: +120° to +180° exclusive 
            FiringArc.Rear => degrees is > 120 + epsilon and <= 180 + epsilon,
            _ => throw new ArgumentException("Invalid arc", nameof(arc))
        };
    }
}
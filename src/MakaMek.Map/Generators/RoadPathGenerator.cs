using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Generators;

/// <summary>
/// Generates branching road networks that grow inward from a random map edge.
/// Unlike rivers (which follow a single meandering path), roads branch: at every
/// hex each of the six directions is rolled independently against a probability
/// that favors continuing straight, producing organic forks and junctions.
/// Roads cannot cross elevation changes greater than 1 level.
/// </summary>
public class RoadPathGenerator
{
    private const int StraightProbability = 85;
    private const int AdjacentProbability = 10;
    private const int OtherProbability = 5;
    private const int MaxPassableLevelDifference = 1;

    private readonly int _width;
    private readonly int _height;
    private readonly Random _random;
    private readonly HashSet<HexCoordinates> _existingRoadHexes = [];
    private readonly HashSet<HexCoordinates>? _existingWaterHexes;
    private readonly Func<HexCoordinates, int>? _levelLookup;
    private readonly PathWalker _pathWalker;

    public RoadPathGenerator(
        int width,
        int height,
        Random? random = null,
        HashSet<HexCoordinates>? existingWaterHexes = null,
        Func<HexCoordinates, int>? levelLookup = null)
    {
        if (width < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be at least 1.");
        if (height < 1)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be at least 1.");

        _width = width;
        _height = height;
        _random = random ?? new Random();
        _existingWaterHexes = existingWaterHexes;
        _levelLookup = levelLookup;
        _pathWalker = new PathWalker(width, height);
    }

    public Dictionary<HexCoordinates, int> GenerateRoads(int roadCount)
    {
        if (roadCount < 0)
            throw new ArgumentOutOfRangeException(nameof(roadCount), "Road count must be non-negative.");

        var result = new Dictionary<HexCoordinates, int>();

        for (var i = 0; i < roadCount; i++)
        {
            var road = GenerateSingleRoad();
            foreach (var hex in road)
                result[hex] = 0;
        }

        return result;
    }

    private HashSet<HexCoordinates> GenerateSingleRoad()
    {
        var startHex = PickRandomEdgeStart();
        _existingRoadHexes.Add(startHex);

        // Direction each hex was entered by; the next hex prefers continuing straight.
        var straightDirections = new Dictionary<HexCoordinates, HexDirection>
        {
            [startHex] = startHex.GetDirectionToward(GetCenterHexCoordinate(), _random)
        };

        return _pathWalker.Flood(
            startHex,
            (current, candidate) => TryExtendRoad(current, candidate, straightDirections));
    }

    /// <summary>
    /// Decides whether the road branches into a candidate hex: it must be new, within the
    /// elevation limit, and win its per-direction probability roll.
    /// </summary>
    private bool TryExtendRoad(
        HexCoordinates current,
        HexCoordinates candidate,
        Dictionary<HexCoordinates, HexDirection> straightDirections)
    {
        if (_existingRoadHexes.Contains(candidate))
            return false;

        var hexLevel = _levelLookup?.Invoke(current);
        if (hexLevel != null && Math.Abs(_levelLookup!(candidate) - hexLevel.Value) > MaxPassableLevelDifference)
            return false;

        var direction = current.GetDirectionToNeighbour(candidate);
        var probability = GetProbabilityForDirection(direction, straightDirections[current]);
        if (_random.Next(100) >= probability)
            return false;

        _existingRoadHexes.Add(candidate);
        straightDirections[candidate] = direction;
        return true;
    }

    private static int GetProbabilityForDirection(HexDirection candidate, HexDirection straightDirection)
    {
        return straightDirection.ShortestRotationTo(candidate) switch
        {
            0 => StraightProbability,   // same direction
            1 => AdjacentProbability,   // one rotation away (±1)
            _ => OtherProbability       // two or three rotations away
        };
    }

    private HexCoordinates PickRandomEdgeStart()
    {
        var edgeHexes = new List<HexCoordinates>();

        for (var q = 1; q <= _width; q++)
        {
            edgeHexes.Add(new HexCoordinates(q, 1));
        }

        if (_height > 1)
        {
            for (var q = 1; q <= _width; q++)
            {
                edgeHexes.Add(new HexCoordinates(q, _height));
            }
        }

        for (var r = 2; r < _height; r++)
        {
            edgeHexes.Add(new HexCoordinates(1, r));
        }

        if (_width > 1)
        {
            for (var r = 2; r < _height; r++)
            {
                edgeHexes.Add(new HexCoordinates(_width, r));
            }
        }

        if (_existingWaterHexes is { Count: > 0 })
            edgeHexes.RemoveAll(h => _existingWaterHexes.Contains(h));

        if (edgeHexes.Count == 0)
            throw new InvalidOperationException(
                "No valid edge hexes available for road generation (all edges are water).");
        return edgeHexes[_random.Next(edgeHexes.Count)];
    }

    private HexCoordinates GetCenterHexCoordinate()
    {
        return new HexCoordinates((_width + 1) / 2, (_height + 1) / 2);
    }
}

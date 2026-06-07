using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Generators;

/// <summary>
/// Generates branching road networks that grow inward from a random map edge.
/// Unlike rivers (which follow a single meandering path), roads branch: at every
/// hex each of the six directions is rolled independently against a probability
/// that favours continuing straight, producing organic forks and junctions.
/// </summary>
public class RoadPathGenerator
{
    private const int StraightProbability = 70;
    private const int AdjacentProbability = 15;
    private const int OtherProbability = 5;

    private readonly int _width;
    private readonly int _height;
    private readonly Random _random;
    private readonly HashSet<HexCoordinates> _existingRoadHexes = [];

    public RoadPathGenerator(int width, int height, Random? random = null)
    {
        if (width < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be at least 1.");
        if (height < 1)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be at least 1.");

        _width = width;
        _height = height;
        _random = random ?? new Random();
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

    internal HashSet<HexCoordinates> GenerateSingleRoad()
    {
        var road = new HashSet<HexCoordinates>();

        var startHex = PickRandomEdgeStart();
        road.Add(startHex);
        _existingRoadHexes.Add(startHex);

        var queue = new Queue<(HexCoordinates Hex, HexDirection Direction)>();
        var initialDirection = startHex.GetDirectionToward(GetCenterHexCoordinate(), _random);
        queue.Enqueue((startHex, initialDirection));

        while (queue.Count > 0)
        {
            var (hex, straightDirection) = queue.Dequeue();

            foreach (var candidate in HexDirectionExtensions.AllDirections)
            {
                var targetHex = hex.GetNeighbour(candidate);

                if (targetHex.Q < 1 || targetHex.Q > _width ||
                    targetHex.R < 1 || targetHex.R > _height)
                    continue;

                if (road.Contains(targetHex) || _existingRoadHexes.Contains(targetHex))
                    continue;

                var probability = GetProbabilityForDirection(candidate, straightDirection);
                if (_random.Next(100) >= probability)
                    continue;

                road.Add(targetHex);
                _existingRoadHexes.Add(targetHex);
                queue.Enqueue((targetHex, candidate));
            }
        }

        return road;
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

        return edgeHexes[_random.Next(edgeHexes.Count)];
    }

    private HexCoordinates GetCenterHexCoordinate()
    {
        return new HexCoordinates((_width + 1) / 2, (_height + 1) / 2);
    }
}

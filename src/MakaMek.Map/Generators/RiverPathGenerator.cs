using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Generators;

public class RiverPathGenerator
{
    private readonly int _width;
    private readonly int _height;
    private readonly Random _random;
    private readonly HashSet<HexCoordinates>? _existingWaterHexes;

    public RiverPathGenerator(
        int width,
        int height,
        Random? random = null,
        HashSet<HexCoordinates>? existingWaterHexes = null)
    {
        if (width < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be at least 1.");
        if (height < 1)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be at least 1.");

        _width = width;
        _height = height;
        _random = random ?? new Random();
        _existingWaterHexes = existingWaterHexes;
    }

    public Dictionary<HexCoordinates, int> GenerateRivers(int riverCount)
    {
        if (riverCount < 0)
            throw new ArgumentOutOfRangeException(nameof(riverCount), "River count must be non-negative.");

        var result = new Dictionary<HexCoordinates, int>();

        for (var i = 0; i < riverCount; i++)
        {
            var river = GenerateSingleRiver(result);
            foreach (var hex in river)
                result[hex] = 0;
        }

        return result;
    }

    internal List<HexCoordinates> GenerateSingleRiver(
        Dictionary<HexCoordinates, int> existingRivers)
    {
        var river = new List<HexCoordinates>();

        var startHex = PickRandomEdgeStart();
        river.Add(startHex);

        var centerDirection = startHex.GetDirectionToward(GetCenterHexCoordinate(), _random);
        var currentPos = startHex;
        HexDirection? currentDir = null;

        while (true)
        {
            var nextDir = GetCurrentDirection(currentDir);
            var nextHex = currentPos.GetNeighbour(nextDir);

            if (nextHex.Q < 1 || nextHex.Q > _width ||
                nextHex.R < 1 || nextHex.R > _height)
                break;

            if (_existingWaterHexes?.Contains(nextHex) == true)
                break;

            if (existingRivers.ContainsKey(nextHex))
                break;

            if (river.Contains(nextHex))
                break;

            river.Add(nextHex);
            currentPos = nextHex;
            currentDir = nextDir;
        }

        return river;

        HexDirection GetCurrentDirection(HexDirection? dir)
        {
            if (dir == null)
                return centerDirection;

            var roll = _random.NextDouble();
            return roll switch
            {
                < 0.5 => dir.Value,
                < 0.75 => dir.Value.Rotate(1),
                _ => dir.Value.Rotate(-1)
            };
        }
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

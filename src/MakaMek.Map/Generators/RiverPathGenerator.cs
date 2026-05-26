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

        var (startHex, _) = PickRandomEdgeStart();
        river.Add(startHex);

        var centerDirection = startHex.GetDirectionToward(GetCenterHexCoordinate());
        var currentPos = startHex;
        var currentDir = centerDirection;

        // Take the first deterministic step toward the map center
        var secondHex = currentPos.GetNeighbour(centerDirection);
        if (secondHex.Q < 1 || secondHex.Q > _width ||
            secondHex.R < 1 || secondHex.R > _height)
            return river;

        if (_existingWaterHexes?.Contains(secondHex) == true)
            return river;

        if (existingRivers.ContainsKey(secondHex))
            return river;

        river.Add(secondHex);
        currentPos = secondHex;

        while (true)
        {
            var roll = _random.NextDouble();
            var nextDir = roll switch
            {
                < 0.5 => currentDir,
                < 0.75 => currentDir.Rotate(1),
                _ => currentDir.Rotate(-1)
            };

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
    }

    private (HexCoordinates hex, HexDirection direction) PickRandomEdgeStart()
    {
        var edgeHexes = new List<(HexCoordinates hex, HexDirection[] directions)>();

        for (var q = 1; q <= _width; q++)
        {
            var hex = new HexCoordinates(q, 1);
            edgeHexes.Add((hex,
            [
                HexDirection.BottomRight, HexDirection.Bottom, HexDirection.BottomLeft
            ]));
        }

        if (_height > 1)
        {
            for (var q = 1; q <= _width; q++)
            {
                var hex = new HexCoordinates(q, _height);
                edgeHexes.Add((hex,
                [
                    HexDirection.TopRight, HexDirection.Top, HexDirection.TopLeft
                ]));
            }
        }

        for (var r = 2; r < _height; r++)
        {
            var hex = new HexCoordinates(1, r);
            edgeHexes.Add((hex,
            [
                HexDirection.TopRight, HexDirection.BottomRight
            ]));
        }

        if (_width > 1)
        {
            for (var r = 2; r < _height; r++)
            {
                var hex = new HexCoordinates(_width, r);
                edgeHexes.Add((hex,
                [
                    HexDirection.TopLeft, HexDirection.BottomLeft
                ]));
            }
        }

        var (startHex, validDirections) = edgeHexes[_random.Next(edgeHexes.Count)];
        var direction = validDirections[_random.Next(validDirections.Length)];

        return (startHex, direction);
    }

    private HexCoordinates GetCenterHexCoordinate()
    {
        return new HexCoordinates((_width + 1) / 2, (_height + 1) / 2);
    }
}

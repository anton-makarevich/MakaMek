using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Generators;

public class RiverPathGenerator
{
    private readonly int _width;
    private readonly int _height;
    private readonly Random _random;
    private readonly HashSet<HexCoordinates>? _existingWaterHexes;
    private readonly Func<HexCoordinates, int>? _levelLookup;
    private readonly PathWalker _pathWalker;

    public RiverPathGenerator(
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
        var startHex = PickRandomEdgeStart();
        var centerDirection = startHex.GetDirectionToward(GetCenterHexCoordinate(), _random);

        return new RiverWalk(this, existingRivers).Run(startHex, centerDirection);
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

    /// <summary>
    /// Walks a single river chain from an edge start towards the map center. The previous direction
    /// is kept with the highest probability; otherwise the walk turns left or right. The chain stops
    /// at the map edge, at existing water or the same river, or when the elevation gradient reverses.
    /// </summary>
    private sealed class RiverWalk(RiverPathGenerator owner, Dictionary<HexCoordinates, int> existingRivers)
    {
        private readonly LevelFlowTracker _flow = new(owner._levelLookup);
        private HexDirection? _currentDirection;

        public List<HexCoordinates> Run(HexCoordinates start, HexDirection centerDirection)
        {
            var river = new List<HexCoordinates> { start };
            var current = start;

            while (true)
            {
                var direction = ChooseDirection(centerDirection);
                var next = current.GetNeighbour(direction);

                if (!CanEnter(next, current, river))
                    break;

                river.Add(next);
                _currentDirection = direction;
                current = next;
            }

            return river;
        }

        private HexDirection ChooseDirection(HexDirection centerDirection)
        {
            if (_currentDirection is not { } direction)
                return centerDirection;

            var roll = owner._random.NextDouble();
            return roll switch
            {
                < 0.5 => direction,
                < 0.75 => direction.Rotate(1),
                _ => direction.Rotate(-1)
            };
        }

        private bool CanEnter(HexCoordinates next, HexCoordinates current, List<HexCoordinates> river)
        {
            if (!owner._pathWalker.IsOnMap(next)) return false;
            if (owner._existingWaterHexes?.Contains(next) == true) return false;
            if (existingRivers.ContainsKey(next)) return false;
            if (river.Contains(next)) return false;

            return _flow.Allows(current, next);
        }
    }

    /// <summary>
    /// Keeps a river flowing in a single elevation direction: the first non-zero level change locks
    /// the flow sign, and a later reversal ends the river.
    /// </summary>
    private sealed class LevelFlowTracker(Func<HexCoordinates, int>? levelLookup)
    {
        private int? _lockedSign;

        public bool Allows(HexCoordinates from, HexCoordinates to)
        {
            if (levelLookup is null) return true;

            var delta = levelLookup(to) - levelLookup(from);
            if (delta == 0) return true;

            var sign = Math.Sign(delta);
            if (_lockedSign is null)
            {
                _lockedSign = sign;
                return true;
            }

            return sign == _lockedSign.Value;
        }
    }
}

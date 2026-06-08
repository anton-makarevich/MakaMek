namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Represents a cached movement path between two positions.
/// </summary>
public class MovementPathCache
{
    private readonly Dictionary<MovementPathCacheKey, MovementPath> _cache = [];

    public void Add(MovementPath path, int unitHeight)
    {
        var key = new MovementPathCacheKey(path.Start, path.Destination, path.IsJump, path.MaxLevelChange, unitHeight);
        _cache[key] = path;
    }

    public MovementPath? Get(HexPosition start, HexPosition destination, bool isJump, int? maxLevelChange = null, int unitHeight = 0)
    {
        var key = new MovementPathCacheKey(start, destination, isJump, maxLevelChange, unitHeight);
        return _cache.TryGetValue(key, out var cachedPath) 
            ? cachedPath 
            : null;
    }

    public void Invalidate(HexCoordinates coordinate)
    {
        var toRemove = _cache
            .Where(kvp => kvp.Value.Hexes.Contains(coordinate))
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var key in toRemove)
        {
            _cache.Remove(key);
        }
    }

    public void Clear()
    {
        _cache.Clear();
    }
}
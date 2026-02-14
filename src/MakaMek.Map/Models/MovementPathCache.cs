namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Represents a cached movement path between two positions.
/// </summary>
public class MovementPathCache
{
    private readonly HashSet<MovementPath> _cache = [];

    public void Add(MovementPath path)
    {
        _cache.Add(path);
    }

    public MovementPath? Get(HexPosition start, HexPosition destination, bool isJump)
    {
        var probe = new MovementPath(start, destination, isJump);
        return _cache.TryGetValue(probe, out var cachedPath) 
            ? cachedPath 
            : null;
    }

    public void Invalidate(HexCoordinates coordinate)
    {
        _cache.RemoveWhere(p => p.Hexes.Contains(coordinate));
    }

    public void Clear()
    {
        _cache.Clear();
    }
}

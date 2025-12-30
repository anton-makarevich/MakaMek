using System.Collections.Concurrent;

namespace Sanet.MakaMek.Core.Models.Map;

/// <summary>
/// Represents a cached line of sight path between two hexes.
/// The path is stored in one direction but can be reversed when needed.
/// </summary>
public class LineOfSightCache
{
    private readonly ConcurrentDictionary<(HexCoordinates from, HexCoordinates to), List<HexCoordinates>> _cache = new();

    public void AddPath(HexCoordinates from, HexCoordinates to, List<HexCoordinates> path)
    {
        _cache[(from, to)] = path;
    }

    public bool TryGetPath(HexCoordinates from, HexCoordinates to, out List<HexCoordinates>? path)
    {
        // Try to get a direct path
        if (_cache.TryGetValue((from, to), out var directPath))
        {
            path = directPath.ToList();
            return true;
        }
        
        // Try to get a reversed path
        if (_cache.TryGetValue((to, from), out var reversedPath))
        {
            path = reversedPath.ToList();
            path.Reverse();
            return true;
        }

        path = null;
        return false;
    }

    public void Clear()
    {
        _cache.Clear();
    }
}

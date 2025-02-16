namespace Sanet.MekForge.Core.Models.Map;

/// <summary>
/// Represents a cached line of sight path between two hexes.
/// The path is stored in one direction but can be reversed when needed.
/// </summary>
public class LineOfSightCache
{
    private readonly Dictionary<(HexCoordinates from, HexCoordinates to), List<HexCoordinates>> _cache = new();

    public void AddPath(HexCoordinates from, HexCoordinates to, List<HexCoordinates> path)
    {
        _cache[(from, to)] = path;
    }

    public bool TryGetPath(HexCoordinates from, HexCoordinates to, out List<HexCoordinates>? path)
    {
        // Try to get direct path
        if (_cache.TryGetValue((from, to), out path))
            return true;

        // Try to get reversed path
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

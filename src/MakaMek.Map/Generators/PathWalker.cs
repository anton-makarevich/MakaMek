using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Generators;

/// <summary>
/// Shared traversal primitives for the terrain path generators: a 1-based map-bounds check and a
/// breadth-first flood that grows a set of hexes outward from a start hex. Each generator supplies
/// its own per-hex legality predicate; the walk shape lives here once.
/// </summary>
internal sealed class PathWalker(int width, int height)
{
    public bool IsOnMap(HexCoordinates coordinates)
    {
        return coordinates.Q >= 1 && coordinates.Q <= width &&
               coordinates.R >= 1 && coordinates.R <= height;
    }

    /// <summary>
    /// Grows a set of hexes outward from <paramref name="start"/> in breadth-first order.
    /// </summary>
    /// <param name="start">Hex where the flood starts; always part of the result.</param>
    /// <param name="tryEnter">
    /// Per-candidate legality check, receiving the hex being expanded and the candidate neighbour.
    /// Returning <c>true</c> accepts the candidate into the result and puts it on the queue.
    /// </param>
    /// <param name="maxSize">Optional upper bound on the number of hexes in the result.</param>
    /// <returns>The accepted hexes, including <paramref name="start"/>.</returns>
    public HashSet<HexCoordinates> Flood(
        HexCoordinates start,
        Func<HexCoordinates, HexCoordinates, bool> tryEnter,
        int? maxSize = null)
    {
        var visited = new HashSet<HexCoordinates> { start };
        var toVisit = new Queue<HexCoordinates>();
        toVisit.Enqueue(start);

        var sizeLimitReached = false;
        while (toVisit.Count > 0 && !sizeLimitReached)
        {
            var current = toVisit.Dequeue();
            sizeLimitReached = Expand(current, visited, tryEnter, maxSize, toVisit);
        }

        return visited;
    }

    /// <summary>
    /// Expands one hex into its accepted neighbours.
    /// </summary>
    /// <returns><c>true</c> when the size limit stopped the expansion.</returns>
    private bool Expand(
        HexCoordinates current,
        HashSet<HexCoordinates> visited,
        Func<HexCoordinates, HexCoordinates, bool> tryEnter,
        int? maxSize,
        Queue<HexCoordinates> toVisit)
    {
        foreach (var candidate in current.GetAllNeighbours())
        {
            if (maxSize is { } limit && visited.Count >= limit)
                return true;

            if (!IsOnMap(candidate) || visited.Contains(candidate))
                continue;

            if (!tryEnter(current, candidate))
                continue;

            visited.Add(candidate);
            toVisit.Enqueue(candidate);
        }

        return false;
    }
}

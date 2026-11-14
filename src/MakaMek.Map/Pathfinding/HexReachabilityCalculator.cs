using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Pathfinding;

/// <summary>
/// Computes every hex a unit can reach within a movement budget, for both walking and jumping
/// movement. Both traversals run through the shared <see cref="HexBreadthFirstSearch"/>; only the
/// successor and legality rules differ.
/// </summary>
internal sealed class HexReachabilityCalculator(IHexGrid grid, IHexMovementRules movementRules)
{
    private static readonly IReadOnlySet<HexCoordinates> NoProhibitedHexes = new HashSet<HexCoordinates>();

    /// <summary>
    /// Gets all valid hexes that can be reached with the given movement points, considering facing.
    /// Multiple entries may be returned for the same coordinate when both surfaces are
    /// independently reachable (e.g., both Ground and Bridge surfaces of a bridge hex).
    /// </summary>
    public IEnumerable<HexReachabilityData> GetReachable(
        HexPosition start,
        int maxMovementPoints,
        int unitHeight,
        IReadOnlySet<HexCoordinates>? prohibitedHexes,
        int? maxLevelChange)
    {
        var blocked = prohibitedHexes ?? NoProhibitedHexes;

        var costs = HexBreadthFirstSearch.Run(
            start,
            (current, currentCost) => ExpandWalk(current, currentCost, maxMovementPoints, unitHeight, blocked, maxLevelChange));

        var bestCosts = SummariseByHexAndSurface(costs);

        return bestCosts.Select(entry => new HexReachabilityData(entry.Key.Coordinates, entry.Key.Surface, entry.Value));
    }

    /// <summary>
    /// Gets all valid hexes that can be reached with jumping movement, where each hex costs 1 MP
    /// regardless of terrain or facing direction.
    /// </summary>
    public IEnumerable<HexReachabilityData> GetJumpReachable(
        HexCoordinates start,
        int movementPoints,
        IReadOnlySet<HexCoordinates>? prohibitedHexes,
        HexSurface startSurface)
    {
        var blocked = prohibitedHexes ?? NoProhibitedHexes;
        var startHex = grid.GetHex(start);
        if (startHex == null || movementPoints <= 0)
            return [];

        var ceiling = startHex.GetStandingLevel(startSurface) + movementPoints;

        // A destination is reachable when a legal path of geometric length exists. Prohibited
        // hexes only block destinations: a jump passes over them.
        var distances = HexBreadthFirstSearch.Run(
            start,
            (current, currentDistance) => ExpandJump(current, currentDistance, movementPoints, ceiling));

        return distances
            .Where(pair => pair.Key != start
                           && !blocked.Contains(pair.Key)
                           && pair.Value == start.DistanceTo(pair.Key))
            .Select(pair => new HexReachabilityData(
                pair.Key,
                grid.GetHex(pair.Key)!.GetHighestSurface(),
                pair.Value))
            .ToList();
    }

    /// <summary>
    /// Successors of a walking position: every surface of every adjacent hex the movement rules
    /// allow, priced with the entry cost, the turning cost and the elevation cost.
    /// </summary>
    private IEnumerable<(HexPosition Node, int Cost)> ExpandWalk(
        HexPosition current,
        int currentCost,
        int maxMovementPoints,
        int unitHeight,
        IReadOnlySet<HexCoordinates> prohibitedHexes,
        int? maxLevelChange)
    {
        var currentHex = grid.GetHex(current.Coordinates);
        if (currentHex == null) yield break;

        foreach (var neighborCoordinates in current.Coordinates.GetAllNeighbours())
        {
            var neighborHex = grid.GetHex(neighborCoordinates);
            if (neighborHex == null || prohibitedHexes.Contains(neighborCoordinates))
                continue;

            var requiredFacing = current.Coordinates.GetDirectionToNeighbour(neighborCoordinates);
            var turningCost = current.GetTurningCost(requiredFacing);

            foreach (var transition in movementRules.GetTransitions(
                         currentHex,
                         neighborHex,
                         current.Surface,
                         unitHeight,
                         maxLevelChange))
            {
                var totalCost = currentCost + transition.MovementCost + turningCost;
                if (totalCost > maxMovementPoints)
                    continue;

                yield return (new HexPosition(neighborCoordinates, requiredFacing, transition.Surface), totalCost);
            }
        }
    }

    /// <summary>
    /// Successors of a jumping position: each adjacent hex on the map whose reference level clears
    /// the jump ceiling, at a flat cost of one movement point.
    /// </summary>
    private IEnumerable<(HexCoordinates Node, int Cost)> ExpandJump(
        HexCoordinates current,
        int currentDistance,
        int movementPoints,
        int ceiling)
    {
        if (currentDistance >= movementPoints) yield break;

        foreach (var neighbor in current.GetAllNeighbours())
        {
            if (!grid.IsOnMap(neighbor)) continue;

            var neighborHex = grid.GetHex(neighbor);
            if (neighborHex == null || neighborHex.GetReferenceLevel() > ceiling) continue;

            yield return (neighbor, currentDistance + 1);
        }
    }

    /// <summary>
    /// Collapses the per-position costs into the cheapest cost per hex and surface, in the order the
    /// positions were first visited.
    /// </summary>
    private static Dictionary<(HexCoordinates Coordinates, HexSurface Surface), int> SummariseByHexAndSurface(
        Dictionary<HexPosition, int> costs)
    {
        var bestCosts = new Dictionary<(HexCoordinates, HexSurface), int>();
        foreach (var (position, cost) in costs)
        {
            var key = (position.Coordinates, position.Surface);
            if (!bestCosts.TryGetValue(key, out var bestCost) || cost < bestCost)
            {
                bestCosts[key] = cost;
            }
        }

        return bestCosts;
    }
}

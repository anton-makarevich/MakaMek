namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Defines the pathfinding strategy to use when finding paths between hexes
/// </summary>
public enum PathFindingMode
{
    /// <summary>
    /// Finds the shortest path to the destination (default behavior).
    /// Minimizes movement cost and number of hexes traversed.
    /// Use for efficient movement with manual waypoints.
    /// </summary>
    Shortest = 0,

    /// <summary>
    /// Finds the longest path within the available movement budget.
    /// Maximizes hexes traversed to increase target movement modifiers for defensive benefits.
    /// Use for automatic defensive positioning.
    /// </summary>
    Longest = 1
}

using Sanet.MakaMek.Map.Data;

namespace Sanet.MakaMek.Map.Models;

public interface IBattleMap
{
    int Width { get; }
    int Height { get; }
    string Biome { get; }

    /// <summary>
    /// Adds a hex to the map. Throws HexOutsideOfMapBoundariesException if hex coordinates are outside map boundaries
    /// </summary>
    void AddHex(Hex hex);

    Hex? GetHex(HexCoordinates coordinates);

    /// <summary>
    /// Gets the level difference between two hexes by their coordinates
    /// </summary>
    /// <param name="firstHex">The first hex coordinates</param>
    /// <param name="secondHex">The second hex coordinates</param>
    /// <returns>The difference in levels (firstHex.Level - secondHex.Level)</returns>
    /// <exception cref="ArgumentException">Thrown if either hex is not found on the map</exception>
    int GetLevelDifference(HexCoordinates firstHex, HexCoordinates secondHex);

    /// <summary>
    /// Finds a path between two positions, considering facing direction and movement costs
    /// </summary>
    MovementPath? FindPath(HexPosition start,
        HexPosition target,
        MovementType movementType,
        int maxMovementPoints,
        IReadOnlySet<HexCoordinates>? prohibitedHexes = null,
        PathFindingMode pathFindingMode = PathFindingMode.Shortest,
        int? maxLevelChange = null);

    /// <summary>
    /// Gets all valid hexes that can be reached with given movement points, considering facing
    /// </summary>
    IEnumerable<(HexCoordinates coordinates, int cost)> GetReachableHexes(
        HexPosition start,
        int maxMovementPoints,
        IReadOnlySet<HexCoordinates>? prohibitedHexes = null,
        int? maxLevelChange = null);

    /// <summary>
    /// Gets all valid hexes that can be reached with jumping movement, where each hex costs 1 MP
    /// regardless of terrain or facing direction
    /// </summary>
    IEnumerable<HexCoordinates> GetJumpReachableHexes(
        HexCoordinates start,
        int movementPoints,
        IReadOnlySet<HexCoordinates>? prohibitedHexes = null);

    /// <summary>
    /// Calculates the line of sight between two hexes and returns a result with full context.
    /// </summary>
    /// <param name="from">Source hex coordinates</param>
    /// <param name="to">Target hex coordinates</param>
    /// <param name="attackerHeight">Height of the attacking unit in levels (added to hex level).</param>
    /// <param name="targetHeight">Height of the target unit in levels (added to hex level). Defaults to 0 for no target.</param>
    /// <returns>
    /// A <see cref="LineOfSightResult"/> whose <see cref="LineOfSightResult.HasLineOfSight"/> property
    /// indicates whether LOS exists, with additional details about the blocking hex and reason.
    /// </returns>
    LineOfSightResult GetLineOfSight(HexCoordinates from, HexCoordinates to, int attackerHeight, int targetHeight = 0);

    IEnumerable<Hex> GetHexes();

    /// <summary>
    /// Converts the battle map to a data object including biome and hex data
    /// </summary>
    /// <returns>BattleMapData object representing the map</returns>
    BattleMapData ToData();

    /// <summary>
    /// Gets hexes along the line of sight between two coordinates, including terrain information
    /// </summary>
    IReadOnlyList<Hex> GetHexesAlongLineOfSight(HexCoordinates from, HexCoordinates to);

    /// <summary>
    /// Clears the line of sight cache.
    /// </summary>
    void ClearLosCache();

    bool IsOnMap(HexCoordinates coordinates);

    /// <summary>
    /// Gets the edge information for all 6 edges of a hex
    /// </summary>
    /// <param name="coordinates">The coordinates of the hex</param>
    /// <returns>A list of HexEdge objects for all 6 directions. Returns empty list if hex doesn't exist.</returns>
    IReadOnlyList<HexEdge> GetHexEdges(HexCoordinates coordinates);
}
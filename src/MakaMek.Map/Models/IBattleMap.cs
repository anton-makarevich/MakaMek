using Sanet.MakaMek.Map.Data;

namespace Sanet.MakaMek.Map.Models;

public interface IBattleMap
{
    int Width { get; }
    int Height { get; }

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
    /// Checks if there is a line of sight between two hexes
    /// </summary>
    bool HasLineOfSight(HexCoordinates from, HexCoordinates to);

    IEnumerable<Hex> GetHexes();

    /// <summary>
    /// Converts the battle map to a list of hex data objects
    /// </summary>
    /// <returns>List of hex data objects representing the map</returns>
    List<HexData> ToData();

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
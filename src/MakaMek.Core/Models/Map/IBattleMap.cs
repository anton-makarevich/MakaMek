using Sanet.MakaMek.Core.Data.Map;

namespace Sanet.MakaMek.Core.Models.Map;

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
    /// Finds a path between two positions, considering facing direction and movement costs
    /// </summary>
    MovementPath? FindPath(HexPosition start, HexPosition target, int maxMovementPoints, IEnumerable<HexCoordinates>? prohibitedHexes = null);

    /// <summary>
    /// Gets all valid hexes that can be reached with given movement points, considering facing
    /// </summary>
    IEnumerable<(HexCoordinates coordinates, int cost)> GetReachableHexes(
        HexPosition start,
        int maxMovementPoints,
        IEnumerable<HexCoordinates>? prohibitedHexes = null);

    /// <summary>
    /// Gets all valid hexes that can be reached with jumping movement, where each hex costs 1 MP
    /// regardless of terrain or facing direction
    /// </summary>
    IEnumerable<HexCoordinates> GetJumpReachableHexes(
        HexCoordinates start,
        int movementPoints,
        IEnumerable<HexCoordinates>? prohibitedHexes = null);

    /// <summary>
    /// Checks if there is line of sight between two hexes
    /// </summary>
    bool HasLineOfSight(HexCoordinates from, HexCoordinates to);

    IEnumerable<Hex> GetHexes();

    /// <summary>
    /// Converts the battle map to a list of hex data objects
    /// </summary>
    /// <returns>List of hex data objects representing the map</returns>
    List<HexData> ToData();

    MovementPath? FindJumpPath(HexPosition from, HexPosition to, int movementPoints);

    /// <summary>
    /// Gets hexes along the line of sight between two coordinates, including terrain information
    /// </summary>
    IReadOnlyList<Hex> GetHexesAlongLineOfSight(HexCoordinates from, HexCoordinates to);

    /// <summary>
    /// Clears the line of sight cache. Should be called at the end of each turn.
    /// </summary>
    void ClearLosCache();

    bool IsOnMap(HexCoordinates coordinates);
}
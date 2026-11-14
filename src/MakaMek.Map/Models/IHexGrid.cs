namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Read-only access to the hex store that pathfinding, reachability and line-of-sight
/// traversal need. Implemented by <see cref="HexGrid"/> and consumed by the traversal
/// algorithms so they never depend on the full battle-map façade.
/// </summary>
internal interface IHexGrid
{
    int Width { get; }

    int Height { get; }

    Hex? GetHex(HexCoordinates coordinates);

    bool IsOnMap(HexCoordinates coordinates);
}

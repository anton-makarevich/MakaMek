using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Exceptions;

public class HexOutsideOfMapBoundariesException : Exception
{
    public HexCoordinates Coordinates { get; }
    public int MapWidth { get; }
    public int MapHeight { get; }

    public HexOutsideOfMapBoundariesException(HexCoordinates coordinates, int mapWidth, int mapHeight)
        : base($"Hex at coordinates ({coordinates.Q}, {coordinates.R}) is outside of map boundaries ({mapWidth}x{mapHeight})")
    {
        Coordinates = coordinates;
        MapWidth = mapWidth;
        MapHeight = mapHeight;
    }
}

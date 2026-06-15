using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Data;

public readonly record struct ReachableHexData(
    HexCoordinates Coordinates,
    HexSurface Surface,
    int Cost);

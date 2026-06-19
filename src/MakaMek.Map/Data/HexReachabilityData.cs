using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Data;

public readonly record struct HexReachabilityData(
    HexCoordinates Coordinates,
    HexSurface Surface,
    int Cost);

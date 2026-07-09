using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Data;

public sealed record HexRenderData(
    Hex Hex,
    IReadOnlyList<HexEdge> Edges,
    CanonicalBitmaskResult? WaterBitmask,
    CanonicalBitmaskResult? RoadBitmask);

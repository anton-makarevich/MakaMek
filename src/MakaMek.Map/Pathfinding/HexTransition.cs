using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Pathfinding;

/// <summary>
/// A single legal transition from a hex to one of the surfaces of an adjacent hex.
/// </summary>
/// <param name="Surface">Destination surface that can be occupied.</param>
/// <param name="EnterCost">Terrain and entry cost of entering the destination hex.</param>
/// <param name="LevelCost">Extra cost for the elevation difference between the two surfaces.</param>
internal readonly record struct HexTransition(HexSurface Surface, int EnterCost, int LevelCost)
{
    public int MovementCost => EnterCost + LevelCost;
}

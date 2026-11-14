using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Pathfinding;

/// <summary>
/// Supplies the legal transitions between two adjacent hexes, including their movement cost.
/// Terrain and elevation rules live behind this collaborator so the traversal algorithms stay
/// independent of the concrete movement rules.
/// </summary>
internal interface IHexMovementRules
{
    /// <summary>
    /// Enumerates the surfaces of <paramref name="toHex"/> that can be entered from the given
    /// surface of <paramref name="fromHex"/>, together with the cost of each transition.
    /// </summary>
    /// <param name="fromHex">Hex currently occupied.</param>
    /// <param name="toHex">Adjacent hex being entered.</param>
    /// <param name="fromSurface">Surface the moving unit currently stands on.</param>
    /// <param name="unitHeight">Height of the moving unit, used for clearance checks.</param>
    /// <param name="maxLevelChange">Maximum allowed elevation change, or null for no limit.</param>
    IEnumerable<HexTransition> GetTransitions(
        Hex fromHex,
        Hex toHex,
        HexSurface fromSurface,
        int unitHeight,
        int? maxLevelChange);
}

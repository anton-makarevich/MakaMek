using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Supplies terrain-specific movement costs to map pathfinding.
/// </summary>
public interface IMovementCostProvider
{
    /// <summary>
    /// Gets the additional movement cost for entering a terrain type.
    /// </summary>
    /// <param name="terrainType">The terrain being entered.</param>
    /// <param name="terrainHeight">The terrain height or depth, when applicable.</param>
    /// <returns>The additional movement points required by the terrain.</returns>
    int GetMovementCost(MakaMekTerrains terrainType, int terrainHeight);
}

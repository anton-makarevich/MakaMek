using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Provides the default terrain movement costs used by standalone maps.
/// </summary>
public sealed class DefaultMovementCostProvider : IMovementCostProvider
{
    /// <inheritdoc />
    public int GetMovementCost(MakaMekTerrains terrainType, int terrainHeight) => terrainType switch
    {
        MakaMekTerrains.Clear => 0,
        MakaMekTerrains.LightWoods => 1,
        MakaMekTerrains.HeavyWoods => 2,
        MakaMekTerrains.Rough => 1,
        MakaMekTerrains.Water => terrainHeight switch
        {
            0 => 0,
            -1 => 1,
            _ => 3
        },
        MakaMekTerrains.Road => 0,
        MakaMekTerrains.Pavement => 0,
        MakaMekTerrains.Bridge => 0,
        MakaMekTerrains.Rubble => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(terrainType), terrainType, "Unknown terrain type.")
    };
}

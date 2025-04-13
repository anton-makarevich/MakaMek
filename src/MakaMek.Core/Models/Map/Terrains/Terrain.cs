namespace Sanet.MakaMek.Core.Models.Map.Terrains;

/// <summary>
/// Base class for all terrain types
/// </summary>
public abstract class Terrain
{
    /// <summary>
    /// Unique identifier for this terrain type
    /// </summary>
    public abstract MakaMekTerrains Id { get; }

    /// <summary>
    /// Fixed height of this terrain type
    /// </summary>
    public abstract int Height { get; }

    /// <summary>
    /// Factor that affects line of sight when this terrain is between attacker and target.
    /// The line of sight is blocked when the sum of intervening factors along the line is 3 or more.
    /// Hexes containing attacker and target are not counted.
    /// </summary>
    public abstract int InterveningFactor { get; }

    /// <summary>
    /// Movement cost modifier for this terrain
    /// </summary>
    public abstract int MovementCost { get; }

    public static Terrain GetTerrainType(MakaMekTerrains terrainType)
    {
        return terrainType switch
        {
            MakaMekTerrains.Clear => new ClearTerrain(),
            MakaMekTerrains.LightWoods => new LightWoodsTerrain(),
            MakaMekTerrains.HeavyWoods => new HeavyWoodsTerrain(),
            _ => throw new ArgumentException($"Unknown terrain type: {terrainType}")
        };
    }
}
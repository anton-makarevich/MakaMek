using Sanet.MakaMek.Map.Data;

namespace Sanet.MakaMek.Map.Models.Terrains;

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
    /// Height of this terrain type
    /// </summary>
    public abstract int Height { get; }

    /// <summary>
    /// Factor that affects the line of sight when this terrain is between attacker and target.
    /// The line of sight is blocked when the sum of intervening factors along the line is 3 or more.
    /// Hexes containing attacker and target are not counted.
    /// </summary>
    public abstract int InterveningFactor { get; }

    /// <summary>
    /// Movement cost modifier for this terrain
    /// </summary>
    public abstract int MovementCost { get; }

    /// <summary>
    /// Converts this terrain to a serializable data transfer object.
    /// Default implementation returns data with just the terrain type (no height).
    /// Override for terrains with variable properties like water depth.
    /// </summary>
    public virtual TerrainData ToData()
    {
        return new TerrainData
        {
            Type = Id,
            Height = null
        };
    }

    /// <summary>
    /// Creates a terrain instance from serialized data.
    /// Delegates to CreateTerrainOfType with the height and constructionFactor values from the data.
    /// </summary>
    public static Terrain FromData(TerrainData data)
    {
        return CreateTerrainOfType(data.Type, data.Height, data.ConstructionFactor);
    }

    /// <summary>
    /// Creates a terrain instance by type.
    /// For terrains with variable properties (like water depth or bridge height), the height parameter is used.
    /// For structural terrains (like bridges), the constructionFactor parameter is used.
    /// For all other terrains, these parameters are ignored.
    /// </summary>
    public static Terrain CreateTerrainOfType(MakaMekTerrains terrainType, int? height = null, int? constructionFactor = null)
    {
        return terrainType switch
        {
            MakaMekTerrains.Clear => new ClearTerrain(),
            MakaMekTerrains.LightWoods => new LightWoodsTerrain(),
            MakaMekTerrains.HeavyWoods => new HeavyWoodsTerrain(),
            MakaMekTerrains.Rough => new RoughTerrain(),
            MakaMekTerrains.Water => new WaterTerrain(height ?? 0),
            MakaMekTerrains.Road => new RoadTerrain(),
            MakaMekTerrains.Pavement => new PavementTerrain(),
            MakaMekTerrains.Bridge => new BridgeTerrain(height ?? 0, constructionFactor ?? 0),
            _ => throw new ArgumentException($"Unknown terrain type: {terrainType}")
        };
    }
}
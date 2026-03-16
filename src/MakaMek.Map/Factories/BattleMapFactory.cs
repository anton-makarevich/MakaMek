using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Factories;

/// <summary>
/// Factory for creating battle maps with different strategies
/// </summary>
public class BattleMapFactory : IBattleMapFactory
{
    /// <summary>
    /// Generate a rectangular map with the specified terrain generator
    /// </summary>
    public BattleMap GenerateMap(int width, int height, ITerrainGenerator generator)
    {
        var map = new BattleMap(width, height);

        for (var q = 1; q < width+1; q++)
        {
            for (var r = 1; r < height+1; r++)
            {
                var coordinates = new HexCoordinates(q, r);
                var hex = generator.Generate(coordinates);
                map.AddHex(hex);
            }
        }

        return map;
    }

    /// <summary>
    /// Create a battle map from existing map data (including biome)
    /// </summary>
    public BattleMap CreateFromData(BattleMapData mapData)
    {
        if (mapData.HexData.Count == 0)
        {
            return new BattleMap(0, 0, mapData.Biome);
        }

        var map = new BattleMap(
            mapData.HexData.Max(h => h.Coordinates.Q),
            mapData.HexData.Max(h => h.Coordinates.R),
            mapData.Biome);

        foreach (var hex in mapData.HexData)
        {
            var newHex = new Hex(new HexCoordinates(hex.Coordinates), hex.Level);
            foreach (var terrainType in hex.TerrainTypes)
            {
                // Map terrain type strings to terrain classes
                var terrain = Terrain.GetTerrainType(terrainType);
                newHex.AddTerrain(terrain);
            }
            map.AddHex(newHex);
        }

        return map;
    }
}

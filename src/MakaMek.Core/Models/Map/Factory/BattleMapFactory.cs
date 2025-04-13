using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Utils.Generators;

namespace Sanet.MakaMek.Core.Models.Map.Factory;

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
    /// Create a battle map from existing hex data
    /// </summary>
    public BattleMap CreateFromData(IList<HexData> hexData)
    {
        var map = new BattleMap(
            hexData.Max(h => h.Coordinates.Q) + 1,
            hexData.Max(h => h.Coordinates.R) + 1);
        
        foreach (var hex in hexData)
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

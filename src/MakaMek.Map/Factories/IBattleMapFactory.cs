using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Factories;

/// <summary>
/// Interface for creating battle maps with different strategies
/// </summary>
public interface IBattleMapFactory
{
    /// <summary>
    /// Generate a rectangular map with the specified terrain generator
    /// </summary>
    BattleMap GenerateMap(int width, int height, ITerrainGenerator generator);

    /// <summary>
    /// Create a battle map from existing hex data
    /// </summary>
    BattleMap CreateFromData(IList<HexData> hexData);
}

using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Utils.Generators;

namespace Sanet.MakaMek.Core.Models.Map.Factory;

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

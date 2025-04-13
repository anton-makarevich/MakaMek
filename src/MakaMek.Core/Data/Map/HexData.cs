using Sanet.MakaMek.Core.Models.Map.Terrains;

namespace Sanet.MakaMek.Core.Data.Map
{
    public record HexData
    {
        public required HexCoordinateData Coordinates { get; init; }
        public required MakaMekTerrains[] TerrainTypes { get; init; }
        public int Level { get; init; }
    }
}

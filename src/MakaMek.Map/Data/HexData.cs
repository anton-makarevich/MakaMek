using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Data
{
    public record HexData
    {
        public required HexCoordinateData Coordinates { get; init; }
        public required MakaMekTerrains[] TerrainTypes { get; init; }
        public int Level { get; init; }
    }
}

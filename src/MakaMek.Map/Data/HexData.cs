namespace Sanet.MakaMek.Map.Data
{
    public record HexData
    {
        public required HexCoordinateData Coordinates { get; init; }
        public required TerrainData[] Terrains { get; init; }
        public int Level { get; init; }
    }
}

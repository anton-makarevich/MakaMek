namespace Sanet.MakaMek.Map.Data
{
    public record HexPositionData
    {
        public required HexCoordinateData Coordinates { get; init; }
        public required int Facing { get; init; }
    }
}

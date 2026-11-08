namespace Sanet.MakaMek.Map.Data
{
    public record HexPositionData
    {
        public required HexCoordinateData Coordinates { get; init; }
        public required int Facing { get; init; }
        public int Surface { get; init; } // HexSurface.Ground
        /// <summary>
        /// Unit base level while airborne; absent/null for grounded positions. Optional so legacy
        /// payloads without the field still deserialize.
        /// </summary>
        public int? AirborneLevel { get; init; }
    }
}

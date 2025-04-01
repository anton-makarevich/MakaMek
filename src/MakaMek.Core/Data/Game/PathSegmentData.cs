using Sanet.MakaMek.Core.Data.Map;

namespace Sanet.MakaMek.Core.Data.Game
{
    public record PathSegmentData
    {
        public required HexPositionData From { get; init; }
        public required HexPositionData To { get; init; }
        public required int Cost { get; init; }
    }
}

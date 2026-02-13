using Sanet.MakaMek.Core.Models.Map;

namespace Sanet.MakaMek.Presentation.Models.Map;

public static class HexCoordinatesPresentationExtensions
{
    public const double HexWidth = 100;
    public const double HexHeight = 86.6;
    private const double HexHorizontalSpacing = HexWidth * 0.75;
    extension(HexCoordinates coordinates)
    {
        /// <summary>
        /// Gets the X coordinate in pixels for rendering
        /// </summary>
        public double H => coordinates.Q * HexHorizontalSpacing;
        
        /// <summary>
        /// Gets the Y coordinate in pixels for rendering
        /// </summary>
        public double V => coordinates.R * HexHeight - (coordinates.Q % 2 == 0 ? 0 : HexHeight * 0.5);
    }
}
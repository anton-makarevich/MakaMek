namespace Sanet.MakaMek.Core.Models.Map;

public enum HexDirection
{
    Top = 0,         
    TopRight = 1,    
    BottomRight = 2, 
    Bottom = 3,      
    BottomLeft = 4, 
    TopLeft = 5 
}

public static class HexDirectionExtensions
{
    /// <summary>
    /// Gets all hex directions as an array
    /// </summary>
    public static HexDirection[] AllDirections { get; } = 
    [
        HexDirection.Top,
        HexDirection.TopRight,
        HexDirection.BottomRight,
        HexDirection.Bottom,
        HexDirection.BottomLeft,
        HexDirection.TopLeft
    ];

    /// <param name="direction">The current direction</param>
    extension(HexDirection direction)
    {
        public HexDirection GetOppositeDirection() =>
            (HexDirection)((int)(direction + 3) % 6);

        /// <summary>
        /// Rotates a direction by the specified number of hexsides
        /// </summary>
        /// <param name="hexsides">The number of hexsides to rotate (positive for clockwise, negative for counter-clockwise)</param>
        /// <returns>The new direction after rotation</returns>
        public HexDirection Rotate(int hexsides) =>
            (HexDirection)(((int)direction + hexsides + 6) % 6);
    }
}

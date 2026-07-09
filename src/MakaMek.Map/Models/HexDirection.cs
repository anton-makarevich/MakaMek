namespace Sanet.MakaMek.Map.Models;

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

        /// <summary>
        /// Calculates the minimum number of hexsides between two directions
        /// </summary>
        /// <param name="target">The target direction</param>
        /// <returns>The minimum number of hexsides to rotate (0-3)</returns>
        public int ShortestRotationTo(HexDirection target)
        {
            var diff = Math.Abs((int)target - (int)direction);
            return Math.Min(diff, 6 - diff);
        }

        /// <summary>
        /// Gets the ordered pair of hex polygon corner indices that form this edge.
        /// The indices match the clockwise winding used by HexagonGeometry.GetCorners().
        /// </summary>
        /// <returns>The start and end corner indices for the edge.</returns>
        public (int StartIndex, int EndIndex) GetHexPointEdgeCornerIndices()
        {
            var directionIndex = (int)direction;
            return ((5 - directionIndex + 6) % 6, (4 - directionIndex + 6) % 6);
        }
    }
}

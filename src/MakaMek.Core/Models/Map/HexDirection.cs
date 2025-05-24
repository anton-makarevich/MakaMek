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
    public static HexDirection GetOppositeDirection(this HexDirection direction) =>
        (HexDirection)((int)(direction + 3) % 6);
        
    /// <summary>
    /// Rotates a direction by the specified number of hexsides
    /// </summary>
    /// <param name="direction">The current direction</param>
    /// <param name="hexsides">The number of hexsides to rotate (positive for clockwise, negative for counter-clockwise)</param>
    /// <returns>The new direction after rotation</returns>
    public static HexDirection Rotate(this HexDirection direction, int hexsides) =>
        (HexDirection)(((int)direction + hexsides + 6) % 6);
}

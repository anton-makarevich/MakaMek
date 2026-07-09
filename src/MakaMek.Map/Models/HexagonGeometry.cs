namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Framework-neutral flat-top hexagon vertex geometry.
/// Returns the six corner coordinates (in local hex space) matching the clockwise
/// winding used by <see cref="HexDirectionExtensions.GetHexPointEdgeCornerIndices"/>:
/// 0=Left, 1=BottomLeft, 2=BottomRight, 3=Right, 4=TopRight, 5=TopLeft.
/// </summary>
public static class HexagonGeometry
{
    /// <summary>
    /// Returns the six corner offsets for a flat-top hex, in clockwise order
    /// starting from the left vertex. Each tuple is (x, y) relative to the
    /// hex's top-left origin.
    /// </summary>
    public static (double X, double Y)[] GetCorners()
    {
        var w = HexCoordinatesPixelExtensions.HexWidth;
        var h = HexCoordinatesPixelExtensions.HexHeight;

        return
        [
            (0, h * 0.5),
            (w * 0.25, h),
            (w * 0.75, h),
            (w, h * 0.5),
            (w * 0.75, 0),
            (w * 0.25, 0)
        ];
    }
}

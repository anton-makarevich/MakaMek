namespace Sanet.MakaMek.Map.Models;

public static class HexCoordinatesPixelExtensions
{
    public const double HexWidth = 100;
    public const double HexHeight = 86.60254037844386; // 100 * Math.Sin(Math.PI / 3.0)
    private const double HexHorizontalSpacing = HexWidth * 0.75;

    private enum PointRelationship
    {
        Left = 1,
        Colinear = 0,
        Right = -1
    }

    private static PointRelationship GetGeometricRelationship(double lineStartX, double lineStartY, double lineEndX, double lineEndY, double pointX, double pointY)
    {
        var cross = (lineEndX - lineStartX) * (pointY - lineStartY) - (pointX - lineStartX) * (lineEndY - lineStartY);
        return cross switch
        {
            > 0.0001 => PointRelationship.Left,
            < -0.0001 => PointRelationship.Right,
            _ => PointRelationship.Colinear
        };
    }

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

        /// <summary>
        /// Determines whether a line segment from lineStart to lineEnd geometrically intersects this hex.
        /// Evaluates using reliable cross-products.
        /// </summary>
        public bool IsIntersectedBy(HexCoordinates lineStart, HexCoordinates lineEnd)
        {
            var x0 = lineStart.H;
            var y0 = lineStart.V;
            var x1 = lineEnd.H;
            var y1 = lineEnd.V;

            var cx = coordinates.H;
            var cy = coordinates.V;
            
            const double size = HexWidth / 2.0;
            const double halfHeight = HexHeight / 2.0;

            // Flat-topped hex corners
            (double x, double y)[] points =
            [
                (cx - size, cy),
                (cx - size / 2, cy - halfHeight),
                (cx + size / 2, cy - halfHeight),
                (cx + size, cy),
                (cx + size / 2, cy + halfHeight),
                (cx - size / 2, cy + halfHeight)
            ];

            var side0 = GetGeometricRelationship(x0, y0, x1, y1, points[0].x, points[0].y);
            if (side0 == PointRelationship.Colinear) return true;

            for (var i = 1; i < 6; i++)
            {
                var sideI = GetGeometricRelationship(x0, y0, x1, y1, points[i].x, points[i].y);
                if (sideI == PointRelationship.Colinear || sideI != side0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
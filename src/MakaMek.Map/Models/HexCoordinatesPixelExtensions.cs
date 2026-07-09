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

    private static PointRelationship GetGeometricRelationship(double lineStartX, double lineStartY, double lineEndX,
        double lineEndY, double pointX, double pointY)
    {
        var dx = lineEndX - lineStartX;
        var dy = lineEndY - lineStartY;
        var cross = dx * (pointY - lineStartY) - (pointX - lineStartX) * dy;
        var signedDistance = (dx == 0 && dy == 0) ? 0 : cross / Math.Sqrt(dx * dx + dy * dy);
        return signedDistance switch
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
            
            var size = HexWidth / 2.0;
            var halfHeight = HexHeight / 2.0;

            // Bounding box check for the finite line segment
            var minX = Math.Min(x0, x1);
            var maxX = Math.Max(x0, x1);
            var minY = Math.Min(y0, y1);
            var maxY = Math.Max(y0, y1);

            var hexMinX = cx - size;
            var hexMaxX = cx + size;
            var hexMinY = cy - halfHeight;
            var hexMaxY = cy + halfHeight;

            if (maxX < hexMinX - 0.1 || minX > hexMaxX + 0.1 || maxY < hexMinY - 0.1 || minY > hexMaxY + 0.1)
            {
                return false;
            }

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

    /// <summary>
    /// Converts pixel/content-space coordinates back to the nearest hex coordinates.
    /// Pixel origin (0,0) is the top-left corner of hex (0,0)'s bounding box.
    /// The hex center is at (HexWidth/2, HexHeight/2), so we offset before inverting.
    /// </summary>
    public static HexCoordinates FromPixel(double x, double y)
    {
        const double size = HexWidth / 2.0;

        // Shift so (0,0) maps to the hex center instead of top-left corner
        var px = x - HexWidth / 2.0;
        var py = y - HexHeight / 2.0;

        // Convert pixel to standard flat-top axial coordinates
        var q = px * 2.0 / 3.0 / size;
        var r = (-px / 3.0 + Math.Sqrt(3) / 3.0 * py) / size;

        // Axial to cube
        var cy = -q - r;

        // Cube rounding
        var rx = Math.Round(q, MidpointRounding.AwayFromZero);
        var ry = Math.Round(cy, MidpointRounding.AwayFromZero);
        var rz = Math.Round(r, MidpointRounding.AwayFromZero);

        var xDiff = Math.Abs(rx - q);
        var yDiff = Math.Abs(ry - cy);
        var zDiff = Math.Abs(rz - r);

        if (xDiff > yDiff && xDiff > zDiff)
            rx = -ry - rz;

        // Rounded cube to axial
        var axialQ = (int)rx;
        var axialR = (int)rz;

        // Axial to odd-r offset: R_offset = r_axial + (q + (q & 1)) / 2
        var offsetR = axialR + (axialQ + (axialQ & 1)) / 2;

        return new HexCoordinates(axialQ, offsetR);
    }
}
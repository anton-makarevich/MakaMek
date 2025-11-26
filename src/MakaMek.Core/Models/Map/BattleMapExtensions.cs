namespace Sanet.MakaMek.Core.Models.Map;

/// <summary>
/// Extension methods for BattleMap
/// </summary>
public static class BattleMapExtensions
{
    /// <param name="map">The battle map</param>
    extension(IBattleMap map)
    {
        /// <summary>
        /// Gets all hex coordinates that are on the edge/border of the map
        /// </summary>
        /// <returns>List of hex coordinates on the map's edge</returns>
        public List<HexCoordinates> GetEdgeHexCoordinates()
        {
            var edgeHexes = new List<HexCoordinates>();

            var width = map.Width;
            var height = map.Height;

            // Add first row
            for (var q = 1; q <= width; q++)
            {
                edgeHexes.Add(new HexCoordinates(q, 1));
            }

            // Add last row (only if different from first row)
            if (height > 1)
            {
                for (var q = 1; q <= width; q++)
                {
                    edgeHexes.Add(new HexCoordinates(q, height));
                }
            }

            // Add first and last columns (excluding corners already added)
            for (var r = 2; r < height; r++)
            {
                edgeHexes.Add(new HexCoordinates(1, r));
                if (width > 1)
                {
                    edgeHexes.Add(new HexCoordinates(width, r));
                }
            }

            return edgeHexes;
        }

        /// <summary>
        /// Gets the hex coordinate of the center of the map
        /// </summary>
        /// <returns>The center hex coordinate</returns>
        public HexCoordinates GetCenterHexCoordinate()
        {
            var centerQ = (map.Width + 1) / 2;
            var centerR = (map.Height + 1) / 2;
            return new HexCoordinates(centerQ, centerR);
        }
    }
}

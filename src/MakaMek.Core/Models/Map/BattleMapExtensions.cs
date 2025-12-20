using Sanet.MakaMek.Core.Models.Units;

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

        /// <summary>
        /// Gets all reachable positions for a unit with a given movement type.
        /// Returns positions with all valid facing directions.
        /// </summary>
        /// <param name="unit">The unit to calculate reachable positions for</param>
        /// <param name="movementType">The movement type to use</param>
        /// <param name="prohibitedHexes">Hexes that cannot be entered (occupied by other units)</param>
        /// <returns>List of reachable positions with all valid facings and their movement costs</returns>
        public List<(HexPosition position, int cost)> GetReachablePositions(
            IUnit unit,
            MovementType movementType,
            IList<HexCoordinates> prohibitedHexes)
        {
            if (unit.Position == null)
                return [];

            var movementPoints = unit.GetMovementPoints(movementType);
            if (movementPoints <= 0)
                return [];

            var results = new List<(HexPosition position, int cost)>();

            if (movementType == MovementType.Jump)
            {
                // For jumping, get all reachable hexes and add all possible facings
                var reachableHexes = map.GetJumpReachableHexes(
                    unit.Position.Coordinates,
                    movementPoints,
                    prohibitedHexes);

                foreach (var hex in reachableHexes)
                {
                    var distance = unit.Position.Coordinates.DistanceTo(hex);
                    foreach (var facing in HexDirectionExtensions.AllDirections)
                    {
                        results.Add((new HexPosition(hex, facing), distance));
                    }
                }
            }
            else
            {
                // For walk/run, get forward reachable hexes
                var forwardReachable = map.GetReachableHexes(
                    unit.Position,
                    movementPoints,
                    prohibitedHexes);

                foreach (var (coordinates, cost) in forwardReachable)
                {
                    foreach (var facing in HexDirectionExtensions.AllDirections)
                    {
                        results.Add((new HexPosition(coordinates, facing), cost));
                    }
                }

                // Add backward reachable hexes if unit can move backward
                if (unit.CanMoveBackward(movementType))
                {
                    var oppositePosition = unit.Position.GetOppositeDirectionPosition();
                    var backwardReachable = map.GetReachableHexes(
                        oppositePosition,
                        movementPoints,
                        prohibitedHexes);
                    
                    // Create a set of existing positions for O(1) lookup
                    var existingPositions = new HashSet<HexPosition>(results.Select(r => r.position));

                    foreach (var (coordinates, cost) in backwardReachable)
                    {
                        foreach (var facing in HexDirectionExtensions.AllDirections)
                        {
                            // Swap facing to account for backward movement
                            var adjustedFacing = facing.GetOppositeDirection();
                            var position = new HexPosition(coordinates, adjustedFacing);
                            
                            // Only add if not already present from forward movement
                            if (existingPositions.Add(position))
                            {
                                results.Add((position, cost));
                            }
                        }
                    }
                }
            }

            return results;
        }
    }
}

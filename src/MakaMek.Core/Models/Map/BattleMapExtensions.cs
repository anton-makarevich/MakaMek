using Sanet.MakaMek.Core.Data.Map;
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

            // Add the first row
            for (var q = 1; q <= width; q++)
            {
                edgeHexes.Add(new HexCoordinates(q, 1));
            }

            // Add the last row (only if different from the first row)
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
        /// Gets all reachable coordinates for a unit with a given movement type.
        /// </summary>
        /// <param name="unit">The unit to calculate reachable coordinates for</param>
        /// <param name="movementType">The movement type to use</param>
        /// <param name="prohibitedHexes">Hexes that cannot be entered or passed through (e.g., occupied by enemy units)</param>
        /// <param name="friendlyUnitsCoordinates">Hexes occupied by friendly units (unit can pass but not stop there)</param>
        /// <returns>List of coordinates that could be reached by a unit using a specified movement type</returns>
        public UnitReachabilityData GetReachableHexesForUnit(
            IUnit unit,
            MovementType movementType,
            IReadOnlySet<HexCoordinates> prohibitedHexes,
            IReadOnlySet<HexCoordinates> friendlyUnitsCoordinates)
        {
            if (unit.Position == null)
                return new UnitReachabilityData([], []);

            var movementPoints = unit.GetMovementPoints(movementType);
            var canMoveBackward = unit.CanMoveBackward(movementType);
            return map.GetReachableHexesForPosition(unit.Position,
                movementPoints,
                canMoveBackward,
                movementType,
                prohibitedHexes,
                friendlyUnitsCoordinates);
        }

        /// <summary>
        /// Gets all reachable coordinates for a unit with a given movement type.
        /// </summary>
        /// <param name="position">Position to calculate reachable hexes from</param>
        /// <param name="movementPoints">Movement points available</param>
        /// <param name="canMoveBackward">If the unit can move backward</param>
        /// <param name="movementType">The movement type to use</param>
        /// <param name="prohibitedHexes">Hexes that cannot be entered or passed through (e.g., occupied by enemy units)</param>
        /// <param name="friendlyUnitsCoordinates">Hexes occupied by friendly units (unit can pass but not stop there)</param>
        /// <returns>List of coordinates that could be reached by a unit using a specified movement type</returns>
        public UnitReachabilityData GetReachableHexesForPosition(
            HexPosition position,
            int movementPoints,
            bool canMoveBackward,
            MovementType movementType,
            IReadOnlySet<HexCoordinates> prohibitedHexes,
            IReadOnlySet<HexCoordinates> friendlyUnitsCoordinates)
        {
            if (movementPoints <= 0)
                return new UnitReachabilityData([], []);

            if (movementType == MovementType.Jump)
            {
                // For jumping, we use the simplified method that ignores terrain and facing
                var reachableHexes = map
                    .GetJumpReachableHexes(
                        position.Coordinates,
                        movementPoints,
                        prohibitedHexes)
                    .Where(hex => !friendlyUnitsCoordinates.Contains(hex))
                    .ToList();

                // For jumping, there's no forward/backward distinction
                return new UnitReachabilityData(reachableHexes, []);
            }

            // Get forward reachable hexes
            var forwardReachableHexes = map
                .GetReachableHexes(position, movementPoints, prohibitedHexes)
                .Select(x => x.coordinates)
                .Where(hex => !friendlyUnitsCoordinates.Contains(hex))
                .ToList();
            
            if (movementType == MovementType.Walk)
                forwardReachableHexes.Add(position.Coordinates);

            // Get backward reachable hexes if the unit can move backward
            if (!canMoveBackward)
                return new UnitReachabilityData(forwardReachableHexes, []);
            var oppositePosition = position.GetOppositeDirectionPosition();
            var backwardReachableHexes = map
                .GetReachableHexes(oppositePosition, movementPoints, prohibitedHexes)
                .Select(x => x.coordinates)
                .Where(hex => !friendlyUnitsCoordinates.Contains(hex))
                .ToList();

            return new UnitReachabilityData(forwardReachableHexes, backwardReachableHexes);
        }


        /// <summary>
        /// Finds all possible paths from a start position to a target hex, considering all possible facing directions.
        /// </summary>
        /// <param name="startPosition">The starting position with facing</param>
        /// <param name="targetHex">The target hex coordinates</param>
        /// <param name="movementType">The type of movement (Jump, Walk, Run)</param>
        /// <param name="movementPoints">Available movement points</param>
        /// <param name="reachabilityData">Reachability data containing forward and backward reachable hexes</param>
        /// <param name="prohibitedHexes">Hexes that cannot be entered or passed through</param>
        /// <returns>Dictionary mapping each valid facing direction to the path that reaches that facing</returns>
        public Dictionary<HexDirection, MovementPath> GetPathsToHexWithAllFacings(
            HexPosition startPosition,
            HexCoordinates targetHex,
            MovementType movementType,
            int movementPoints,
            UnitReachabilityData reachabilityData,
            IReadOnlySet<HexCoordinates>? prohibitedHexes = null)
        {
            var possibleDirections = new Dictionary<HexDirection, MovementPath>();
            var isForwardReachable = reachabilityData.IsForwardReachable(targetHex);
            var isBackwardReachable = reachabilityData.IsBackwardReachable(targetHex);
    
            foreach (var direction in HexDirectionExtensions.AllDirections)
            {
                var targetPos = new HexPosition(targetHex, direction);
                MovementPath? path = null;

                // Try forward movement (or Jump, which ignores reachability)
                if (movementType == MovementType.Jump || isForwardReachable)
                {
                    path = map.FindPath(startPosition, targetPos, movementType, movementPoints, prohibitedHexes);
                }

                // Try backward movement for Walk only
                if (path == null && movementType == MovementType.Walk && isBackwardReachable)
                {
                    var oppositeStartPos = startPosition.GetOppositeDirectionPosition();
                    var oppositeTargetPos = targetPos.GetOppositeDirectionPosition();
            
                    path = map.FindPath(oppositeStartPos, oppositeTargetPos, movementType, movementPoints, prohibitedHexes)
                        ?.ReverseFacing();
                }

                if (path != null)
                {
                    possibleDirections[direction] = path;
                }
            }

            return possibleDirections;
        }
    }
}

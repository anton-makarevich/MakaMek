using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Exceptions;

namespace Sanet.MakaMek.Core.Models.Map;

/// <summary>
/// Represents the game battle map, managing hexes and providing pathfinding capabilities
/// </summary>
public class BattleMap(int width, int height) : IBattleMap
{
    private readonly Dictionary<HexCoordinates, Hex> _hexes = new();
    private readonly LineOfSightCache _losCache = new();

    public int Width { get; } = width;
    public int Height { get; } = height;

    /// <summary>
    /// Adds a hex to the map. Throws HexOutsideOfMapBoundariesException if hex coordinates are outside map boundaries
    /// </summary>
    public void AddHex(Hex hex)
    {
        if (hex.Coordinates.Q < 1 || hex.Coordinates.Q >= Width +1 ||
            hex.Coordinates.R < 1 || hex.Coordinates.R >= Height +1)
        {
            throw new HexOutsideOfMapBoundariesException(hex.Coordinates, Width, Height);
        }
        
        _hexes[hex.Coordinates] = hex;
    }

    public Hex? GetHex(HexCoordinates coordinates)
    {
        return _hexes.GetValueOrDefault(coordinates);
    }

    /// <summary>
    /// Finds a path between two positions, considering facing direction and movement costs
    /// </summary>
    public List<PathSegment>? FindPath(HexPosition start, HexPosition target, int maxMovementPoints, IEnumerable<HexCoordinates>? prohibitedHexes = null)
    {
        // If start and target are in the same hex, just return turning segments
        if (start.Coordinates == target.Coordinates)
        {
            var turningSteps = start.GetTurningSteps(target.Facing).ToList();
            if (turningSteps.Count > maxMovementPoints)
                return null;

            var segments = new List<PathSegment>();
            var currentPos = start;
            foreach (var step in turningSteps)
            {
                segments.Add(new PathSegment(currentPos, step, 1)); // Cost 1 for each turn
                currentPos = step;
            }
            return segments;
        }

        var frontier = new PriorityQueue<(HexPosition pos, List<HexPosition> path, int cost), int>();
        var visited = new Dictionary<(HexCoordinates coords, HexDirection facing), int>();
        var prohibited = prohibitedHexes?.ToHashSet() ?? [];
        
        frontier.Enqueue((start, [start], 0), 0);
        visited[(start.Coordinates, start.Facing)] = 0;

        while (frontier.Count > 0)
        {
            var (current, path, currentCost) = frontier.Dequeue();
            
            // Check if we've reached the target
            if (current.Coordinates == target.Coordinates && current.Facing == target.Facing)
            {
                // Convert path to segments
                var segments = new List<PathSegment>();
                for (var i = 0; i < path.Count - 1; i++)
                {
                    var from = path[i];
                    var to = path[i + 1];
                    var segmentCost = 1; // Default cost for turning

                    // If coordinates changed, it's a movement
                    if (from.Coordinates != to.Coordinates)
                    {
                        var hex = GetHex(to.Coordinates);
                        segmentCost = hex!.MovementCost;
                    }

                    segments.Add(new PathSegment(from, to, segmentCost));
                }
                return segments;
            }

            // For each adjacent hex
            foreach (var nextCoord in current.Coordinates.GetAdjacentCoordinates())
            {
                var hex = GetHex(nextCoord);
                if (hex == null || prohibited.Contains(nextCoord))
                    continue;

                // Get required facing for movement
                var requiredFacing = current.Coordinates.GetDirectionToNeighbour(nextCoord);
                
                // Calculate turning steps and cost if needed
                var turningSteps = current.GetTurningSteps(requiredFacing).ToList();
                var turningCost = turningSteps.Count;
                var newPath = new List<HexPosition>(path);
                newPath.AddRange(turningSteps);
                
                // Add the movement step
                var nextPos = new HexPosition(nextCoord, requiredFacing);
                newPath.Add(nextPos);
                
                // Calculate total cost including terrain
                var totalCost = currentCost + hex.MovementCost + turningCost;
                
                if (totalCost > maxMovementPoints)
                    continue;
                    
                // Skip if we've visited this state with a lower or equal cost
                var nextKey = (nextCoord, requiredFacing);
                if (visited.TryGetValue(nextKey, out var visitedCost) && totalCost >= visitedCost)
                    continue;
                
                visited[nextKey] = totalCost;
                
                // Calculate priority based on remaining distance plus current cost
                var priority = totalCost + nextCoord.DistanceTo(target.Coordinates);
                
                // If we're at target coordinates but wrong facing, add turning steps to target facing
                if (nextCoord == target.Coordinates && requiredFacing != target.Facing)
                {
                    var finalTurningSteps = nextPos.GetTurningSteps(target.Facing).ToList();
                    var finalCost = totalCost + finalTurningSteps.Count;
                    if (finalCost > maxMovementPoints) continue;
                    newPath.AddRange(finalTurningSteps);
                    frontier.Enqueue((new HexPosition(nextCoord, target.Facing), newPath, finalCost), priority);
                }
                else
                {
                    frontier.Enqueue((nextPos, newPath, totalCost), priority);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all valid hexes that can be reached with given movement points, considering facing
    /// </summary>
    public IEnumerable<(HexCoordinates coordinates, int cost)> GetReachableHexes(
        HexPosition start,
        int maxMovementPoints,
        IEnumerable<HexCoordinates>? prohibitedHexes = null)
    {
        var visited = new Dictionary<(HexCoordinates coords, HexDirection facing), int>();
        var bestCosts = new Dictionary<HexCoordinates, int>();
        var toVisit = new Queue<HexPosition>();
        var prohibited = prohibitedHexes?.ToHashSet() ?? [];
        
        visited[(start.Coordinates, start.Facing)] = 0;
        bestCosts[start.Coordinates] = 0;
        toVisit.Enqueue(start);

        while (toVisit.Count > 0)
        {
            var current = toVisit.Dequeue();
            var currentCost = visited[(current.Coordinates, current.Facing)];

            // For each adjacent hex
            foreach (var neighborCoord in current.Coordinates.GetAdjacentCoordinates())
            {
                // Skip if hex doesn't exist on map or is prohibited
                var neighborHex = GetHex(neighborCoord);
                if (neighborHex == null || prohibited.Contains(neighborCoord))
                    continue;

                // Get required facing to move to this hex
                var requiredFacing = current.Coordinates.GetDirectionToNeighbour(neighborCoord);
                
                // Calculate turning cost from current facing
                var turningCost = current.GetTurningCost(requiredFacing);
                
                // Calculate total cost including turning and movement
                var totalCost = currentCost + neighborHex.MovementCost + turningCost;
                
                if (totalCost > maxMovementPoints) // Exceeds movement points
                    continue;

                var neighborKey = (neighborCoord, requiredFacing);
                
                // Skip if we've visited this hex+facing combination with a lower cost
                if (visited.TryGetValue(neighborKey, out var visitedCost) && totalCost >= visitedCost)
                    continue;
                
                // Update both visited and best costs
                visited[neighborKey] = totalCost;
                if (!bestCosts.TryGetValue(neighborCoord, out var bestCost) || totalCost < bestCost)
                {
                    bestCosts[neighborCoord] = totalCost;
                }
                
                toVisit.Enqueue(new HexPosition(neighborCoord, requiredFacing));
            }
        }

        return bestCosts
            .Select(v => (v.Key, v.Value));
    }

    /// <summary>
    /// Gets all valid hexes that can be reached with jumping movement, where each hex costs 1 MP
    /// regardless of terrain or facing direction
    /// </summary>
    public IEnumerable<HexCoordinates> GetJumpReachableHexes(
        HexCoordinates start,
        int movementPoints,
        IEnumerable<HexCoordinates>? prohibitedHexes = null)
    {
        var prohibited = prohibitedHexes?.ToHashSet() ?? [];
        
        // Get all hexes within range using the existing method
        return start.GetCoordinatesInRange(movementPoints)
            .Where(coordinates =>
            {
                // Skip if hex doesn't exist on a map or is prohibited
                var hex = GetHex(coordinates);
                return hex != null && 
                       !prohibited.Contains(coordinates) &&
                       coordinates != start;
            });
    }

    /// <summary>
    /// Checks if there is line of sight between two hexes
    /// </summary>
    public bool HasLineOfSight(HexCoordinates from, HexCoordinates to)
    {
        if (!IsOnMap(from) || !IsOnMap(to))
            return false;

        // If same hex, always has LOS
        if (from == to)
            return true;

        var fromHex = GetHex(from);
        var toHex = GetHex(to);
        
        if (fromHex == null || toHex == null)
            return false;

        // Get all hexes along the line, resolving any divided line segments
        var hexLine = ResolveHexesAlongTheLine(from, to);

        // Remove first and last hex (attacker and target positions)
        hexLine = hexLine.Skip(1).SkipLast(1).ToList();

        if (!hexLine.Any())
            return true; // No intervening hexes

        var distance = 1;
        var totalDistance = hexLine.Count;
        foreach (var coordinates in hexLine)
        {
            var hex = GetHex(coordinates);
            if (hex == null)
                return false;

            // Calculate the minimum height needed at this distance to maintain LOS
            var requiredHeight = InterpolateHeight(
                fromHex.GetCeiling(),
                toHex.GetCeiling(),
                distance,
                totalDistance);

            // If the hex is higher than the line between start and end points, it blocks LOS
            if (hex.Level > requiredHeight)
                return false;

            distance++;
        }
        
        // Calculate total intervening factor, handling nulls properly
        var totalInterveningFactor = 0;
        foreach (var coordinates in hexLine)
        {
            var hex = GetHex(coordinates);
            if (hex == null) return false; //Hex doesn't exist on the map
            var hexFactor = hex.GetTerrains().Sum(t => t.InterveningFactor);
            totalInterveningFactor += hexFactor;

            // Early exit if we already know LOS is blocked
            if (totalInterveningFactor >= 3)
                return false;
        }

        return true;
    }

    private List<HexCoordinates> ResolveHexesAlongTheLine(HexCoordinates from, HexCoordinates to)
    {
        // Check cache first
        if (_losCache.TryGetPath(from, to, out var cachedPath))
        {
            return cachedPath!;
        }

        // Get all possible segments in the line of sight
        var segments = from.LineTo(to);
        
        // If no divided segments, just return main options
        if (segments.All(s => s.SecondOption == null))
        {
            var path = segments.Select(s => s.MainOption).ToList();
            _losCache.AddPath(from, to, path);
            return path;
        }

        // Calculate intervening factors only for the divided segments
        var dividedSegments = segments.Where(s => s.SecondOption != null).ToList();
        var mainOptionsFactor = dividedSegments
            .Sum(s => GetHex(s.MainOption)?.GetTerrains().Sum(t => t.InterveningFactor) ?? 0);

        var secondaryOptionsFactor = dividedSegments
            .Sum(s => GetHex(s.SecondOption!)?.GetTerrains().Sum(t => t.InterveningFactor) ?? 0);

        // Choose whether to use secondary options based on which gives better defense
        var useSecondaryOptions = secondaryOptionsFactor > mainOptionsFactor;

        // Build the final path using the chosen option for divided segments
        var resolvedPath = segments.Select(s => 
            dividedSegments.Contains(s) && useSecondaryOptions 
                ? s.SecondOption! 
                : s.MainOption).ToList();

        // Cache the resolved path
        _losCache.AddPath(from, to, resolvedPath);

        return resolvedPath;
    }

    /// <summary>
    /// Interpolate height between two points for LOS calculation
    /// </summary>
    private static int InterpolateHeight(int startHeight, int endHeight, int currentDistance, int totalDistance)
    {
        if (totalDistance == 0)
            return startHeight;

        var t = (double)currentDistance / totalDistance;
        return (int)Math.Round(startHeight + (endHeight - startHeight) * t);
    }

    public IEnumerable<Hex> GetHexes()
    {
        return _hexes.Values;
    }

    /// <summary>
    /// Converts the battle map to a list of hex data objects
    /// </summary>
    /// <returns>List of hex data objects representing the map</returns>
    public List<HexData> ToData()
    {
        return GetHexes().Select(hex => hex.ToData()).ToList();
    }

    public List<PathSegment>? FindJumpPath(HexPosition from, HexPosition to, int movementPoints)
    {
        if (!IsOnMap(from.Coordinates) || !IsOnMap(to.Coordinates))
            return null;

        var distance = from.Coordinates.DistanceTo(to.Coordinates);
        if (distance > movementPoints)
            return null;

        // For jumping, we want the shortest path ignoring terrain and turning costs
        var path = new List<PathSegment>();
        var currentPosition = from;
        var remainingDistance = distance;

        while (remainingDistance > 0)
        {
            // Find the next hex in the direction of the target
            var neighbors = currentPosition.Coordinates.GetAdjacentCoordinates()
                .Where(IsOnMap)
                .ToList();

            

            // Add path segment with cost 1 (each hex costs 1 MP for jumping)
            HexPosition nextPosition;
            if (remainingDistance == 1)
                nextPosition = to;
            else
            {
                // Get the neighbor that's closest to the target
                            var nextCoords = neighbors
                                .OrderBy(n => n.DistanceTo(to.Coordinates))
                                .First();
                nextPosition = new HexPosition(nextCoords,
                                    currentPosition.Coordinates.GetDirectionToNeighbour(nextCoords));
            }
            
            path.Add(new PathSegment(
                currentPosition,
                nextPosition,
                1));

            currentPosition = nextPosition;
            remainingDistance--;
        }

        return path;
    }

    /// <summary>
    /// Gets hexes along the line of sight between two coordinates, including terrain information
    /// </summary>
    public IReadOnlyList<Hex> GetHexesAlongLineOfSight(HexCoordinates from, HexCoordinates to)
    {
        var coordinates = ResolveHexesAlongTheLine(from, to);
        return coordinates.Select(c => GetHex(c)!).ToList();
    }

    /// <summary>
    /// Clears the line of sight cache. Should be called at the end of each turn.
    /// </summary>
    public void ClearLosCache()
    {
        _losCache.Clear();
    }

    public bool IsOnMap(HexCoordinates coordinates)
    {
        return coordinates.Q >= 1 && coordinates.Q <= Width &&
               coordinates.R >= 1 && coordinates.R <= Height;
    }
}

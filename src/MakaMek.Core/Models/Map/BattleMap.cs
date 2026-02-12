using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Exceptions;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Map;

/// <summary>
/// Represents the game battle map, managing hexes and providing pathfinding capabilities
/// </summary>
public class BattleMap(int width, int height) : IBattleMap
{
    private readonly Dictionary<HexCoordinates, Hex> _hexes = new();
    private readonly LineOfSightCache _losCache = new();
    private readonly MovementPathCache _movementPathCache = new();
    private readonly MovementPathCache _movementLongPathCache = new();

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
    public MovementPath? FindPath(HexPosition start,
        HexPosition target,
        MovementType movementType,
        int maxMovementPoints,
        IReadOnlySet<HexCoordinates>? prohibitedHexes = null,
        PathFindingMode pathFindingMode = PathFindingMode.Shortest)
    {
        if (movementType == MovementType.Jump)
        {
            return FindJumpPath(start, target, maxMovementPoints);
        }

        return pathFindingMode == PathFindingMode.Shortest
            ? FindShortestPath(start, target, movementType, maxMovementPoints, prohibitedHexes)
            : FindLongestPath(start, target, movementType, maxMovementPoints, prohibitedHexes);
    }

    /// <summary>
    /// Creates a turning path when start and target are in the same hex
    /// </summary>
    private static MovementPath? CreateTurningPath(HexPosition start, HexPosition target, int maxMovementPoints, MovementType movementType)
    {
        var turningSteps = start.GetTurningSteps(target.Facing).ToList();
        if (turningSteps.Count > maxMovementPoints)
            return null;

        var segments = new List<PathSegment>();

        if (turningSteps.Count == 0)
        {
            segments.Add(new PathSegment(start, target, 0));
        }
        else
        {
            var currentPos = start;
            foreach (var step in turningSteps)
            {
                segments.Add(new PathSegment(currentPos, step, 1)); // Cost 1 for each turn
                currentPos = step;
            }
        }

        return new MovementPath(segments, movementType);
    }

    /// <summary>
    /// Converts a list of hex positions to path segments
    /// </summary>
    private List<PathSegment> ConvertPathToSegments(List<HexPosition> path)
    {
        var segments = new List<PathSegment>();
        for (var i = 0; i < path.Count - 1; i++)
        {
            var from = path[i];
            var to = path[i + 1];
            var segmentCost = 1; // Default cost for turning

            // If coordinates changed, it's a movement
            if (from.Coordinates != to.Coordinates)
            {
                var hex = GetHex(to.Coordinates) ?? throw new WrongHexException(to.Coordinates, "Hex not found");
                segmentCost = hex.MovementCost;
            }

            segments.Add(new PathSegment(from, to, segmentCost));
        }
        return segments;
    }

    /// <summary>
    /// Finds the shortest path between two positions (original behavior)
    /// </summary>
    private MovementPath? FindShortestPath(HexPosition start,
        HexPosition target,
        MovementType movementType,
        int maxMovementPoints,
        IReadOnlySet<HexCoordinates>? prohibitedHexes)
    {
        prohibitedHexes??= new HashSet<HexCoordinates>();
        var useCache = prohibitedHexes.Count == 0;

        if (useCache)
        {
            var cachedPath = _movementPathCache.Get(start, target, false);
            if (cachedPath != null)
            {
                return cachedPath.TotalCost <= maxMovementPoints ? cachedPath : null;
            }
        }

        // If start and target are in the same hex, just return turning segments
        if (start.Coordinates == target.Coordinates)
        {
            var path = CreateTurningPath(start, target, maxMovementPoints, movementType);
            if (path != null && useCache) _movementPathCache.Add(path);
            return path;
        }

        var frontier = new PriorityQueue<(HexPosition pos, List<HexPosition> path, int cost), int>();
        var visited = new Dictionary<(HexCoordinates coords, HexDirection facing), int>();

        frontier.Enqueue((start, [start], 0), 0);
        visited[(start.Coordinates, start.Facing)] = 0;

        while (frontier.Count > 0)
        {
            var (current, path, currentCost) = frontier.Dequeue();

            // Check if we've reached the target
            if (current.Coordinates == target.Coordinates && current.Facing == target.Facing)
            {
                // Convert path to segments
                var segments = ConvertPathToSegments(path);
                var result = new MovementPath(segments, movementType);
                if (useCache) _movementPathCache.Add(result);
                return result;
            }

            // For each adjacent hex
            foreach (var nextCoord in current.Coordinates.GetAdjacentCoordinates())
            {
                var hex = GetHex(nextCoord);
                if (hex == null || prohibitedHexes.Contains(nextCoord))
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
    /// Finds the longest path that maximizes hexes traversed within the movement budget
    /// The method does not guarantee to find the actually longest path, but it's good enough for the purpose.
    /// </summary>
    private MovementPath? FindLongestPath(HexPosition start,
        HexPosition target,
        MovementType movementType,
        int maxMovementPoints,
        IReadOnlySet<HexCoordinates>? prohibitedHexes)
    {
        prohibitedHexes ??= new HashSet<HexCoordinates>();
        var useCache = prohibitedHexes.Count == 0;

        if (useCache)
        {
            var cachedPath = _movementLongPathCache.Get(start, target, false);
            if (cachedPath != null)
            {
                return cachedPath.TotalCost <= maxMovementPoints ? cachedPath : null;
            }
        }

        // If start and target are in the same hex, just return turning segments
        if (start.Coordinates == target.Coordinates)
        {
            var path = CreateTurningPath(start, target, maxMovementPoints, movementType);
            if (path != null && useCache) _movementLongPathCache.Add(path);
            return path;
        }

        // Track the best path found so far (highest hexes traveled)
        MovementPath? bestPath = null;
        var bestHexesTraveled = 0;

        // Use a priority queue that prioritizes higher hex counts
        var frontier = new PriorityQueue<(HexPosition pos, List<HexPosition> path, int cost, int hexesTraveled), (int, int)>();
        var visited = new Dictionary<(HexCoordinates coords, HexDirection facing), (int cost, int hexesTraveled)>();

        frontier.Enqueue((start, [start], 0, 0), (0, 0));
        visited[(start.Coordinates, start.Facing)] = (0, 0);

        while (frontier.Count > 0)
        {
            var (current, path, currentCost, hexesTraveled) = frontier.Dequeue();

            // Check if we've reached the target
            if (current.Coordinates == target.Coordinates && current.Facing == target.Facing)
            {
                // Convert path to segments
                var segments = ConvertPathToSegments(path);
                var candidatePath = new MovementPath(segments, movementType);

                // Update the best path if this one has more hexes traveled
                if (candidatePath.HexesTraveled > bestHexesTraveled)
                {
                    bestPath = candidatePath;
                    bestHexesTraveled = candidatePath.HexesTraveled;
                }

                // Continue searching for longer paths
                continue;
            }

            // For each adjacent hex
            foreach (var nextCoord in current.Coordinates.GetAdjacentCoordinates())
            {
                var hex = GetHex(nextCoord);
                if (hex == null || prohibitedHexes.Contains(nextCoord))
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
                var newHexesTraveled = hexesTraveled + 1;

                if (totalCost > maxMovementPoints)
                    continue;

                // For the longest path, allow revisiting if we have the same cost but more hexes traveled
                var nextKey = (nextCoord, requiredFacing);
                if (visited.TryGetValue(nextKey, out var visitedState))
                {
                    // Skip only if we've visited with both lower-or-equal cost AND more-or-equal hexes
                    if (visitedState.cost <= totalCost && visitedState.hexesTraveled >= newHexesTraveled)
                        continue;
                }

                visited[nextKey] = (totalCost, newHexesTraveled);

                // Priority: negative hexes traveled (to maximize), then distance (to reach target)
                var priority = (-newHexesTraveled, nextCoord.DistanceTo(target.Coordinates));

                // If we're at target coordinates but wrong facing, add turning steps to target facing
                if (nextCoord == target.Coordinates && requiredFacing != target.Facing)
                {
                    var finalTurningSteps = nextPos.GetTurningSteps(target.Facing).ToList();
                    var finalCost = totalCost + finalTurningSteps.Count;
                    if (finalCost > maxMovementPoints) continue;
                    newPath.AddRange(finalTurningSteps);
                    frontier.Enqueue((new HexPosition(nextCoord, target.Facing), newPath, finalCost, newHexesTraveled), priority);
                }
                else
                {
                    frontier.Enqueue((nextPos, newPath, totalCost, newHexesTraveled), priority);
                }
            }
        }

        if (bestPath != null && useCache)
        {
            _movementLongPathCache.Add(bestPath);
        }

        return bestPath;
    }

    /// <summary>
    /// Gets all valid hexes that can be reached with given movement points, considering facing
    /// </summary>
    public IEnumerable<(HexCoordinates coordinates, int cost)> GetReachableHexes(
        HexPosition start,
        int maxMovementPoints,
        IReadOnlySet<HexCoordinates>? prohibitedHexes = null)
    {
        var visited = new Dictionary<(HexCoordinates coords, HexDirection facing), int>();
        var bestCosts = new Dictionary<HexCoordinates, int>();
        var toVisit = new Queue<HexPosition>();
        prohibitedHexes ??= new HashSet<HexCoordinates>();
        
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
                // Skip if hex doesn't exist on a map or is prohibited
                var neighborHex = GetHex(neighborCoord);
                if (neighborHex == null || prohibitedHexes.Contains(neighborCoord))
                    continue;

                // Get the required facing to move to this hex
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
        IReadOnlySet<HexCoordinates>? prohibitedHexes = null)
    {
        prohibitedHexes ??= new HashSet<HexCoordinates>();
        
        // Get all hexes within range using the existing method
        return start.GetCoordinatesInRange(movementPoints)
            .Where(coordinates =>
            {
                // Skip if hex doesn't exist on a map or is prohibited
                var hex = GetHex(coordinates);
                return hex != null && 
                       !prohibitedHexes.Contains(coordinates) &&
                       coordinates != start;
            });
    }

    /// <summary>
    /// Checks if there is a line of sight between two hexes
    /// </summary>
    public bool HasLineOfSight(HexCoordinates from, HexCoordinates to)
    {
        if (!IsOnMap(from) || !IsOnMap(to))
            return false;

        // If the same hex, always has LOS
        if (from == to)
            return true;

        var fromHex = GetHex(from);
        var toHex = GetHex(to);
        
        if (fromHex == null || toHex == null)
            return false;

        // Get all hexes along the line, resolving any divided line segments
        var hexLine = ResolveHexesAlongTheLine(from, to);

        // Remove the first and last hex (attacker and target positions)
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
        
        // Calculate a total intervening factor, handling nulls properly
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
        
        // If no divided segments, just return the main options
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

        // Choose whether to use secondary options based on which gives the better defense
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

    private MovementPath? FindJumpPath(HexPosition from, HexPosition to, int movementPoints)
    {
        if (!IsOnMap(from.Coordinates) || !IsOnMap(to.Coordinates))
            return null;

        var cachedPath = _movementPathCache.Get(from, to, true);
        if (cachedPath != null)
        {
            return cachedPath.TotalCost <= movementPoints ? cachedPath : null;
        }

        var distance = from.Coordinates.DistanceTo(to.Coordinates);
        if (distance > movementPoints)
            return null;

        // For jumping, we want the shortest path ignoring terrain and turning costs
        var path = new List<PathSegment>();
        
        if (from == to)
        {
            path.Add(new PathSegment(from, to, 0));
        }
        else
        {
            var currentPosition = from;
            var remainingDistance = distance;

            while (remainingDistance > 0)
            {
                // Find the next hex in the direction of the target
                var neighbors = currentPosition.Coordinates.GetAdjacentCoordinates()
                    .Where(IsOnMap)
                    .ToList();



                // Add a path segment with cost 1 (each hex costs 1 MP for jumping)
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
        }

        var result = new MovementPath(path, MovementType.Jump);
        _movementPathCache.Add(result);
        return result;
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

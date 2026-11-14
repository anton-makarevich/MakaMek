using Sanet.MakaMek.Map.Exceptions;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;

namespace Sanet.MakaMek.Map.Pathfinding;

/// <summary>
/// A* search over the hex graph for ground movement. The search itself knows nothing about
/// terrain: every legal step and its cost comes from the injected <see cref="IHexMovementRules"/>.
/// Supports the two <see cref="PathFindingMode"/> strategies: shortest cost and longest travel
/// within the movement budget.
/// </summary>
internal sealed class HexPathfinder(IHexGrid grid, IHexMovementRules movementRules)
{
    private static readonly IReadOnlySet<HexCoordinates> NoProhibitedHexes = new HashSet<HexCoordinates>();

    private readonly record struct PathfindingState(
        HexPosition Position,
        List<HexPosition> Path,
        int Cost,
        int HexesTraveled);

    private readonly record struct NeighbourTransition(
        HexCoordinates Coordinates,
        HexDirection RequiredFacing,
        int TurningCost,
        List<HexPosition> TurningSteps,
        HexTransition Transition);

    /// <summary>
    /// Finds a path between two positions, considering facing direction and movement costs.
    /// </summary>
    /// <param name="request">Search inputs.</param>
    /// <param name="cache">Cache for the selected path-finding mode.</param>
    public MovementPath? Find(PathRequest request, MovementPathCache cache)
    {
        var prohibitedHexes = request.ProhibitedHexes ?? NoProhibitedHexes;
        var useCache = prohibitedHexes.Count == 0;

        if (useCache)
        {
            var cachedPath = cache.Get(request.Start, request.Target, false, request.MaxLevelChange, request.UnitHeight);
            if (cachedPath != null)
            {
                if (cachedPath.TotalCost <= request.MaxMovementPoints)
                    return cachedPath;
                if (request.Mode == PathFindingMode.Shortest)
                    return null;
            }
        }

        if (request.Start.Coordinates == request.Target.Coordinates)
            return FindSameHexPath(request, cache, useCache);

        var path = Search(request, prohibitedHexes, cache, useCache);

        if (request.Mode == PathFindingMode.Longest && path != null && useCache)
            cache.Add(path, request.UnitHeight);

        return path;
    }

    /// <summary>
    /// Creates a turning path when start and target are in the same hex.
    /// </summary>
    private MovementPath? FindSameHexPath(PathRequest request, MovementPathCache cache, bool useCache)
    {
        if (request.Start.Surface != request.Target.Surface)
            return null;

        var path = CreateTurningPath(request.Start, request.Target, request.MaxMovementPoints, request.MovementType);
        if (path != null && useCache) cache.Add(path, request.UnitHeight);
        return path;
    }

    private static MovementPath? CreateTurningPath(
        HexPosition start,
        HexPosition target,
        int maxMovementPoints,
        MovementType movementType)
    {
        var turningSteps = start.GetTurningSteps(target.Facing).ToList();
        if (turningSteps.Count > maxMovementPoints)
            return null;

        var segments = new List<PathSegment>();

        if (turningSteps.Count == 0)
        {
            segments.Add(new PathSegment(start, target, [], ElevationChange: 0));
        }
        else
        {
            var currentPos = start;
            foreach (var step in turningSteps)
            {
                segments.Add(new PathSegment(
                    currentPos,
                    step,
                    [new RotationMovementCost { FromFacing = currentPos.Facing, ToFacing = step.Facing, Value = 1 }],
                    ElevationChange: 0));
                currentPos = step;
            }
        }

        return new MovementPath(segments, movementType);
    }

    private MovementPath? Search(
        PathRequest request,
        IReadOnlySet<HexCoordinates> prohibitedHexes,
        MovementPathCache cache,
        bool useCache)
    {
        MovementPath? bestPath = null;
        var bestHexesTraveled = 0;

        var frontier = new PriorityQueue<PathfindingState, (int, int)>();
        var visited = new Dictionary<HexPosition, (int Cost, int Hexes)>();

        frontier.Enqueue(new PathfindingState(request.Start, [request.Start], 0, 0), (0, 0));
        visited[request.Start] = (0, 0);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();

            if (current.Position == request.Target)
            {
                var candidatePath = BuildPath(request, current.Path);

                if (request.Mode == PathFindingMode.Shortest)
                {
                    if (useCache) cache.Add(candidatePath, request.UnitHeight);
                    return candidatePath;
                }

                if (candidatePath.HexesTraveled > bestHexesTraveled)
                {
                    bestPath = candidatePath;
                    bestHexesTraveled = candidatePath.HexesTraveled;
                }

                continue;
            }

            foreach (var (state, priority) in Expand(current, request, prohibitedHexes, visited))
            {
                frontier.Enqueue(state, priority);
            }
        }

        return request.Mode == PathFindingMode.Longest ? bestPath : null;
    }

    private IEnumerable<(PathfindingState State, (int Cost, int Heuristic) Priority)> Expand(
        PathfindingState current,
        PathRequest request,
        IReadOnlySet<HexCoordinates> prohibitedHexes,
        Dictionary<HexPosition, (int Cost, int Hexes)> visited)
    {
        var currentHex = grid.GetHex(current.Position.Coordinates);
        if (currentHex == null) yield break;

        foreach (var nextCoordinates in current.Position.Coordinates.GetAllNeighbours())
        {
            var nextHex = grid.GetHex(nextCoordinates);
            if (nextHex == null || prohibitedHexes.Contains(nextCoordinates))
                continue;

            var requiredFacing = current.Position.Coordinates.GetDirectionToNeighbour(nextCoordinates);
            var turningSteps = current.Position.GetTurningSteps(requiredFacing).ToList();

            foreach (var transition in movementRules.GetTransitions(
                         currentHex,
                         nextHex,
                         current.Position.Surface,
                         request.UnitHeight,
                         request.MaxLevelChange))
            {
                var step = new NeighbourTransition(
                    nextCoordinates,
                    requiredFacing,
                    turningSteps.Count,
                    turningSteps,
                    transition);

                foreach (var expanded in ExpandTransition(current, request, step, visited))
                {
                    yield return expanded;
                }
            }
        }
    }

    private IEnumerable<(PathfindingState State, (int Cost, int Heuristic) Priority)> ExpandTransition(
        PathfindingState current,
        PathRequest request,
        NeighbourTransition step,
        Dictionary<HexPosition, (int Cost, int Hexes)> visited)
    {
        var totalCost = current.Cost + step.Transition.MovementCost + step.TurningCost;
        if (totalCost > request.MaxMovementPoints) yield break;

        var nextPosition = new HexPosition(step.Coordinates, step.RequiredFacing, step.Transition.Surface);
        var isNewHex = current.Path.All(p => p.Coordinates != step.Coordinates);
        var hexesTraveled = current.HexesTraveled + (isNewHex ? 1 : 0);

        if (visited.TryGetValue(nextPosition, out var visitedState)
            && IsNotAnImprovement(request.Mode, visitedState, totalCost, hexesTraveled))
        {
            yield break;
        }

        visited[nextPosition] = (totalCost, hexesTraveled);

        var newPath = new List<HexPosition>(current.Path);
        newPath.AddRange(step.TurningSteps);
        newPath.Add(nextPosition);

        if (step.Coordinates != request.Target.Coordinates || step.RequiredFacing == request.Target.Facing)
        {
            yield return (
                new PathfindingState(nextPosition, newPath, totalCost, hexesTraveled),
                Priority(request.Mode, totalCost, hexesTraveled, step.Coordinates.DistanceTo(request.Target.Coordinates)));
            yield break;
        }

        var finalTurningSteps = nextPosition.GetTurningSteps(request.Target.Facing).ToList();
        var finalCost = totalCost + finalTurningSteps.Count;
        if (finalCost > request.MaxMovementPoints) yield break;

        newPath.AddRange(finalTurningSteps);
        var targetPosition = new HexPosition(step.Coordinates, request.Target.Facing, step.Transition.Surface);

        yield return (
            new PathfindingState(targetPosition, newPath, finalCost, hexesTraveled),
            Priority(request.Mode, finalCost, hexesTraveled, 0));
    }

    private static bool IsNotAnImprovement(
        PathFindingMode mode,
        (int Cost, int Hexes) visitedState,
        int cost,
        int hexesTraveled)
    {
        return mode == PathFindingMode.Shortest
            ? visitedState.Cost <= cost
            : visitedState.Cost <= cost && visitedState.Hexes >= hexesTraveled;
    }

    private static (int Cost, int Heuristic) Priority(
        PathFindingMode mode,
        int cost,
        int hexesTraveled,
        int distanceToTarget)
    {
        return mode == PathFindingMode.Shortest
            ? (cost + distanceToTarget, 0)
            : (-hexesTraveled, distanceToTarget);
    }

    private MovementPath BuildPath(PathRequest request, List<HexPosition> path)
    {
        return new MovementPath(ConvertPathToSegments(path), request.MovementType, request.MaxLevelChange);
    }

    /// <summary>
    /// Converts a list of hex positions to path segments.
    /// </summary>
    internal List<PathSegment> ConvertPathToSegments(List<HexPosition> path)
    {
        var segments = new List<PathSegment>();
        for (var i = 0; i < path.Count - 1; i++)
        {
            segments.Add(CreateSegment(path[i], path[i + 1]));
        }

        return segments;
    }

    private PathSegment CreateSegment(HexPosition from, HexPosition to)
    {
        if (from.Coordinates == to.Coordinates)
        {
            return new PathSegment(
                from,
                to,
                [new RotationMovementCost { FromFacing = from.Facing, ToFacing = to.Facing, Value = 1 }],
                ElevationChange: 0);
        }

        var hex = grid.GetHex(to.Coordinates) ?? throw new WrongHexException(to.Coordinates, "Hex not found");
        var fromHex = grid.GetHex(from.Coordinates) ?? throw new WrongHexException(from.Coordinates, "Hex not found");
        var elevationChange = hex.GetElevationChange(fromHex, from.Surface, to.Surface);

        var costs = new List<MovementCost>(hex.GetEnterMovementCost(fromHex, from.Surface, to.Surface));
        var levelCost = Math.Abs(elevationChange);
        if (levelCost > 0)
        {
            costs.Add(new ElevationChangeMovementCost { ElevationDelta = elevationChange, Value = levelCost });
        }

        return new PathSegment(from, to, costs, ElevationChange: elevationChange);
    }
}

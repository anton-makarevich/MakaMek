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

    /// <summary>
    /// One node of the search graph. The accepted step and the parent link are stored instead of a
    /// copied route prefix, so segments are built once for the selected route.
    /// </summary>
    private sealed class PathfindingState(
        HexPosition position,
        PathfindingState? parent,
        NeighbourTransition? enteringStep,
        int cost,
        int hexesTraveled,
        List<HexPosition>? finalTurningSteps = null)
    {
        public HexPosition Position { get; } = position;

        public PathfindingState? Parent { get; } = parent;

        public NeighbourTransition? EnteringStep { get; } = enteringStep;

        public int Cost { get; } = cost;

        public int HexesTraveled { get; } = hexesTraveled;

        public List<HexPosition>? FinalTurningSteps { get; } = finalTurningSteps;
    }

    private readonly record struct NeighbourTransition(
        Hex FromHex,
        Hex ToHex,
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
    private static MovementPath? FindSameHexPath(PathRequest request, MovementPathCache cache, bool useCache)
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
        PathfindingState? bestGoal = null;
        var bestHexesTraveled = 0;

        var frontier = new PriorityQueue<PathfindingState, (int, int)>();
        var visited = new Dictionary<HexPosition, (int Cost, int Hexes)>();

        frontier.Enqueue(new PathfindingState(request.Start, null, null, 0, 0), (0, 0));
        visited[request.Start] = (0, 0);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();

            if (current.Position == request.Target)
            {
                if (request.Mode == PathFindingMode.Shortest)
                {
                    var candidatePath = BuildPath(request, current);
                    if (useCache) cache.Add(candidatePath, request.UnitHeight);
                    return candidatePath;
                }

                // Every pathfinder segment is forward, so the state's count of distinct entered
                // hexes already equals the count BuildPath would report. Keeping only the best
                // goal state defers segment construction until the winner is known.
                if (current.HexesTraveled > bestHexesTraveled)
                {
                    bestGoal = current;
                    bestHexesTraveled = current.HexesTraveled;
                }

                continue;
            }

            foreach (var (state, priority) in Expand(current, request, prohibitedHexes, visited))
            {
                frontier.Enqueue(state, priority);
            }
        }

        return request.Mode == PathFindingMode.Longest && bestGoal != null
            ? BuildPath(request, bestGoal)
            : null;
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
                    currentHex,
                    nextHex,
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

    private static IEnumerable<(PathfindingState State, (int Cost, int Heuristic) Priority)> ExpandTransition(
        PathfindingState current,
        PathRequest request,
        NeighbourTransition step,
        Dictionary<HexPosition, (int Cost, int Hexes)> visited)
    {
        // Compare the accumulated cost in a wider type first: a rule can price a step high
        // enough that summing in int would wrap and let an over-budget step through.
        var totalCost = (long)current.Cost + step.Transition.MovementCost + step.TurningCost;
        if (totalCost > request.MaxMovementPoints) yield break;
        var cost = (int)totalCost;

        var nextPosition = new HexPosition(step.ToHex.Coordinates, step.RequiredFacing, step.Transition.Surface);
        var isNewHex = IsNewHex(current, step.ToHex.Coordinates);
        var hexesTraveled = current.HexesTraveled + (isNewHex ? 1 : 0);

        if (visited.TryGetValue(nextPosition, out var visitedState)
            && IsNotAnImprovement(request.Mode, visitedState, cost, hexesTraveled))
        {
            yield break;
        }

        visited[nextPosition] = (cost, hexesTraveled);

        // The route outlives the movement rules' enumerator, so take ownership of the accepted
        // step's costs before the rule can reuse its backing collection for the next transition.
        var acceptedStep = step with { Transition = step.Transition.WithOwnedCosts() };

        if (step.ToHex.Coordinates != request.Target.Coordinates || step.RequiredFacing == request.Target.Facing)
        {
            yield return (
                new PathfindingState(nextPosition, current, acceptedStep, cost, hexesTraveled),
                Priority(request.Mode, cost, hexesTraveled,
                    step.ToHex.Coordinates.DistanceTo(request.Target.Coordinates)));
            yield break;
        }

        var finalTurningSteps = nextPosition.GetTurningSteps(request.Target.Facing).ToList();
        var finalCost = totalCost + finalTurningSteps.Count;
        if (finalCost > request.MaxMovementPoints) yield break;
        var narrowedFinalCost = (int)finalCost;

        var targetPosition = new HexPosition(step.ToHex.Coordinates, request.Target.Facing, step.Transition.Surface);

        yield return (
            new PathfindingState(targetPosition, current, acceptedStep, narrowedFinalCost, hexesTraveled, finalTurningSteps),
            Priority(request.Mode, narrowedFinalCost, hexesTraveled, 0));
    }

    /// <summary>
    /// Checks whether this route has not entered <paramref name="coordinates"/> before by walking
    /// the parent links. Allocation-free, matching the scan the search used before it kept links.
    /// </summary>
    private static bool IsNewHex(PathfindingState current, HexCoordinates coordinates)
    {
        for (var state = current; state is not null; state = state.Parent)
        {
            if (state.Position.Coordinates == coordinates)
                return false;
        }

        return true;
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

    /// <summary>
    /// Rebuilds the accepted route from the goal's parent links and creates its segments once, so
    /// an unsuccessful or wide search never materialises path segments for abandoned branches.
    /// </summary>
    private static MovementPath BuildPath(PathRequest request, PathfindingState goal)
    {
        var route = new List<PathfindingState>();
        for (var state = goal; state is not null; state = state.Parent)
        {
            route.Add(state);
        }

        route.Reverse();

        var segments = new List<PathSegment>(route.Count);
        foreach (var state in route)
        {
            if (state.EnteringStep is not { } step)
                continue;

            var fromPosition = state.Parent!.Position;
            foreach (var turningStep in step.TurningSteps)
            {
                segments.Add(CreateRotationSegment(fromPosition, turningStep));
                fromPosition = turningStep;
            }

            // A goal reached with the wrong facing stores the target position after its final
            // turns; the movement segment itself ends at the entered facing.
            var enteredPosition = state.FinalTurningSteps is null
                ? state.Position
                : new HexPosition(state.Position.Coordinates, step.RequiredFacing, step.Transition.Surface);
            segments.Add(CreateMovementSegment(fromPosition, enteredPosition, step));

            if (state.FinalTurningSteps is not { } finalTurningSteps)
                continue;

            fromPosition = enteredPosition;
            foreach (var turningStep in finalTurningSteps)
            {
                segments.Add(CreateRotationSegment(fromPosition, turningStep));
                fromPosition = turningStep;
            }
        }

        return new MovementPath(segments, request.MovementType, request.MaxLevelChange);
    }

    private static PathSegment CreateRotationSegment(HexPosition from, HexPosition to)
    {
        return new PathSegment(
            from,
            to,
            [new RotationMovementCost { FromFacing = from.Facing, ToFacing = to.Facing, Value = 1 }],
            ElevationChange: 0);
    }

    /// <summary>
    /// Builds the segment for an accepted step from the cost breakdown the movement rules charged,
    /// so the returned path and its cache entry carry the same cost the search accepted.
    /// </summary>
    private static PathSegment CreateMovementSegment(
        HexPosition from,
        HexPosition to,
        NeighbourTransition step)
    {
        var elevationChange = step.ToHex.GetElevationChange(step.FromHex, from.Surface, to.Surface);

        var costs = new List<MovementCost>(step.Transition.EnterCosts);
        if (step.Transition.LevelCost > 0)
        {
            costs.Add(new ElevationChangeMovementCost
            {
                ElevationDelta = elevationChange,
                Value = step.Transition.LevelCost
            });
        }

        return new PathSegment(from, to, costs, ElevationChange: elevationChange);
    }
}

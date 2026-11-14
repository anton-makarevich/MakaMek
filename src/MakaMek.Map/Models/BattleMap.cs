using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Pathfinding;

namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Represents the game battle map, managing hexes and providing pathfinding capabilities.
/// The map itself is a thin hex store: pathfinding, reachability and line-of-sight work is
/// delegated to the dedicated collaborators in <see cref="Pathfinding"/>.
/// </summary>
public class BattleMap : IBattleMap
{
    private const string DefaultBiome = "makamek.biomes.grasslands";

    private readonly HexGrid _grid;
    private readonly LineOfSightCache _losCache;
    private readonly MovementPathCache _movementPathCache;
    private readonly MovementPathCache _movementLongPathCache;
    private readonly HexPathfinder _pathfinder;
    private readonly HexReachabilityCalculator _reachability;
    private readonly LineOfSightCalculator _lineOfSight;

    /// <summary>
    /// Creates a battle map with the default map collaborators.
    /// </summary>
    public BattleMap(int width, int height, string biome = DefaultBiome)
        : this(
            new HexGrid(width, height, biome),
            new HexMovementRules(),
            new LineOfSightCache(),
            new MovementPathCache(),
            new MovementPathCache())
    {
    }

    internal BattleMap(
        HexGrid grid,
        IHexMovementRules movementRules,
        LineOfSightCache losCache,
        MovementPathCache movementPathCache,
        MovementPathCache movementLongPathCache)
    {
        _grid = grid;
        _losCache = losCache;
        _movementPathCache = movementPathCache;
        _movementLongPathCache = movementLongPathCache;
        _pathfinder = new HexPathfinder(grid, movementRules);
        _reachability = new HexReachabilityCalculator(grid, movementRules);
        _lineOfSight = new LineOfSightCalculator(grid, losCache);
    }

    public int Width => _grid.Width;

    public int Height => _grid.Height;

    public string Biome => _grid.Biome;

    /// <summary>
    /// Adds a hex to the map. Throws HexOutsideOfMapBoundariesException if hex coordinates are outside map boundaries.
    /// Stamps the map's biome onto the hex.
    /// </summary>
    public void AddHex(Hex hex)
    {
        _grid.AddHex(hex);
    }

    public Hex? GetHex(HexCoordinates coordinates)
    {
        return _grid.GetHex(coordinates);
    }

    /// <summary>
    /// Finds a path between two positions, considering facing direction and movement costs
    /// </summary>
    public MovementPath? FindPath(HexPosition start,
        HexPosition target,
        MovementType movementType,
        int maxMovementPoints,
        int unitHeight,
        IReadOnlySet<HexCoordinates>? prohibitedHexes = null,
        PathFindingMode pathFindingMode = PathFindingMode.Shortest,
        int? maxLevelChange = null,
        int approachWaypointOffset = 0)
    {
        if (movementType == MovementType.Jump)
        {
            return FindJumpPath(start, target, maxMovementPoints, approachWaypointOffset);
        }

        var cache = pathFindingMode == PathFindingMode.Shortest
            ? _movementPathCache
            : _movementLongPathCache;

        var request = new PathRequest(
            pathFindingMode,
            start,
            target,
            movementType,
            maxMovementPoints,
            unitHeight,
            prohibitedHexes,
            maxLevelChange);

        return _pathfinder.Find(request, cache);
    }

    /// <summary>
    /// Gets all valid hexes that can be reached with given movement points, considering facing.
    /// Multiple entries may be returned for the same coordinate when both surfaces are
    /// independently reachable (e.g., both Ground and Bridge surfaces of a bridge hex).
    /// </summary>
    public IEnumerable<HexReachabilityData> GetReachableHexes(
        HexPosition start,
        int maxMovementPoints,
        int unitHeight,
        IReadOnlySet<HexCoordinates>? prohibitedHexes = null,
        int? maxLevelChange = null)
    {
        return _reachability.GetReachable(start, maxMovementPoints, unitHeight, prohibitedHexes, maxLevelChange);
    }

    /// <summary>
    /// Gets all valid hexes that can be reached with jumping movement, where each hex costs 1 MP
    /// regardless of terrain or facing direction
    /// </summary>
    public IEnumerable<HexReachabilityData> GetJumpReachableHexes(
        HexCoordinates start,
        int movementPoints,
        IReadOnlySet<HexCoordinates>? prohibitedHexes = null,
        HexSurface startSurface = HexSurface.Ground)
    {
        return _reachability.GetJumpReachable(start, movementPoints, prohibitedHexes, startSurface);
    }

    /// <summary>
    /// Calculates the line of sight between two positions, taking each position's surface and
    /// airborne level directly from the <see cref="HexPosition"/>.
    /// </summary>
    public LineOfSightResult GetLineOfSight(HexPosition from, HexPosition to, int attackHeight, int targetHeight = 0)
        => GetLineOfSight(
            from.Coordinates,
            to.Coordinates,
            attackHeight,
            targetHeight,
            from.Surface,
            to.Surface,
            from.AirborneLevel,
            to.AirborneLevel);

    /// <summary>
    /// Calculates the line of sight between two hexes and returns a result with full context.
    /// </summary>
    /// <param name="from">Source hex coordinates</param>
    /// <param name="to">Target hex coordinates</param>
    /// <param name="attackHeight">Height of the attacking weapon in levels (added to hex level).</param>
    /// <param name="targetHeight">Height of the target unit in levels (added to hex level). Defaults to 0 for no target.</param>
    /// <param name="fromSurface">Surface level of an attacker unit, bull if no attacker (check from the lowest level)</param>
    /// <param name="toSurface">Surface level of a target unit, null if no unit in a target hex</param>
    /// <param name="fromBaseLevel">Absolute base level of the attacker when it is airborne (for example a
    /// pending DFA approach position). When supplied, it replaces the surface standing level and the
    /// endpoint is never treated as submerged.</param>
    /// <param name="toBaseLevel">Absolute base level of the target when it is airborne. When supplied,
    /// it replaces the surface standing level and the endpoint is never treated as submerged.</param>
    public LineOfSightResult GetLineOfSight(HexCoordinates from, HexCoordinates to, int attackHeight,
        int targetHeight = 0, HexSurface? fromSurface = null, HexSurface? toSurface = null,
        int? fromBaseLevel = null, int? toBaseLevel = null)
    {
        return _lineOfSight.Get(from, to, attackHeight, targetHeight, fromSurface, toSurface, fromBaseLevel, toBaseLevel);
    }

    /// <summary>
    /// Converts a list of hex positions to path segments. Kept on the map as the façade for the
    /// pathfinder's segment builder.
    /// </summary>
    private List<PathSegment> ConvertPathToSegments(List<HexPosition> path)
    {
        return _pathfinder.ConvertPathToSegments(path);
    }

    public IEnumerable<Hex> GetHexes()
    {
        return _grid.GetHexes();
    }

    /// <summary>
    /// Converts the battle map to a data object including biome and hex data
    /// </summary>
    /// <returns>BattleMapData object representing the map</returns>
    public BattleMapData ToData()
    {
        return new BattleMapData
        {
            Biome = _grid.Biome,
            HexData = _grid.GetHexes().Select(hex => hex.ToData()).ToList()
        };
    }

    private MovementPath? FindJumpPath(HexPosition from, HexPosition to, int movementPoints,
        int approachWaypointOffset)
    {
        if (!IsOnMap(from.Coordinates) || !IsOnMap(to.Coordinates))
            return null;

        var cachedPath = _movementPathCache.Get(
            from,
            to,
            true,
            unitHeight: 0,
            jumpMovementPoints: movementPoints,
            approachWaypointOffset: approachWaypointOffset);
        if (cachedPath != null)
            return cachedPath;

        var startHex = GetHex(from.Coordinates);
        var targetHex = GetHex(to.Coordinates);
        if (startHex == null || targetHex == null)
            return null;

        var distance = from.Coordinates.DistanceTo(to.Coordinates);
        if (distance > movementPoints)
            return null;

        if (from.Coordinates == to.Coordinates)
        {
            var sameHexPath = new MovementPath(
                [new PathSegment(from, to, [], ElevationChange: 0)],
                MovementType.Jump);
            _movementPathCache.Add(sameHexPath, 0, movementPoints, keyStart: from, keyDestination: to,
                approachWaypointOffset: approachWaypointOffset);
            return sameHexPath;
        }

        // The path must follow a shortest route, and every crossed hex (destination included) must
        // stay at or below the jump-height ceiling. The start level is the unit's standing level:
        // a bridge deck when it starts on a bridge, the riverbed when it starts in water. Take the
        // first legal route: choosing among equally short legal routes has no rules effect here.
        var startLevel = startHex.GetStandingLevel(from.Surface);
        var ceiling = startLevel + movementPoints;
        var route = FindFirstLegalJumpRoute(from.Coordinates, to.Coordinates, distance, ceiling);
        if (route == null)
            return null;

        var levels = BuildJumpLevels(route, startLevel, ceiling, approachWaypointOffset);
        if (levels == null)
            return null;

        var result = BuildJumpPath(from, to, route, levels);
        _movementPathCache.Add(result, 0, movementPoints, keyStart: from, keyDestination: to,
            approachWaypointOffset: approachWaypointOffset);
        return result;
    }

    /// <summary>
    /// Returns the first shortest route from <paramref name="start"/> to <paramref name="target"/>
    /// whose crossed hexes all clear the jump ceiling, or <c>null</c> when no such route exists.
    /// </summary>
    private List<HexCoordinates>? FindFirstLegalJumpRoute(
        HexCoordinates start,
        HexCoordinates target,
        int distance,
        int ceiling)
    {
        var route = new List<HexCoordinates>(distance + 1) { start };
        return TryFind(start, distance) ? route : null;

        bool TryFind(HexCoordinates current, int remaining)
        {
            if (remaining == 0)
                return current == target;

            foreach (var neighbor in current.GetAllNeighbours())
            {
                // Stay on a shortest route: the remaining straight-line distance drops by one.
                if (neighbor.DistanceTo(target) != remaining - 1) continue;
                if (!IsJumpHexLegal(neighbor, ceiling)) continue;

                route.Add(neighbor);
                if (TryFind(neighbor, remaining - 1))
                    return true;
                route.RemoveAt(route.Count - 1);
            }

            return false;
        }
    }

    private bool IsJumpHexLegal(HexCoordinates coordinates, int ceiling)
    {
        var hex = GetHex(coordinates);
        return hex != null && hex.GetReferenceLevel() <= ceiling;
    }

    /// <summary>
    /// Builds the single vertical profile for the route. Both endpoints are grounded (<c>null</c>);
    /// intermediate positions carry their airborne base level. The last translation's <c>From</c> is
    /// the approach position and is always a hard waypoint at <c>max(reference approach, reference
    /// destination)</c>, whether or not the jump is a DFA, so DFA LOS can read it directly. An
    /// adjacent jump has no intermediate position, so its start position carries that waypoint.
    /// </summary>
    private int?[]? BuildJumpLevels(IReadOnlyList<HexCoordinates> route, int startLevel, int ceiling,
        int approachWaypointOffset)
    {
        var lastIndex = route.Count - 1;
        var levels = new int?[route.Count];
        if (lastIndex == 0)
            return levels;

        var referenceLevels = new int[route.Count];
        for (var i = 0; i <= lastIndex; i++)
            referenceLevels[i] = GetHex(route[i])!.GetReferenceLevel();

        var approachIndex = lastIndex - 1;

        // The start hex is not part of the crossed path, so an adjacent jump checks the
        // destination against the unit's standing level. Its reference level (a bridge deck
        // above or a water surface it is standing below) is not a clearance requirement.
        var startSideLevel = lastIndex == 1 ? startLevel : referenceLevels[approachIndex];
        if (Math.Max(startSideLevel, referenceLevels[lastIndex]) > ceiling)
            return null;

        // The stored waypoint is the airborne base the caller supplied for the approach position:
        // the higher of the approach and target reference levels plus the caller's rule offset.
        // It is also the start hex for an adjacent jump. LOS adds the unit height.
        var approachLevel = Math.Max(referenceLevels[approachIndex], referenceLevels[lastIndex])
                            + approachWaypointOffset;

        if (lastIndex == 1)
        {
            levels[0] = approachLevel;
            return levels;
        }

        for (var i = 1; i < approachIndex; i++)
        {
            var interpolated = startLevel
                               + CeilingDivide((approachLevel - startLevel) * i, approachIndex);
            levels[i] = Math.Clamp(interpolated, referenceLevels[i], ceiling);
        }

        levels[approachIndex] = approachLevel;
        return levels;
    }

    private static int CeilingDivide(int numerator, int denominator)
    {
        if (numerator >= 0)
            return (numerator + denominator - 1) / denominator;
        return -(-numerator / denominator);
    }

    private MovementPath BuildJumpPath(
        HexPosition start,
        HexPosition target,
        IReadOnlyList<HexCoordinates> route,
        IReadOnlyList<int?> levels)
    {
        var lastIndex = route.Count - 1;
        var startPosition = start with { AirborneLevel = levels[0] };
        var positions = new List<HexPosition>(route.Count) { startPosition };

        for (var i = 1; i <= lastIndex; i++)
        {
            var coordinates = route[i];
            var hex = GetHex(coordinates)!;
            var facing = i == lastIndex
                ? target.Facing
                : route[i - 1].GetDirectionToNeighbour(coordinates);
            var surface = hex.GetBridgeHeight() != null ? HexSurface.Bridge : HexSurface.Ground;
            positions.Add(new HexPosition(coordinates, facing, surface, levels[i]));
        }

        var segments = new List<PathSegment>(lastIndex);
        for (var i = 0; i < lastIndex; i++)
        {
            var fromHex = GetHex(route[i])!;
            var toHex = GetHex(route[i + 1])!;
            segments.Add(new PathSegment(
                positions[i],
                positions[i + 1],
                [new JumpMovementCost { Value = 1 }],
                ElevationChange: toHex.GetGroundElevationChange(fromHex)));
        }

        return new MovementPath(segments, MovementType.Jump);
    }

    /// <summary>
    /// Clears the line of sight cache. Should be called at the end of each turn.
    /// </summary>
    public void ClearLosCache()
    {
        _losCache.Clear();
    }

    public IEnumerable<HexCoordinates> GetAffectedCoordinates(HexCoordinates changedCoord)
    {
        return _grid.GetAffectedCoordinates(changedCoord);
    }

    public bool IsOnMap(HexCoordinates coordinates)
    {
        return _grid.IsOnMap(coordinates);
    }

    /// <summary>
    /// Gets the edge information for all 6 edges of a hex
    /// </summary>
    /// <param name="coordinates">The coordinates of the hex</param>
    /// <returns>A list of HexEdge objects for all 6 directions. Returns an empty list if hex doesn't exist.</returns>
    public IReadOnlyList<HexEdge> GetHexEdges(HexCoordinates coordinates)
    {
        var hex = GetHex(coordinates);
        if (hex == null)
            return [];

        var edges = new List<HexEdge>(6);

        foreach (var direction in HexDirectionExtensions.AllDirections)
        {
            var neighborCoords = coordinates.GetNeighbour(direction);
            var neighborHex = GetHex(neighborCoords);

            // If neighbor doesn't exist (map boundary), treat as elevation difference 0
            var elevationDifference = neighborHex != null
                ? hex.GetLevelDifference(neighborHex)
                : 0;

            edges.Add(new HexEdge(coordinates, direction, elevationDifference));
        }

        return edges;
    }
}

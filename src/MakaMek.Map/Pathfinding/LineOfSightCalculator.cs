using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Pathfinding;

/// <summary>
/// Calculates line of sight between two hexes. The calculator walks the segments produced by
/// <see cref="HexCoordinates.LineTo"/> and evaluates each intervening hex for elevation and
/// terrain obstruction; it never re-implements the line interpolation itself.
/// </summary>
internal sealed class LineOfSightCalculator(IHexGrid grid, LineOfSightCache cache)
{
    /// <summary>
    /// Calculates the line of sight between two hexes and returns a result with full context.
    /// </summary>
    /// <param name="from">Source hex coordinates.</param>
    /// <param name="to">Target hex coordinates.</param>
    /// <param name="attackHeight">Height of the attacking weapon in levels (added to hex level).</param>
    /// <param name="targetHeight">Height of the target unit in levels (added to hex level).</param>
    /// <param name="fromSurface">Surface of the attacker endpoint; null infers the bridge deck when present.</param>
    /// <param name="toSurface">Surface of the target endpoint; null infers the bridge deck when present.</param>
    /// <param name="fromBaseLevel">Absolute base level of an airborne attacker, overriding the standing level.</param>
    /// <param name="toBaseLevel">Absolute base level of an airborne target, overriding the standing level.</param>
    public LineOfSightResult Get(
        HexCoordinates from,
        HexCoordinates to,
        int attackHeight,
        int targetHeight,
        HexSurface? fromSurface,
        HexSurface? toSurface,
        int? fromBaseLevel,
        int? toBaseLevel)
    {
        var isFromOnMap = grid.IsOnMap(from);
        var isToOnMap = grid.IsOnMap(to);

        if (!isFromOnMap || !isToOnMap)
        {
            return LineOfSightResult.Blocked(
                from: from,
                to: to,
                blockingHex: isFromOnMap ? to : from,
                reason: LineOfSightBlockReason.InvalidCoordinates);
        }

        var fromHex = grid.GetHex(from);
        var toHex = grid.GetHex(to);

        if (fromHex == null || toHex == null)
        {
            return LineOfSightResult.Blocked(
                from: from,
                to: to,
                blockingHex: fromHex == null ? from : to);
        }

        var attacker = ResolveEndpoint(fromHex, fromSurface, attackHeight, fromBaseLevel);
        var target = ResolveEndpoint(toHex, toSurface, targetHeight, toBaseLevel);

        if (attacker.Submerged != target.Submerged)
        {
            return LineOfSightResult.Blocked(
                from: from,
                to: to,
                blockingHex: attacker.Submerged ? from : to,
                reason: LineOfSightBlockReason.WaterSubmersion);
        }

        // The same hex always has LOS, no intervening path.
        if (from == to)
        {
            return LineOfSightResult.Unblocked(
                from: from,
                to: to,
                attackerLosLevel: attacker.LosLevel,
                targetLosLevel: target.LosLevel,
                hexPath: [CreateTargetHexInfo(toHex, target.LosLevel)]);
        }

        // Resolve the divided line segments, then drop the attacker and target positions.
        var hexLine = ResolveHexesAlongTheLine(from, to).Skip(1).SkipLast(1).ToList();

        if (hexLine.Count == 0)
        {
            return LineOfSightResult.Unblocked(
                from: from,
                to: to,
                attackerLosLevel: attacker.LosLevel,
                targetLosLevel: target.LosLevel,
                hexPath: [CreateTargetHexInfo(toHex, target.LosLevel)]);
        }

        return EvaluatePath(from, to, hexLine, attacker, target, toHex);
    }

    /// <summary>
    /// Computes the effective LOS level of an endpoint. An absolute base level (an airborne
    /// position) replaces the surface standing level and is never submerged.
    /// </summary>
    private static LosEndpoint ResolveEndpoint(Hex hex, HexSurface? surface, int height, int? baseLevel)
    {
        if (baseLevel is { } airborneLevel)
            return new LosEndpoint(airborneLevel + height, Submerged: false);

        var (standingLevel, submerged) = hex.GetLosEndpointInfo(surface, height);
        return new LosEndpoint(standingLevel + height, submerged);
    }

    private LineOfSightResult EvaluatePath(
        HexCoordinates from,
        HexCoordinates to,
        List<HexCoordinates> hexLine,
        LosEndpoint attacker,
        LosEndpoint target,
        Hex toHex)
    {
        var accumulator = new LosPathAccumulator();
        var totalDistance = hexLine.Count + 1;

        foreach (var coordinates in hexLine)
        {
            var hex = grid.GetHex(coordinates);
            if (hex == null)
            {
                return CreateBlockedResult(
                    from, to, attacker, target, accumulator,
                    coordinates: coordinates,
                    reason: LineOfSightBlockReason.InvalidCoordinates);
            }

            var blocked = EvaluateHex(from, to, hex, coordinates, attacker, target, totalDistance, accumulator);
            if (blocked != null)
                return blocked;
        }

        // The target hex contributes to the path but never blocks the line of sight.
        accumulator.HexPath.Add(CreateTargetHexInfo(toHex, target.LosLevel));

        return new LineOfSightResult
        {
            From = from,
            To = to,
            AttackerLosLevel = attacker.LosLevel,
            TargetLosLevel = target.LosLevel,
            HasLineOfSight = true,
            HexPath = accumulator.HexPath,
            TotalInterveningFactor = accumulator.TotalInterveningFactor
        };
    }

    /// <summary>
    /// Evaluates one intervening hex, updating the accumulator, and returns a blocked result when
    /// the hex ends the line of sight.
    /// </summary>
    private LineOfSightResult? EvaluateHex(
        HexCoordinates from,
        HexCoordinates to,
        Hex hex,
        HexCoordinates coordinates,
        LosEndpoint attacker,
        LosEndpoint target,
        int totalDistance,
        LosPathAccumulator accumulator)
    {
        var interpolatedLosHeight = InterpolateHeight(
            attacker.LosLevel,
            target.LosLevel,
            accumulator.Distance,
            totalDistance);

        // Only terrain whose ceiling reaches the LOS line contributes an intervening factor.
        var contribution = hex.GetCeiling() >= interpolatedLosHeight ? hex.GetInterveningFactor() : 0;

        accumulator.HexPath.Add(new LineOfSightHexInfo
        {
            Hex = hex,
            InterpolatedLosHeight = interpolatedLosHeight,
            InterveningFactor = contribution
        });

        // Elevation check: the hex base level blocks the LOS line entirely.
        if (hex.GetBottomLevel() >= interpolatedLosHeight)
        {
            return CreateBlockedResult(
                from, to, attacker, target, accumulator,
                coordinates: coordinates,
                reason: LineOfSightBlockReason.Elevation);
        }

        accumulator.TotalInterveningFactor += contribution;

        // Terrain intervening factor threshold check.
        if (accumulator.TotalInterveningFactor >= 3)
        {
            return CreateBlockedResult(
                from, to, attacker, target, accumulator,
                coordinates: coordinates,
                reason: LineOfSightBlockReason.InterveningTerrain);
        }

        accumulator.Distance++;
        return null;
    }

    private static LineOfSightResult CreateBlockedResult(
        HexCoordinates from,
        HexCoordinates to,
        LosEndpoint attacker,
        LosEndpoint target,
        LosPathAccumulator accumulator,
        HexCoordinates coordinates,
        LineOfSightBlockReason reason)
    {
        return new LineOfSightResult
        {
            From = from,
            To = to,
            AttackerLosLevel = attacker.LosLevel,
            TargetLosLevel = target.LosLevel,
            HasLineOfSight = false,
            HexPath = accumulator.HexPath,
            BlockingHexCoordinates = coordinates,
            BlockReason = reason,
            TotalInterveningFactor = accumulator.TotalInterveningFactor
        };
    }

    /// <summary>
    /// Resolves the line into a single hex path, choosing the defender-favouring option for divided
    /// segments and caching the result for both directions.
    /// </summary>
    private List<HexCoordinates> ResolveHexesAlongTheLine(HexCoordinates from, HexCoordinates to)
    {
        if (cache.TryGetPath(from, to, out var cachedPath))
            return cachedPath!;

        var segments = from.LineTo(to);

        if (segments.All(s => s.SecondOption == null))
        {
            var path = segments.Select(s => s.MainOption).ToList();
            cache.AddPath(from, to, path);
            return path;
        }

        var useSecondaryOptions = ShouldUseSecondaryOptions(segments);

        var resolvedPath = segments
            .Select(s => s.SecondOption != null && useSecondaryOptions ? s.SecondOption : s.MainOption)
            .ToList();

        cache.AddPath(from, to, resolvedPath);

        return resolvedPath;
    }

    /// <summary>
    /// Chooses the divided-segment option that gives the defender the better cover: the side with
    /// the larger total intervening factor wins.
    /// </summary>
    private bool ShouldUseSecondaryOptions(List<LineOfSightSegment> segments)
    {
        var dividedSegments = segments.Where(s => s.SecondOption != null).ToList();

        var mainOptionsFactor = dividedSegments
            .Sum(s => grid.GetHex(s.MainOption)?.GetInterveningFactor() ?? 0);

        var secondaryOptionsFactor = dividedSegments
            .Sum(s => grid.GetHex(s.SecondOption!)?.GetInterveningFactor() ?? 0);

        return secondaryOptionsFactor > mainOptionsFactor;
    }

    private static LineOfSightHexInfo CreateTargetHexInfo(Hex targetHex, int targetLosLevel)
    {
        return new LineOfSightHexInfo
        {
            Hex = targetHex,
            InterpolatedLosHeight = targetLosLevel,
            InterveningFactor = targetHex.GetCeiling() >= targetLosLevel
                ? targetHex.GetInterveningFactor()
                : 0
        };
    }

    /// <summary>
    /// Interpolates height between two points for LOS calculation.
    /// </summary>
    private static double InterpolateHeight(int startHeight, int endHeight, int currentDistance, int totalDistance)
    {
        if (totalDistance == 0)
            return startHeight;

        var t = (double)currentDistance / totalDistance;
        return startHeight + (endHeight - startHeight) * t;
    }

    private readonly record struct LosEndpoint(int LosLevel, bool Submerged);

    private sealed class LosPathAccumulator
    {
        public int Distance { get; set; } = 1;

        public int TotalInterveningFactor { get; set; }

        public List<LineOfSightHexInfo> HexPath { get; } = [];
    }
}

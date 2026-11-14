using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Movement;

/// <summary>
/// Candidate generation against the battle map's reachability and path-finding API.
/// </summary>
public sealed class MoveCandidateProvider(IClientGame game) : IMoveCandidateProvider
{
    public IReadOnlyList<MovementPath> GetCandidatePaths(
        IUnit unit,
        IReadOnlySet<HexCoordinates> friendlyPositions)
    {
        var candidatePaths = new List<MovementPath>();
        var battleMap = game.BattleMap;
        if (battleMap == null || unit.Position == null)
            return candidatePaths;

        // Get occupied hexes (exclude the moving unit itself)
        var occupiedHexes = GetOccupiedHexes(unit);
        var approachWaypointOffset = game.RulesProvider.GetDfaAirborneBaseOffset();

        foreach (var movementType in unit.GetAvailableMovementTypes())
        {
            // Get all reachable hexes for this movement type
            var reachabilityData = battleMap.GetReachableHexesForUnit(
                unit,
                movementType,
                occupiedHexes,
                friendlyPositions);

            // Process all reachable hexes (both forward and backward)
            foreach (var coordinates in reachabilityData.AllReachableCoordinates)
            {
                var paths = battleMap.GetPathsToHexWithAllFacings(
                    unit.Position,
                    coordinates,
                    movementType,
                    unit.GetMovementPoints(movementType),
                    reachabilityData,
                    unit.Height,
                    unit.MaxLevelChangeForward,
                    unit.MaxLevelChangeBackward,
                    occupiedHexes,
                    PathFindingMode.Longest,
                    approachWaypointOffset: approachWaypointOffset);

                candidatePaths.AddRange(paths.Values);
            }
        }

        return candidatePaths;
    }

    /// <summary>
    /// Gets the coordinates of all occupied hexes (deployed units except the specified moving unit)
    /// </summary>
    private HashSet<HexCoordinates> GetOccupiedHexes(IUnit movingUnit)
    {
        return game.Players
            .SelectMany(p => p.Units)
            .Where(u => u is { IsDeployed: true, Position: not null } && u.Id != movingUnit.Id)
            .Select(u => u.Position!.Coordinates)
            .ToHashSet();
    }
}

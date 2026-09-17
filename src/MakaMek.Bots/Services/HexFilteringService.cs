using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Services;

/// <summary>
/// Selects a small, deterministic set of tactically relevant facings for bot movement.
/// </summary>
public sealed class HexFilteringService
{
    /// <summary>
    /// Gets the primary facing toward the enemy center of mass and its two adjacent flanking
    /// facings. When no enemy positions are available, a stable three-facing fallback is used.
    /// </summary>
    /// <param name="candidateHex">The hex where the bot may end its movement.</param>
    /// <param name="enemyPositions">Known deployed enemy positions.</param>
    /// <returns>Exactly three distinct facings in primary, left-flank, right-flank order.</returns>
    public IReadOnlyList<HexDirection> GetPriorityFacings(
        HexCoordinates candidateHex,
        IEnumerable<HexCoordinates>? enemyPositions)
    {
        var positions = enemyPositions?.ToList() ?? [];
        var primaryFacing = positions.Count == 0
            ? HexDirection.Top
            : FindFacingTowardCenter(candidateHex, positions);

        return
        [
            primaryFacing,
            primaryFacing.Rotate(-1),
            primaryFacing.Rotate(1)
        ];
    }

    private static HexDirection FindFacingTowardCenter(
        HexCoordinates candidateHex,
        IReadOnlyList<HexCoordinates> enemyPositions)
    {
        var centerX = enemyPositions.Average(position => position.X);
        var centerY = enemyPositions.Average(position => position.Y);
        var centerZ = enemyPositions.Average(position => position.Z);

        return HexDirectionExtensions.AllDirections
            .Select((direction, index) =>
            {
                var neighbour = candidateHex.GetNeighbour(direction);
                var distanceSquared =
                    Math.Pow(neighbour.X - centerX, 2)
                    + Math.Pow(neighbour.Y - centerY, 2)
                    + Math.Pow(neighbour.Z - centerZ, 2);
                return (direction, index, distanceSquared);
            })
            .OrderBy(result => result.distanceSquared)
            .ThenBy(result => result.index)
            .First()
            .direction;
    }
}

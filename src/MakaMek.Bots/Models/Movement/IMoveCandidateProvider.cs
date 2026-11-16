using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Movement;

/// <summary>
/// Generates the movement path candidates a unit can choose from.
/// </summary>
public interface IMoveCandidateProvider
{
    /// <summary>
    /// Generates every reachable path for the unit across all of its available movement types.
    /// </summary>
    /// <param name="unit">The unit that would move.</param>
    /// <param name="friendlyPositions">Hexes occupied by friendly units (passable but not valid destinations).</param>
    IReadOnlyList<MovementPath> GetCandidatePaths(IUnit unit, IReadOnlySet<HexCoordinates> friendlyPositions);
}

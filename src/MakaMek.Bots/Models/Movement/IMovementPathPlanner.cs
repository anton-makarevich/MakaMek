using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Movement;

/// <summary>
/// Scores candidate movement paths and selects the best one for a unit.
/// </summary>
public interface IMovementPathPlanner
{
    /// <summary>
    /// Evaluates every candidate path and returns the best one, or <c>null</c> when no path
    /// could be evaluated.
    /// </summary>
    /// <param name="unit">The unit that would move.</param>
    /// <param name="enemyUnits">All enemy units, used for tactical scoring.</param>
    /// <param name="friendlyPositions">Hexes occupied by friendly units.</param>
    /// <param name="turnState">Optional turn state for caching evaluation results.</param>
    /// <param name="settings">Bot settings controlling the offensive/defensive weighting.</param>
    MovementPath? SelectBestPath(
        IUnit unit,
        IReadOnlyList<IUnit> enemyUnits,
        IReadOnlySet<HexCoordinates> friendlyPositions,
        ITurnState? turnState,
        BotSettings settings);
}

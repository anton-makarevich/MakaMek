using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Bots.Models.Movement;

/// <summary>
/// Calculates the order in which a bot should move its units.
/// </summary>
public interface IUnitPriorityCalculator
{
    /// <summary>
    /// Calculates the movement priority for a unit. Higher values are moved first.
    /// </summary>
    double CalculatePriority(IUnit unit, MovementPhaseState state);
}

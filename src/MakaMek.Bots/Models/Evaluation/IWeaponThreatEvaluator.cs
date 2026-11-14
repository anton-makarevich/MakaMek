using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Evaluation;

/// <summary>
/// Calculates the threat an enemy unit projects onto a defended position.
/// </summary>
public interface IWeaponThreatEvaluator
{
    /// <summary>
    /// Yields the threat value of every enemy weapon that can effectively fire at the defended position.
    /// </summary>
    /// <param name="enemy">Snapshot of the enemy unit projecting the threat.</param>
    /// <param name="targetArc">Arc of the defended position that the enemy would hit.</param>
    /// <param name="defenderPath">Defended position and the path used to reach it.</param>
    /// <param name="defenderHeight">Height of the defending unit in levels.</param>
    /// <param name="defenderIsProne">Whether the defending unit is prone.</param>
    IEnumerable<double> GetThreats(
        UnitSnapshot enemy,
        FiringArc targetArc,
        MovementPath defenderPath,
        int defenderHeight,
        bool defenderIsProne);
}

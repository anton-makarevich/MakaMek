using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Evaluation;

/// <summary>
/// Estimates the probability of a hypothetical weapon attack hitting its target.
/// </summary>
public interface IHitProbabilityCalculator
{
    /// <summary>
    /// Calculates the hit probability for an attack described by the supplied snapshots and paths.
    /// </summary>
    double Calculate(
        UnitSnapshot attacker,
        MovementPath attackerPath,
        HexPosition attackerPosition,
        MovementPath targetPath,
        HexPosition targetPosition,
        WeaponSnapshot weapon,
        HexDirection? attackerFacing,
        int targetHeight,
        bool isTargetProne);
}

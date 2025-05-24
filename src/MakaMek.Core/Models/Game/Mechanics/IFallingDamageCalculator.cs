using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Interface for calculating damage when a unit falls
/// </summary>
public interface IFallingDamageCalculator
{
    /// <summary>
    /// Calculates the damage a unit takes when falling
    /// </summary>
    /// <param name="unit">The unit that fell</param>
    /// <param name="levelsFallen">The number of levels the unit fell</param>
    /// <param name="wasJumping">Whether the unit was jumping when it fell</param>
    /// <returns>The result of the falling damage calculation</returns>
    FallingDamageData CalculateFallingDamage(Unit unit, int levelsFallen, bool wasJumping);

    /// <summary>
    /// Determines if a warrior takes damage from a fall
    /// </summary>
    /// <param name="unit">The unit that fell</param>
    /// <param name="levelsFallen">The number of levels the unit fell</param>
    /// <param name="psrBreakdown">The piloting skill roll breakdown</param>
    /// <param name="diceRolls">The dice roll result</param>
    /// <returns>True if the MechWarrior takes damage, false otherwise</returns>
    bool DeterminePilotDamage(Unit unit, int levelsFallen, PsrBreakdown psrBreakdown, List<DiceResult> diceRolls);
}

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
    /// <param name="psrBreakdown">The piloting skill roll breakdown</param>
    /// <returns>The result of the falling damage calculation</returns>
    FallingDamageData CalculateFallingDamage(Unit unit, int levelsFallen, bool wasJumping, PsrBreakdown psrBreakdown);
}

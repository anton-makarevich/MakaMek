using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;

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
    /// Calculates the damage a unit takes when skidding
    /// </summary>
    /// <param name="unit">The unit that skidded</param>
    /// <param name="skidHexes">The number of hexes the unit skidded</param>
    /// <returns>The result of the skid damage calculation</returns>
    FallingDamageData CalculateSkidDamage(Unit unit, int skidHexes);
}

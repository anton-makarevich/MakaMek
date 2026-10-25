using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;

public interface IFallingDamageCalculator
{
    /// <summary>
    /// Calculates falling damage. Rolls the facing direction after the fall.
    /// </summary>
    /// <param name="unit">The falling unit</param>
    /// <param name="levelsFallen">
    /// The total number of levels the unit fell, down to the hex bottom
    /// (0 for a fall in place).
    /// </param>
    /// <param name="waterDepth">
    /// Water depth of the hex where the fall happens (0 for dry land).
    /// The water reduction is delegated to the rules provider and is rules-set specific.
    /// </param>
    FallingDamageData CalculateFallingDamage(Unit unit, int levelsFallen, int waterDepth = 0);

    /// <summary>
    /// Calculates falling damage with an explicit facing roll.
    /// </summary>
    /// <param name="unit">The falling unit</param>
    /// <param name="levelsFallen">
    /// The total number of levels the unit fell, down to the hex bottom
    /// (0 for a fall in place).
    /// </param>
    /// <param name="waterDepth">
    /// Water depth of the hex where the fall happens (0 for dry land).
    /// The water reduction is delegated to the rules provider and is rules-set specific.
    /// </param>
    /// <param name="facingDiceRoll">The dice roll for the facing after the fall</param>
    /// <param name="facingAfterFall">The facing after the fall</param>
    FallingDamageData CalculateFallingDamage(Unit unit, int levelsFallen, int waterDepth, DiceResult facingDiceRoll, HexDirection facingAfterFall);

    FallingDamageData CalculateSkidDamage(Unit unit, int skidDistance, HexDirection facingAfterFall, DiceResult facingRoll, HitDirection attackDirection);
}

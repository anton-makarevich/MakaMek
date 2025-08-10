using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Interface for calculating to-hit modifiers using GATOR system
/// </summary>
public interface IToHitCalculator
{
    /// <summary>
    /// Gets the total to-hit modifier for a weapon attack
    /// </summary>
    int GetToHitNumber(Unit attacker,
        Unit target,
        Weapon weapon,
        BattleMap map,
        bool isPrimaryTarget = true,
        PartLocation? aimedShotTarget = null);

    /// <summary>
    /// Gets detailed breakdown of all modifiers affecting the attack, including aimed shot modifiers
    /// </summary>
    ToHitBreakdown GetModifierBreakdown(Unit attacker,
        Unit target,
        Weapon weapon,
        BattleMap map,
        bool isPrimaryTarget = true,
        PartLocation? aimedShotTarget = null);

    /// <summary>
    /// Creates a new ToHitBreakdown with an aimed shot modifier added to the existing breakdown.
    /// This is more efficient than recalculating the entire breakdown when only the aimed shot target changes.
    /// </summary>
    /// <param name="existingBreakdown">The existing breakdown to modify</param>
    /// <param name="aimedShotTarget">The body part being targeted for the aimed shot</param>
    /// <returns>A new ToHitBreakdown with the aimed shot modifier added</returns>
    ToHitBreakdown AddAimedShotModifier(ToHitBreakdown existingBreakdown, PartLocation aimedShotTarget);
}

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
    int GetToHitNumber(IUnit attacker,
        IUnit target,
        Weapon weapon,
        IBattleMap map,
        bool isPrimaryTarget = true,
        PartLocation? aimedShotTarget = null);

    /// <summary>
    /// Gets a detailed breakdown of all modifiers affecting the attack, including aimed shot modifiers
    /// </summary>
    ToHitBreakdown GetModifierBreakdown(IUnit attacker,
        IUnit target,
        Weapon weapon,
        IBattleMap map,
        bool isPrimaryTarget = true,
        PartLocation? aimedShotTarget = null);

    /// <summary>
    /// Creates a new ToHitBreakdown by adding or replacing the aimed shot modifier in the existing breakdown.
    /// This is more efficient than recalculating the entire breakdown when only the aimed shot target changes.
    /// </summary>
    /// <param name="existingBreakdown">The existing breakdown to modify</param>
    /// <param name="aimedShotTarget">The body part being targeted for the aimed shot</param>
    /// <returns>A new ToHitBreakdown with the aimed shot modifier added</returns>
    ToHitBreakdown AddAimedShotModifier(ToHitBreakdown existingBreakdown, PartLocation aimedShotTarget);

    /// <summary>
    /// Gets the total to-hit number for a weapon attack using an attack scenario.
    /// This method supports both actual attacks and hypothetical scenarios (for bot evaluation).
    /// </summary>
    /// <param name="scenario">The attack scenario containing all attack parameters</param>
    /// <param name="weapon">The weapon being fired</param>
    /// <param name="map">The battle map for line of sight and terrain calculations</param>
    /// <returns>The total to-hit number (2-13, where 13 is impossible)</returns>
    int GetToHitNumber(AttackScenario scenario, Weapon weapon, IBattleMap map);

    /// <summary>
    /// Gets a detailed breakdown of all modifiers affecting the attack using an attack scenario.
    /// This method supports both actual attacks and hypothetical scenarios (for bot evaluation).
    /// </summary>
    /// <param name="scenario">The attack scenario containing all attack parameters</param>
    /// <param name="weapon">The weapon being fired</param>
    /// <param name="map">The battle map for line of sight and terrain calculations</param>
    /// <returns>Detailed breakdown of all modifiers</returns>
    ToHitBreakdown GetModifierBreakdown(AttackScenario scenario, Weapon weapon, IBattleMap map);
}

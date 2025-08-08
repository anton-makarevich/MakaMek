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
    int GetToHitNumber(Unit attacker, Unit target, Weapon weapon, BattleMap map, bool isPrimaryTarget = true);

    /// <summary>
    /// Gets detailed breakdown of all modifiers affecting the attack, including aimed shot modifiers
    /// </summary>
    ToHitBreakdown GetModifierBreakdown(Unit attacker, Unit target, Weapon weapon, BattleMap map, bool isPrimaryTarget = true, PartLocation? aimedShotTarget = null);
}

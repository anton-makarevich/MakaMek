using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// To-hit calculation for physical attacks. Separate from IToHitCalculator because the base is
/// Piloting rather than Gunnery and there is no weapon or range involved.
/// </summary>
public interface IPhysicalToHitCalculator
{
    /// <summary>
    /// The to-hit number for a declared physical attack.
    /// </summary>
    int GetToHitNumber(
        IUnit attacker,
        IUnit target,
        PhysicalAttackType attackType,
        PartLocation attackerLimb,
        IBattleMap map);

    /// <summary>
    /// The modifier breakdown behind that number.
    /// </summary>
    PhysicalToHitBreakdown GetModifierBreakdown(
        IUnit attacker,
        IUnit target,
        PhysicalAttackType attackType,
        PartLocation attackerLimb,
        IBattleMap map);

    /// <summary>
    /// The same calculation over a scenario rather than live units, for the UI and bots. Reads
    /// nothing and changes nothing, so it is safe to call while the player is still choosing.
    /// </summary>
    PhysicalToHitBreakdown Preview(PhysicalAttackScenario scenario, IBattleMap map);
}

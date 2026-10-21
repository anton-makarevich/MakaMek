using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Interface for calculating physical attack to-hit modifiers, keyed on the attacker's Piloting Skill.
/// Heat and sensor-damage modifiers never apply to physical attacks.
/// </summary>
public interface IPhysicalAttackCalculator
{
    /// <summary>
    /// Gets the total to-hit number for a physical attack from the current unit state.
    /// Pure preview: mutates no unit, map or game state.
    /// </summary>
    int GetToHitNumber(IUnit attacker, IUnit target, PhysicalAttackType attackType, IBattleMap map);

    /// <summary>
    /// Gets a detailed breakdown of all modifiers affecting a physical attack from the current unit state.
    /// Pure preview: mutates no unit, map or game state.
    /// </summary>
    PhysicalAttackBreakdown GetModifierBreakdown(IUnit attacker, IUnit target, PhysicalAttackType attackType, IBattleMap map);

    /// <summary>
    /// Gets the total to-hit number for a physical attack using a scenario.
    /// Supports hypothetical previews (for UI and bot evaluation).
    /// Pure preview: mutates no unit, map or game state.
    /// </summary>
    int GetToHitNumber(PhysicalAttackScenario scenario, IBattleMap map);

    /// <summary>
    /// Gets a detailed breakdown of all modifiers affecting a physical attack using a scenario.
    /// Supports hypothetical previews (for UI and bot evaluation).
    /// Pure preview: mutates no unit, map or game state.
    /// </summary>
    PhysicalAttackBreakdown GetModifierBreakdown(PhysicalAttackScenario scenario, IBattleMap map);
}

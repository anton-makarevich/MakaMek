using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Interface for calculating physical attack to-hit modifiers, keyed on the attacker's Piloting Skill.
/// Heat and sensor-damage modifiers never apply to physical attacks.
/// <para>
/// A <see cref="PhysicalAttackScenario"/> copies its scalar inputs (Piloting Skill, positions,
/// movement type, target hexes moved and heights) but keeps live <c>AttackerUnit</c>/<c>TargetUnit</c>
/// references. Skidding, target prone status and partial cover are read from those units at
/// evaluation time, so previews do not mutate state but are not isolated snapshots. Scenarios
/// without units omit the modifiers that depend on them.
/// </para>
/// </summary>
public interface IPhysicalAttackCalculator
{
    /// <summary>
    /// Gets the total to-hit number for a physical attack from the current unit state.
    /// Pure preview: mutates no unit, map or game state.
    /// </summary>
    int GetToHitNumber(IUnit attacker, IUnit target, PhysicalAttackType attackType, IBattleMap map);

    /// <summary>
    /// Gets the damage this attack would deal when made with the given limb, after the actuator
    /// reductions of the rules provider have been applied. A push reports zero because it deals no
    /// damage. Pure preview: mutates no unit, map or game state.
    /// </summary>
    int GetDamage(IUnit attacker, PhysicalAttackType attackType, PartLocation limb);

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

    /// <summary>
    /// Gets a detailed breakdown of a charge's to-hit modifiers: the standard physical attack
    /// modifiers plus the relative-piloting modifier and, for a replacement target, the +1
    /// modifier. A charge has no attacking limb. Pure preview: mutates no unit, map or game state.
    /// </summary>
    /// <param name="attacker">The charging unit.</param>
    /// <param name="target">The unit in the charged hex.</param>
    /// <param name="map">The battle map.</param>
    /// <param name="isReplacementTarget">Whether the target replaced the originally declared one.</param>
    PhysicalAttackBreakdown GetChargeModifierBreakdown(
        IUnit attacker,
        IUnit target,
        IBattleMap map,
        bool isReplacementTarget);

    /// <summary>
    /// Gets a detailed breakdown of a movement-origin (Charge or DFA) attack from a preview
    /// scenario: the standard physical attack modifiers plus the relative-piloting modifier.
    /// Pure preview: mutates no unit, map or game state.
    /// </summary>
    /// <param name="scenario">The hypothetical attack scenario.</param>
    /// <param name="map">The battle map.</param>
    PhysicalAttackBreakdown GetMovementAttackModifierBreakdown(PhysicalAttackScenario scenario, IBattleMap map);

    /// <summary>
    /// Gets the total charge to-hit number from the current unit state.
    /// Pure preview: mutates no unit, map or game state.
    /// </summary>
    int GetChargeToHitNumber(IUnit attacker, IUnit target, IBattleMap map, bool isReplacementTarget);

    /// <summary>
    /// Gets a detailed breakdown of a Death From Above's to-hit modifiers: the standard physical
    /// attack modifiers, including the attacker's jumping movement and the target's movement and
    /// status, plus the relative-piloting modifier. Terrain is excluded (Total Warfare p. 144
    /// footnote). Pure preview: mutates no unit, map or game state.
    /// </summary>
    /// <param name="attacker">The jumping unit.</param>
    /// <param name="target">The unit in the target hex.</param>
    /// <param name="map">The battle map.</param>
    PhysicalAttackBreakdown GetDfaModifierBreakdown(IUnit attacker, IUnit target, IBattleMap map);

    /// <summary>
    /// Gets the total Death From Above to-hit number from the current unit state.
    /// Pure preview: mutates no unit, map or game state.
    /// </summary>
    int GetDfaToHitNumber(IUnit attacker, IUnit target, IBattleMap map);
}

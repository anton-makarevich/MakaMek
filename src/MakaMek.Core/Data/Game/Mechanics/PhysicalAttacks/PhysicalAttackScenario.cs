using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

/// <summary>
/// A physical attack to-hit scenario. When it is built it copies the attacker's Piloting Skill,
/// both positions, the attacker movement type, the target hexes moved and both heights, so those
/// values stay consistent for the lifetime of the scenario.
/// <para>
/// <see cref="AttackerUnit"/> and <see cref="TargetUnit"/> are live references, not snapshots. The
/// calculator reads skidding from both units, the target's prone status and the partial-cover
/// inputs from them when it evaluates. A preview therefore does not mutate state, but it is not an
/// isolated snapshot either: if a unit changes before the calculator runs, the result reflects the
/// new state. A scenario without a unit omits the modifiers that depend on that unit.
/// </para>
/// The scenario is keyed on the attacker's Piloting Skill and carries no attacker-modifier list,
/// so heat and sensor-damage modifiers are excluded by construction. It can represent both actual
/// attacks (from the current unit state) and hypothetical attacks (for UI preview and bot
/// evaluation).
/// </summary>
public record PhysicalAttackScenario
{
    /// <summary>
    /// Attacker's piloting skill value (base to-hit for physical attacks)
    /// </summary>
    public required int AttackerPiloting { get; init; }

    /// <summary>
    /// Type of the physical attack (affects the per-type base modifier)
    /// </summary>
    public required PhysicalAttackType AttackType { get; init; }

    /// <summary>
    /// Position from which the attack is made
    /// </summary>
    public required HexPosition AttackerPosition { get; init; }

    /// <summary>
    /// Position of the target being attacked
    /// </summary>
    public required HexPosition TargetPosition { get; init; }

    /// <summary>
    /// Movement type used by the attacker (affects attacker movement modifier)
    /// </summary>
    public required MovementType AttackerMovementType { get; init; }

    /// <summary>
    /// Number of hexes the target has moved (affects target movement modifier)
    /// </summary>
    public required int TargetHexesMoved { get; init; }

    /// <summary>
    /// Height of the attacking unit in levels (used for LOS calculations)
    /// </summary>
    public int AttackerHeight { get; private init; }

    /// <summary>
    /// Height of the target unit in levels (used for LOS calculations)
    /// </summary>
    public int TargetHeight { get; private init; }

    /// <summary>
    /// The target unit, read live when the scenario is evaluated to determine target-side modifiers
    /// (partial cover, skidding, prone status). Optional: without it those modifiers are omitted.
    /// </summary>
    public IUnit? TargetUnit { get; private init; }

    /// <summary>
    /// The attacker unit, read live when the scenario is evaluated to determine attacker-side
    /// modifiers (skidding). Optional: without it those modifiers are omitted.
    /// </summary>
    public IUnit? AttackerUnit { get; private init; }

    /// <summary>
    /// Creates a PhysicalAttackScenario from actual units in their current state.
    /// Used for real combat calculations and to-hit previews.
    /// </summary>
    /// <param name="attacker">The attacking unit</param>
    /// <param name="target">The target unit</param>
    /// <param name="attackType">The type of the physical attack</param>
    /// <returns>PhysicalAttackScenario representing the actual attack</returns>
    /// <exception cref="InvalidOperationException">Thrown if the attacker has no pilot, no position, no movement type, or the target has no position or movement type set</exception>
    public static PhysicalAttackScenario FromUnits(
        IUnit attacker,
        IUnit target,
        PhysicalAttackType attackType)
    {
        if (attacker.Pilot is null)
            throw new InvalidOperationException("Attacker pilot is not assigned");
        if (attacker.Position is null)
            throw new InvalidOperationException("Attacker position is not set");
        if (target.Position is null)
            throw new InvalidOperationException("Target position is not set");
        if (attacker.MovementTaken is null)
            throw new InvalidOperationException("Attacker's Movement Type is undefined");
        // A movement attack (Charge or DFA) can target a unit that has not moved at all this turn,
        // including one that began the turn shut down or with an unconscious pilot. Treat it as
        // stationary.
        if (target.MovementTaken is null && !PhysicalAttackDefinitions.IsMovementAttack(attackType))
            throw new InvalidOperationException("Target's Movement Type is undefined");

        return new PhysicalAttackScenario
        {
            AttackerPiloting = attacker.Pilot.Piloting,
            AttackType = attackType,
            AttackerPosition = attacker.Position,
            TargetPosition = target.Position,
            AttackerMovementType = attacker.MovementTaken.MovementType,
            TargetHexesMoved = target.MovementTaken?.HexesTraveled ?? 0,
            AttackerHeight = attacker.Height,
            TargetHeight = target.Height,
            TargetUnit = target,
            AttackerUnit = attacker
        };
    }

    /// <summary>
    /// Creates a scenario for a hypothetical (preview) attack from an explicit attacker position,
    /// which is what a movement-origin attack needs: the attacker has not moved yet, so its live
    /// position is still the movement origin rather than the approach hex. The movement type and
    /// the target's hexes moved are supplied by the caller instead of being read from the units.
    /// </summary>
    /// <param name="attacker">The attacking unit, read live for attacker-side modifiers.</param>
    /// <param name="target">The target unit, read live for target-side modifiers.</param>
    /// <param name="attackType">The type of the physical attack.</param>
    /// <param name="attackerPosition">The position the attacker would attack from.</param>
    /// <param name="attackerMovementType">The movement type the attack would use.</param>
    /// <param name="targetHexesMoved">The hexes the target has already moved.</param>
    /// <returns>A scenario representing the intended attack.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the attacker has no pilot or the target has no position.</exception>
    public static PhysicalAttackScenario ForPreview(
        IUnit attacker,
        IUnit target,
        PhysicalAttackType attackType,
        HexPosition attackerPosition,
        MovementType attackerMovementType,
        int targetHexesMoved)
    {
        if (attacker.Pilot is null)
            throw new InvalidOperationException("Attacker pilot is not assigned");
        if (target.Position is null)
            throw new InvalidOperationException("Target position is not set");

        return new PhysicalAttackScenario
        {
            AttackerPiloting = attacker.Pilot.Piloting,
            AttackType = attackType,
            AttackerPosition = attackerPosition,
            TargetPosition = target.Position,
            AttackerMovementType = attackerMovementType,
            TargetHexesMoved = targetHexesMoved,
            AttackerHeight = attacker.Height,
            TargetHeight = target.Height,
            TargetUnit = target,
            AttackerUnit = attacker
        };
    }
}

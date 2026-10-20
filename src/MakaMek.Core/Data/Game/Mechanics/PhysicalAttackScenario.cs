using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics;

/// <summary>
/// Represents a physical attack scenario with all parameters needed for to-hit calculation.
/// It is keyed on the attacker's Piloting Skill and carries no attacker-modifier list, so heat and
/// sensor-damage modifiers are excluded by construction. Can represent both actual attacks (from the
/// current unit state) and hypothetical attacks (for UI preview and bot evaluation).
/// The target unit is optional and only used to evaluate target-side modifiers
/// (partial cover, skidding); without it those modifiers cannot be determined.
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
    /// The actual target unit, if available, for evaluating target-side modifiers
    /// (partial cover, skidding).
    /// </summary>
    public IUnit? TargetUnit { get; private init; }

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
        if (target.MovementTaken is null)
            throw new InvalidOperationException("Target's Movement Type is undefined");

        return new PhysicalAttackScenario
        {
            AttackerPiloting = attacker.Pilot.Piloting,
            AttackType = attackType,
            AttackerPosition = attacker.Position,
            TargetPosition = target.Position,
            AttackerMovementType = attacker.MovementTaken.MovementType,
            TargetHexesMoved = target.MovementTaken.HexesTraveled,
            AttackerHeight = attacker.Height,
            TargetHeight = target.Height,
            TargetUnit = target
        };
    }
}

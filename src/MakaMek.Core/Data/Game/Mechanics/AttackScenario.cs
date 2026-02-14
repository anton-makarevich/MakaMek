using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics;

/// <summary>
/// Represents an attack scenario with all parameters needed for to-hit calculation.
/// Can represent both actual attacks (from the current unit state) and hypothetical attacks (for bot evaluation).
/// </summary>
public record AttackScenario
{
    /// <summary>
    /// Attacker's gunnery skill value
    /// </summary>
    public required int AttackerGunnery { get; init; }
    
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
    /// Additional modifiers affecting the attacker (heat, prone, sensors, arm actuators, etc.)
    /// </summary>
    public required IReadOnlyList<RollModifier> AttackerModifiers { get; init; }
    
    /// <summary>
    /// Facing the direction of the attacker (used for secondary target calculations)
    /// </summary>
    public HexDirection? AttackerFacing { get; private init; }
    
    /// <summary>
    /// Whether this is the primary target or a secondary target
    /// </summary>
    public bool IsPrimaryTarget { get; private init; } = true;
    
    /// <summary>
    /// Body part being targeted for aimed shot, if any
    /// </summary>
    public PartLocation? AimedShotTarget { get; private init; }
    
    /// <summary>
    /// Creates an AttackScenario from actual units in their current state.
    /// Used for real combat calculations.
    /// </summary>
    /// <param name="attacker">The attacking unit</param>
    /// <param name="target">The target unit</param>
    /// <param name="weaponLocation">Location where the weapon is mounted (for attack modifiers)</param>
    /// <param name="isPrimaryTarget">Whether this is the primary target</param>
    /// <param name="aimedShotTarget">Body part being targeted for aimed shot, if any</param>
    /// <returns>AttackScenario representing the actual attack</returns>
    /// <exception cref="Exception">Thrown if the attacker has no pilot, no position, or no movement type set</exception>
    public static AttackScenario FromUnits(
        IUnit attacker,
        IUnit target,
        PartLocation weaponLocation,
        bool isPrimaryTarget = true,
        PartLocation? aimedShotTarget = null)
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
            
        return new AttackScenario
        {
            AttackerGunnery = attacker.Pilot.Gunnery,
            AttackerPosition = attacker.Position,
            TargetPosition = target.Position,
            AttackerMovementType = attacker.MovementTaken.MovementType,
            TargetHexesMoved = target.MovementTaken.HexesTraveled,
            AttackerModifiers = attacker.GetAttackModifiers(weaponLocation),
            AttackerFacing = attacker.Facing,
            IsPrimaryTarget = isPrimaryTarget,
            AimedShotTarget = aimedShotTarget
        };
    }
    
    /// <summary>
    /// Creates an AttackScenario for hypothetical attack evaluation.
    /// Used by bots to evaluate potential positions and movement decisions.
    /// </summary>
    /// <param name="attackerGunnery">Attacker's gunnery skill</param>
    /// <param name="attackerPosition">Hypothetical position from which to attack</param>
    /// <param name="attackerMovementType">Hypothetical movement type to be used</param>
    /// <param name="targetPosition">Target's position</param>
    /// <param name="targetHexesMoved">Number of hexes target has moved</param>
    /// <param name="attackerModifiers">Attack modifiers (heat, damage, etc.)</param>
    /// <param name="attackerFacing">Attacker's facing direction</param>
    /// <param name="isPrimaryTarget">Whether this is the primary target</param>
    /// <param name="aimedShotTarget">Body part being targeted for aimed shot, if any</param>
    /// <returns>AttackScenario representing the hypothetical attack</returns>
    public static AttackScenario FromHypothetical(
        int attackerGunnery,
        HexPosition attackerPosition,
        MovementType attackerMovementType,
        HexPosition targetPosition,
        int targetHexesMoved,
        IReadOnlyList<RollModifier> attackerModifiers,
        HexDirection? attackerFacing = null,
        bool isPrimaryTarget = true,
        PartLocation? aimedShotTarget = null)
    {
        return new AttackScenario
        {
            AttackerGunnery = attackerGunnery,
            AttackerPosition = attackerPosition,
            TargetPosition = targetPosition,
            AttackerMovementType = attackerMovementType,
            TargetHexesMoved = targetHexesMoved,
            AttackerModifiers = attackerModifiers,
            AttackerFacing = attackerFacing,
            IsPrimaryTarget = isPrimaryTarget,
            AimedShotTarget = aimedShotTarget
        };
    }
}


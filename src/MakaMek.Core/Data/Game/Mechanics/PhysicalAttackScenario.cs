using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics;

/// <summary>
/// Everything a physical attack's to-hit calculation needs. Separate from AttackScenario rather
/// than a flag on it, because the differences are structural: the base is Piloting rather than
/// Gunnery, there is no range or weapon, and heat and sensor modifiers never apply. Filtering those
/// out here is what "never apply" means in practice, so there is no path that can add them back.
/// </summary>
public record PhysicalAttackScenario
{
    /// <summary>
    /// Attacker's piloting skill value, which is the base to-hit for a physical attack
    /// </summary>
    public required int AttackerPiloting { get; init; }

    /// <summary>
    /// The kind of physical attack being made, which decides the base modifier
    /// </summary>
    public required PhysicalAttackType AttackType { get; init; }

    public required HexPosition AttackerPosition { get; init; }

    public required HexPosition TargetPosition { get; init; }

    /// <summary>
    /// Movement type used by the attacker (affects attacker movement modifier)
    /// </summary>
    public required MovementType AttackerMovementType { get; init; }

    /// <summary>
    /// Attacker modifiers that survive the heat and sensor exclusion: prone, skidding, actuators.
    /// </summary>
    public required IReadOnlyList<RollModifier> AttackerModifiers { get; init; }

    public int AttackerHeight { get; private init; }

    public int TargetHeight { get; private init; }

    /// <summary>
    /// The target unit, where one is available, for the modifiers that read its state.
    /// </summary>
    public IUnit? TargetUnit { get; private init; }

    /// <summary>
    /// Heat and sensor damage never modify a physical attack, so they are dropped here rather than
    /// anywhere downstream. Everything else the unit reports is kept.
    /// </summary>
    internal static IReadOnlyList<RollModifier> WithoutHeatAndSensors(
        IReadOnlyList<RollModifier> modifiers) =>
        modifiers
            .Where(m => m is not SensorHitModifier)
            .Where(m => m is not HeatRollModifier)
            .ToList();

    public static PhysicalAttackScenario FromUnits(
        IUnit attacker,
        IUnit target,
        PhysicalAttackType attackType,
        PartLocation attackerLimb)
    {
        if (attacker.Pilot is null)
            throw new InvalidOperationException("Attacker pilot is not assigned");
        if (attacker.Position is null)
            throw new InvalidOperationException("Attacker position is not set");
        if (target.Position is null)
            throw new InvalidOperationException("Target position is not set");
        if (attacker.MovementTaken is null)
            throw new InvalidOperationException("Attacker's Movement Type is undefined");

        return new PhysicalAttackScenario
        {
            AttackerPiloting = attacker.Pilot.Piloting,
            AttackType = attackType,
            AttackerPosition = attacker.Position,
            TargetPosition = target.Position,
            AttackerMovementType = attacker.MovementTaken.MovementType,
            AttackerModifiers = WithoutHeatAndSensors(attacker.GetAttackModifiers(attackerLimb)),
            AttackerHeight = attacker.Height,
            TargetHeight = target.Height,
            TargetUnit = target
        };
    }

    /// <summary>
    /// A hypothetical scenario, for a bot weighing a position or the UI previewing an attack that
    /// has not been declared. Takes no unit, so it cannot read or change one.
    /// </summary>
    public static PhysicalAttackScenario ForPreview(
        int attackerPiloting,
        PhysicalAttackType attackType,
        HexPosition attackerPosition,
        HexPosition targetPosition,
        MovementType attackerMovementType,
        IReadOnlyList<RollModifier>? attackerModifiers = null,
        int attackerHeight = 2,
        int targetHeight = 2) =>
        new()
        {
            AttackerPiloting = attackerPiloting,
            AttackType = attackType,
            AttackerPosition = attackerPosition,
            TargetPosition = targetPosition,
            AttackerMovementType = attackerMovementType,
            AttackerModifiers = WithoutHeatAndSensors(attackerModifiers ?? []),
            AttackerHeight = attackerHeight,
            TargetHeight = targetHeight
        };
}

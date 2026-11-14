using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Evaluation;

/// <summary>
/// Immutable projection of the unit state that tactical evaluation needs.
/// Evaluators depend on this snapshot instead of the mutable <c>IUnit</c> domain model,
/// which keeps them decoupled from unit/component internals.
/// </summary>
public sealed record UnitSnapshot
{
    /// <summary>
    /// The unit's identity, used for target evaluation keys.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Position of the unit, or the evaluated destination when the snapshot is built for an attacker.
    /// </summary>
    public required HexPosition? Position { get; init; }

    /// <summary>
    /// The path the unit took (or would take) to reach <see cref="Position"/>.
    /// </summary>
    public required MovementPath? MovementPath { get; init; }

    /// <summary>
    /// Unit height in levels, used for line-of-sight calculations.
    /// </summary>
    public required int Height { get; init; }

    /// <summary>
    /// Whether the unit is prone, used for target modifiers.
    /// </summary>
    public required bool IsProne { get; init; }

    /// <summary>
    /// The pilot's gunnery skill, or <c>null</c> when the unit has no pilot.
    /// </summary>
    public required int? Gunnery { get; init; }

    /// <summary>
    /// The weapons considered by evaluation (available weapons, or every available weapon for an attacker).
    /// </summary>
    public required IReadOnlyList<WeaponSnapshot> Weapons { get; init; }

    /// <summary>
    /// Weapon configurations to evaluate for an attacker (no rotation plus torso rotations).
    /// Empty for units evaluated as targets or defenders.
    /// </summary>
    public required IReadOnlyList<WeaponConfiguration> Configurations { get; init; }

    /// <summary>
    /// Weapon configuration types each body part supports at the evaluated position.
    /// </summary>
    public required IReadOnlyDictionary<PartLocation, HashSet<WeaponConfigurationType>> ConfigurationTypesByPart
    {
        get;
        init;
    }
}

/// <summary>
/// Immutable projection of a single weapon's evaluation-relevant state.
/// </summary>
public sealed record WeaponSnapshot
{
    /// <summary>
    /// The weapon instance, passed through to the to-hit calculator for weapon-specific rules.
    /// </summary>
    public required Weapon Weapon { get; init; }

    /// <summary>
    /// Damage per hit.
    /// </summary>
    public required int Damage { get; init; }

    /// <summary>
    /// Long range bracket of the weapon, or <c>null</c> when it cannot fire.
    /// </summary>
    public required int? LongRange { get; init; }

    /// <summary>
    /// Location the weapon is mounted on, or <c>null</c> when it is not mounted.
    /// </summary>
    public required PartLocation? MountLocation { get; init; }

    /// <summary>
    /// Facing of the mounting part, used when evaluation does not supply a facing.
    /// </summary>
    public required HexDirection? MountFacing { get; init; }

    /// <summary>
    /// Firing arcs the weapon can cover from its mounting part.
    /// </summary>
    public required IReadOnlyList<FiringArc> FiringArcs { get; init; }

    /// <summary>
    /// Attack modifiers that apply when this weapon fires (heat, prone, actuators, etc.).
    /// </summary>
    public required IReadOnlyList<RollModifier> AttackModifiers { get; init; }
}

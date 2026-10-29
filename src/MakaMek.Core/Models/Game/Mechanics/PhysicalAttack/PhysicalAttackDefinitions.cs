using System.Diagnostics.CodeAnalysis;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// The single source of truth for which physical attacks exist and which limbs they use.
/// It holds structural metadata only; eligibility policy (arcs, actuators, level differences)
/// remains replaceable through <see cref="IPhysicalAttackEligibility"/>.
/// </summary>
public static class PhysicalAttackDefinitions
{
    private static readonly IReadOnlyDictionary<PhysicalAttackType, PhysicalAttackDefinition> Definitions =
        new Dictionary<PhysicalAttackType, PhysicalAttackDefinition>
        {
            [PhysicalAttackType.Punch] = new(
                PhysicalAttackType.Punch,
                [PartLocation.LeftArm, PartLocation.RightArm],
                MaxLimbsPerDeclaration: 2),
            [PhysicalAttackType.Kick] = new(
                PhysicalAttackType.Kick,
                [PartLocation.LeftLeg, PartLocation.RightLeg],
                MaxLimbsPerDeclaration: 1),
            [PhysicalAttackType.Push] = new(
                PhysicalAttackType.Push,
                [PartLocation.LeftArm, PartLocation.RightArm],
                MaxLimbsPerDeclaration: 2,
                MinLimbsPerDeclaration: 2,
                ResolvesAsSingleAttack: true)
        };

    /// <summary>
    /// The physical attacks that are currently implemented.
    /// </summary>
    public static IReadOnlyCollection<PhysicalAttackDefinition> Supported { get; } =
        Definitions.Values.ToList();

    /// <summary>
    /// Whether the attack displaces its target, so it shares the one-displacement-per-target
    /// limit with Charge and DFA (Total Warfare, p. 148-149).
    /// </summary>
    public static bool IsDisplacementAttack(PhysicalAttackType attackType) =>
        attackType is PhysicalAttackType.Push or PhysicalAttackType.Charge or PhysicalAttackType.DFA;

    public static bool TryGet(
        PhysicalAttackType attackType,
        [NotNullWhen(true)] out PhysicalAttackDefinition? definition) =>
        Definitions.TryGetValue(attackType, out definition);

    /// <summary>
    /// Gets the definition of a supported attack.
    /// </summary>
    /// <exception cref="NotSupportedException">The attack type is not implemented yet.</exception>
    public static PhysicalAttackDefinition Get(PhysicalAttackType attackType) =>
        TryGet(attackType, out var definition)
            ? definition
            : throw new NotSupportedException(
                $"Physical attack resolution is not supported for attack type: {attackType}");
}

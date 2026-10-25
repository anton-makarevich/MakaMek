using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Structural metadata for a supported physical attack: which limbs can make it and how many of
/// them a single declaration may use. Dynamic policy such as arcs, actuator prerequisites and
/// level differences stays in <see cref="IPhysicalAttackEligibility"/>.
/// </summary>
/// <param name="AttackType">The attack type described.</param>
/// <param name="CandidateLimbs">The limbs that can make the attack.</param>
/// <param name="MaxLimbsPerDeclaration">The largest number of limbs one declaration may use.</param>
public record PhysicalAttackDefinition(
    PhysicalAttackType AttackType,
    IReadOnlyList<PartLocation> CandidateLimbs,
    int MaxLimbsPerDeclaration);

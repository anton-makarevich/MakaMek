using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Structural metadata for a supported physical attack: which limbs can make it, how many of
/// them a single declaration may use, and whether the declared limbs resolve as one attack or
/// one attack each. Dynamic policy such as arcs, actuator prerequisites and level differences
/// stays in <see cref="IPhysicalAttackEligibility"/>.
/// </summary>
/// <param name="AttackType">The attack type described.</param>
/// <param name="CandidateLimbs">The limbs that can make the attack.</param>
/// <param name="MaxLimbsPerDeclaration">The largest number of limbs one declaration may use.</param>
/// <param name="MinLimbsPerDeclaration">
/// The smallest number of limbs a valid declaration needs. Push requires both arms; Punch and
/// Kick keep the default of one limb.
/// </param>
/// <param name="ResolvesAsSingleAttack">
/// True when the declared limbs act together and produce one resolution: Push uses both arms for
/// a single attack, while a two-arm Punch still resolves one attack per arm.
/// </param>
public record PhysicalAttackDefinition(
    PhysicalAttackType AttackType,
    IReadOnlyList<PartLocation> CandidateLimbs,
    int MaxLimbsPerDeclaration,
    int MinLimbsPerDeclaration = 1,
    bool ResolvesAsSingleAttack = false);

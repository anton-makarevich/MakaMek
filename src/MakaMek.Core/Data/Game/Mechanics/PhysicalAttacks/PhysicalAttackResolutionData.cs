using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

/// <summary>
/// The result of resolving one physical attack limb against one target. It wraps the weapon
/// shaped <see cref="AttackResolutionData"/> so the standard damage pipeline accepts it, and adds
/// the physical attack specifics: the attacking limb, the damage value and the piloting skill
/// roll requirements the attack creates.
/// </summary>
/// <param name="AttackType">The kind of physical attack that was resolved.</param>
/// <param name="AttackerLimb">The attacking limb used for this resolution.</param>
/// <param name="Damage">The damage value of the attack, derived from the attacker's tonnage.</param>
/// <param name="AttackResolution">The weapon shaped resolution data with the roll and the hit location.</param>
/// <param name="PsrRequirements">
/// The piloting skill rolls this attack requires, selected by the rules provider from the attack
/// type and the hit result. The list is empty when the attack imposes no obligation.
/// </param>
public record PhysicalAttackResolutionData(
    PhysicalAttackType AttackType,
    PartLocation AttackerLimb,
    int Damage,
    AttackResolutionData AttackResolution,
    IReadOnlyList<PhysicalAttackPsrRequirement> PsrRequirements);

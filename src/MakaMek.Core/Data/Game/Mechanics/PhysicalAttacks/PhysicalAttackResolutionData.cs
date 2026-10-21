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
/// <param name="RequiresTargetPsr">
/// True when the target has to make a piloting skill roll because of this attack. A successful
/// kick requires a target piloting skill roll. Every miss leaves this false.
/// </param>
/// <param name="RequiresAttackerPsr">
/// True when the attacker has to make a piloting skill roll because of this attack. A missed
/// kick requires an attacker piloting skill roll.
/// </param>
public record PhysicalAttackResolutionData(
    PhysicalAttackType AttackType,
    PartLocation AttackerLimb,
    int Damage,
    AttackResolutionData AttackResolution,
    bool RequiresTargetPsr,
    bool RequiresAttackerPsr);

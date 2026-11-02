using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// One candidate limb of a physical attack and, when the limb cannot be used, the reason why.
/// Unlike <see cref="IPhysicalAttackEligibility.GetEligibleLimbs"/>, this keeps the limbs that
/// fail so the declaration dialog can show them disabled with an explanation instead of hiding
/// them.
/// </summary>
public readonly record struct PhysicalAttackLimbEligibility(
    PartLocation Limb,
    PhysicalAttackRestriction? Restriction)
{
    /// <summary>
    /// Whether the limb can make the attack. A null <see cref="Restriction"/> means it can.
    /// </summary>
    public bool IsAvailable => Restriction is null;
}

using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// The one definition of who can make which physical attack against whom. Declaration validation,
/// the physical attack phase's turn order and the UI's action list all ask this, so they cannot
/// disagree.
/// </summary>
public interface IPhysicalAttackEligibility
{
    /// <summary>
    /// The limbs the attacker could use for this attack type against this target right now. Empty
    /// when the attack is not possible at all. A punch returns arms, a kick returns legs.
    /// </summary>
    IReadOnlyList<PartLocation> GetEligibleLimbs(IUnit attacker, IUnit target, PhysicalAttackType attackType,
        IBattleMap map);

    /// <summary>
    /// Whether an attack the attacker has already declared can still be made with this limb: the
    /// same checks as <see cref="GetEligibleLimbs"/>, apart from the one attack per turn rule.
    /// The resolution phase asks this before any damage of the phase is applied.
    /// </summary>
    bool CanResolve(IUnit attacker, IUnit target, PhysicalAttackType attackType, PartLocation limb, IBattleMap map);

    /// <summary>
    /// Whether the attacker has at least one physical attack against at least one of the units,
    /// whatever the odds of hitting.
    /// </summary>
    bool HasAnyOption(IUnit attacker, IEnumerable<IUnit> units, IBattleMap map);
}

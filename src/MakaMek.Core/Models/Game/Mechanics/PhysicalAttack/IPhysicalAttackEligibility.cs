using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// The one definition of who can make which physical attack against whom. Declaration validation,
/// the physical attack phase's turn order and the UI's action list all ask this, so they cannot
/// disagree.
/// <para>
/// This interface is the construction-time replaceable eligibility policy. It is registered as a
/// singleton in <c>CoreServices</c> and <c>BotContainerServices</c>, and the declaration phase,
/// the resolution phase and the resolution gates consume the instance owned by the game. Replacing
/// the registration therefore replaces the policy without touching the mechanics.
/// </para>
/// <para>
/// Structural metadata (which limbs a supported attack can use and how many limbs one declaration
/// may select) lives in <see cref="PhysicalAttackDefinitions"/>; this interface owns the dynamic
/// policy that depends on the current board state.
/// </para>
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

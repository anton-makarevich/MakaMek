namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Why a physical attack is not available, so the UI can tell the player instead of silently
/// hiding the action. Every supported attack type can report a reason; a declaration-time
/// restriction such as <see cref="TargetAlreadyDisplaced"/> comes from
/// <see cref="IPhysicalAttackEligibility.GetDeclarationRestriction"/>.
/// </summary>
public enum PhysicalAttackRestriction
{
    /// <summary>No unit occupies the hex directly in front of the attacker's feet.</summary>
    NoTargetInFront,

    /// <summary>The unit in front is not a standing, living Mech.</summary>
    TargetNotStandingMech,

    /// <summary>The target is carrying out a Charge or a Death From Above attack this turn.</summary>
    TargetPerformingDisplacementAttack,

    /// <summary>
    /// The target is carrying out a Death From Above attack this turn, so its movement is not
    /// finished and it cannot be the target of any physical attack (Total Warfare p. 149).
    /// </summary>
    TargetPerformingDeathFromAbove,

    /// <summary>The attacker and the target do not stand at the same elevation.</summary>
    ElevationMismatch,

    /// <summary>The target is fully submerged and the attack would not begin underwater.</summary>
    TargetSubmerged,

    /// <summary>The attacker is missing an arm or one of its arms was destroyed.</summary>
    ArmMissing,

    /// <summary>An arm-mounted weapon was fired this turn.</summary>
    ArmWeaponFired,

    /// <summary>Another unit already declared a displacement attack against the target this turn.</summary>
    TargetAlreadyDisplaced,

    /// <summary>
    /// A Punch or Kick cannot be made at all: the attacker is not a Mech able to attack, the
    /// target is not a living adjacent enemy Mech, or a position cannot be resolved on the map.
    /// </summary>
    TargetOutOfReach,

    /// <summary>The target is outside the punch (torso) or kick (feet) arc.</summary>
    TargetOutsideArc,

    /// <summary>The level difference is outside the punch or kick range.</summary>
    LevelDifferenceOutOfRange,

    /// <summary>The level difference would be allowed against a standing target, but not a prone one.</summary>
    TargetProne,

    /// <summary>The punching arm is missing or destroyed.</summary>
    ArmDestroyed,

    /// <summary>The punching arm's shoulder actuator is missing or destroyed.</summary>
    ShoulderActuatorDestroyed,

    /// <summary>The kicking leg is missing or destroyed.</summary>
    LegDestroyed,

    /// <summary>A kicking leg's hip actuator is missing or destroyed.</summary>
    HipActuatorDestroyed,

    /// <summary>A weapon mounted in the kicking leg fired this turn.</summary>
    LegWeaponFired
}

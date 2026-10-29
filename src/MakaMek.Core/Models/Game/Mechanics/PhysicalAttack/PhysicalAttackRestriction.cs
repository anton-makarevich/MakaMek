namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Why a physical attack is not available, so the UI can tell the player instead of silently
/// hiding the action. The displacement budget is checked separately and reports
/// <see cref="TargetAlreadyDisplaced"/>.
/// </summary>
public enum PhysicalAttackRestriction
{
    /// <summary>No unit occupies the hex directly in front of the attacker's feet.</summary>
    NoTargetInFront,

    /// <summary>The unit in front is not a standing, living Mech.</summary>
    TargetNotStandingMech,

    /// <summary>The target is carrying out a Charge or a Death From Above attack this turn.</summary>
    TargetPerformingDisplacementAttack,

    /// <summary>The attacker and the target do not stand at the same elevation.</summary>
    ElevationMismatch,

    /// <summary>The attacker is missing an arm or one of its arms was destroyed.</summary>
    ArmMissing,

    /// <summary>An arm-mounted weapon was fired this turn.</summary>
    ArmWeaponFired,

    /// <summary>Another unit already declared a displacement attack against the target this turn.</summary>
    TargetAlreadyDisplaced
}

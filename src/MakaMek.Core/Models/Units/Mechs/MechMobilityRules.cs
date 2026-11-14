using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

/// <summary>
/// The 'Mech mobility rules: movement points by type, whether it can run, jump or stand up, and
/// the piloting-skill roll jumping calls for after actuator or gyro damage. Extracted from
/// <see cref="Mech"/> so the mech only exposes the values and this class owns the rules.
/// </summary>
internal static class MechMobilityRules
{
    /// <summary>
    /// Gets the movement points available for a movement type, after the points spent so far.
    /// </summary>
    public static int GetMovementPoints(Mech mech, MovementType type, int modifiedMovement)
    {
        var baseMp = type switch
        {
            MovementType.Walk => modifiedMovement,
            MovementType.Run => (int)Math.Ceiling(modifiedMovement * 1.5),
            MovementType.Jump => mech.GetAvailableComponents<JumpJets>().Sum(j => j.JumpMp),
            _ => 0
        };
        return Math.Max(0, baseMp - mech.MovementPointsSpent);
    }

    public static bool CanJump(Mech mech)
    {
        // Cannot jump if currently prone
        if (mech.IsProne) return false;

        // Cannot jump if the mech stood up in this phase
        if (mech.StandupAttempts > 0) return false;

        // Cannot jump if no functional jump jets are available
        return mech.GetMovementPoints(MovementType.Jump) >= 1;
    }

    public static bool CanRun(Mech mech)
    {
        var destroyedLegs = mech.Parts.Values.OfType<Leg>().Count(p => p.IsDestroyed);
        return destroyedLegs <= 0;
    }

    public static bool CanStandup(Mech mech)
    {
        if (mech.IsShutdown) return false;

        if (mech.HasMoved) return false;

        // Cannot stand up in the same turn if falling during a jump
        if (mech.MovementTaken?.MovementType == MovementType.Jump)
            return false;

        // Cannot stand up when skidding
        if (mech.IsSkidding) return false;

        if (!IsGyroAvailable(mech)) return false;

        var destroyedLegs = mech.Parts.Values.OfType<Leg>().Count(p => p.IsDestroyed);
        if (destroyedLegs >= 2) return false;

        // Check if the Mech has at least the effective standup cost worth of movement points
        if (mech.GetMovementPoints(mech.MovementTaken?.MovementType ?? MovementType.Walk) < mech.EffectiveStandupCost)
            return false;

        if (mech.Pilot?.IsConscious == false) return false;

        return true;
    }

    /// <summary>
    /// Determines if a piloting skill roll is required for jumping due to damage.
    /// </summary>
    public static bool IsPsrForJumpRequired(Mech mech)
    {
        var destroyedFootActuators = mech.GetAllComponents<FootActuator>()
            .Where(a => a.IsDestroyed);
        if (destroyedFootActuators.Any()) return true;
        var destroyedHipActuators = mech.GetAllComponents<HipActuator>()
            .Where(a => a.IsDestroyed);
        if (destroyedHipActuators.Any()) return true;
        var destroyedLowerLegActuators = mech.GetAllComponents<LowerLegActuator>()
            .Where(a => a.IsDestroyed);
        if (destroyedLowerLegActuators.Any()) return true;
        var destroyedUpperLegActuators = mech.GetAllComponents<UpperLegActuator>()
            .Where(a => a.IsDestroyed);
        if (destroyedUpperLegActuators.Any()) return true;

        var gyro = mech.GetAvailableComponents<Gyro>().FirstOrDefault();
        return gyro?.Hits == 1;
    }

    /// <summary>
    /// Determines if the mech can change its facing while prone.
    /// </summary>
    public static bool CanChangeFacingWhileProne(Mech mech)
    {
        // Must be prone to use this action
        if (!mech.IsProne) return false;

        // Cannot change facing if shutdown
        if (mech.IsShutdown) return false;

        // Must have at least 1 movement point available
        if (mech.GetMovementPoints(MovementType.Walk) < 1) return false;

        return true;
    }

    public static bool IsGyroAvailable(Mech mech) =>
        mech.GetAvailableComponents<Gyro>().FirstOrDefault() != null;
}


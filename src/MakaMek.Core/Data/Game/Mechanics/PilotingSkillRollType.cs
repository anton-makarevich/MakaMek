namespace Sanet.MakaMek.Core.Data.Game.Mechanics;

/// <summary>
/// Defines the types of events or conditions that can trigger a Piloting Skill Roll (PSR)
/// or modify its difficulty.
/// </summary>
public enum PilotingSkillRollType
{
    /// <summary>
    /// PSR modifiers due to a damaged or destroyed gyro.
    /// </summary>
    GyroHit,
    GyroDestroyed,
    /// <summary>
    /// PSR for determining if a MechWarrior takes damage when a mech falls.
    /// </summary>
    PilotDamageFromFall,

    /// <summary>
    /// PSR modifier due to a critical hit on a lower leg actuator.
    /// </summary>
    LowerLegActuatorHit,
    
    /// <summary>
    /// PSR required when a 'Mech takes 20 or more damage points in a single phase.
    /// </summary>
    HeavyDamage,

    /// <summary>
    /// PSR modifier due to a critical hit on a hip actuator.
    /// </summary>
    HipActuatorHit,

    /// <summary>
    /// PSR modifier due to a critical hit on a foot actuator.
    /// </summary>
    FootActuatorHit,

    /// <summary>
    /// PSR modifier due to a leg being destroyed (for pilot damage during fall).
    /// </summary>
    LegDestroyed,

    // Add other PSR types here in the future, e.g.:
    // Shutdown,
    // EnteringDeepWater,
    // Skid
    StandupAttempt,
    JumpWithDamage
}

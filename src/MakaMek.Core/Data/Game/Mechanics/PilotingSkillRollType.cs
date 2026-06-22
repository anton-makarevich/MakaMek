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
    /// PSR modifier due to a critical hit on a upper leg actuator.
    /// </summary>
    UpperLegActuatorHit,
    
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

    /// <summary>
    /// Automatic fall when a bridge collapses under a unit's weight.
    /// </summary>
    BridgeCollapse,

    SkidCheck,
    StandupAttempt,
    JumpWithDamage,

    /// <summary>
    /// PSR required when a 'Mech enters water hex.
    /// </summary>
    WaterEntry,

    /// <summary>
    /// PSR required when a 'Mech enters a rubble hex.
    /// </summary>
    RubbleEntry,

    /// <summary>
    /// Automatic fall when a mech skids off a cliff edge.
    /// </summary>
    CliffFall
}

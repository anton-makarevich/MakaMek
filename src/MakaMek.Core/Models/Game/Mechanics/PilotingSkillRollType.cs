namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Defines the types of events or conditions that can trigger a Piloting Skill Roll (PSR)
/// or modify its difficulty.
/// </summary>
public enum PilotingSkillRollType
{
    /// <summary>
    /// PSR modifier due to a damaged or destroyed gyro.
    /// </summary>
    GyroHit,
    
    /// <summary>
    /// PSR for determining if a MechWarrior takes damage when a mech falls.
    /// </summary>
    WarriorDamageFromFall
    // Add other PSR types here in the future, e.g.:
    // ActuatorDamage,
    // PilotDamage,
    // LegDamage,
    // Shutdown,
    // AttemptingToStand,
    // EnteringDeepWater,
    // Skid
}

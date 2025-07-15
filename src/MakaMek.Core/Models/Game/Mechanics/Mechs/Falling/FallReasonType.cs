using Sanet.MakaMek.Core.Data.Game.Mechanics;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;

/// <summary>
/// Represents the different reasons a 'Mech might fall
/// </summary>
public enum FallReasonType
{
    /// <summary>
    /// Fall due to gyro hit (requires PSR)
    /// </summary>
    GyroHit,
    
    /// <summary>
    /// Fall due to lower leg actuator hit (requires PSR)
    /// </summary>
    LowerLegActuatorHit,

    /// <summary>
    /// Fall due to hip actuator hit (requires PSR)
    /// </summary>
    HipActuatorHit,

    /// <summary>
    /// Fall due to foot actuator hit (requires PSR)
    /// </summary>
    FootActuatorHit,

    /// <summary>
    /// Fall due to heavy damage (requires PSR)
    /// </summary>
    HeavyDamage,
    
    /// <summary>
    /// Automatic fall due to destroyed gyro (no PSR)
    /// </summary>
    GyroDestroyed,
    
    /// <summary>
    /// Automatic fall due to destroyed leg (no PSR)
    /// </summary>
    LegDestroyed,
    
    /// <summary>
    /// Attempt to stand up from prone position (requires PSR)
    /// </summary>
    StandUpAttempt,

    /// <summary>
    /// Jump attempt with damaged gyro or foot/leg/hip actuators (requires PSR)
    /// </summary>
    JumpWithDamage,
    UpperLegActuatorHit
}

/// <summary>
/// Extension methods for the FallReasonType enum
/// </summary>
public static class FallReasonTypeExtensions
{
    /// <summary>
    /// Maps a FallReasonType to the corresponding PilotingSkillRollType for PSR calculations
    /// </summary>
    /// <param name="reasonType">The fall reason type to map</param>
    /// <returns>The corresponding PilotingSkillRollType</returns>
    public static PilotingSkillRollType? ToPilotingSkillRollType(this FallReasonType reasonType)
    {
        return reasonType switch
        {
            FallReasonType.GyroHit => PilotingSkillRollType.GyroHit,
            FallReasonType.LowerLegActuatorHit => PilotingSkillRollType.LowerLegActuatorHit,
            FallReasonType.UpperLegActuatorHit => PilotingSkillRollType.UpperLegActuatorHit,
            FallReasonType.HipActuatorHit => PilotingSkillRollType.HipActuatorHit,
            FallReasonType.FootActuatorHit => PilotingSkillRollType.FootActuatorHit,
            FallReasonType.HeavyDamage => PilotingSkillRollType.HeavyDamage,
            FallReasonType.StandUpAttempt => PilotingSkillRollType.StandupAttempt,
            FallReasonType.JumpWithDamage => PilotingSkillRollType.JumpWithDamage,
            _ => null // PSR is not required for that reason
        };
    }
    
    /// <summary>
    /// Determines whether a PSR is required for this fall reason
    /// </summary>
    /// <param name="reasonType">The fall reason type to check</param>
    /// <returns>True if a PSR is required, false if it's an automatic fall</returns>
    public static bool RequiresPilotingSkillRoll(this FallReasonType reasonType)
    {
        return reasonType.ToPilotingSkillRollType()!=null;
    }
}

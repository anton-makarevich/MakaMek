using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;

/// <summary>
/// Contains all context data related to a unit's fall or standup attempt
/// </summary>
public record FallContextData
{
    /// <summary>
    /// The unit that is falling or attempting to stand up
    /// </summary>
    public required Guid UnitId { get; init; }
    
    /// <summary>
    /// The game instance
    /// </summary>
    public required Guid GameId { get; init; }
    
    /// <summary>
    /// Whether the unit is falling (true)
    /// </summary>
    public bool IsFalling { get; init; }
    
    /// <summary>
    /// The reason for the fall check
    /// </summary>
    public required FallReasonType ReasonType { get; init; }
    
    /// <summary>
    /// The piloting skill roll data for the fall check
    /// </summary>
    public PilotingSkillRollData? PilotingSkillRoll { get; init; }
    
    /// <summary>
    /// The piloting skill roll data for pilot damage check (only applies if IsFalling is true)
    /// </summary>
    public PilotingSkillRollData? PilotDamagePilotingSkillRoll { get; init; }
    
    /// <summary>
    /// The falling damage data (only applies if IsFalling is true)
    /// </summary>
    public FallingDamageData? FallingDamageData { get; init; }
    
    /// <summary>
    /// The number of levels the unit fell (only applies if IsFalling is true)
    /// </summary>
    public int LevelsFallen { get; init; }
    
    /// <summary>
    /// Whether the unit was jumping when it fell (only applies if IsFalling is true)
    /// </summary>
    public bool WasJumping { get; init; }
    
    public MechFallCommand ToMechFallCommand()
    {
        // Convert FallContextData to MechFallCommand
        return new MechFallCommand
        {
            UnitId = UnitId,
            GameOriginId = GameId,
            Timestamp = DateTime.UtcNow,
            LevelsFallen = LevelsFallen,
            WasJumping = WasJumping,
            DamageData = FallingDamageData,
            FallPilotingSkillRoll = PilotingSkillRoll,
            PilotDamagePilotingSkillRoll = PilotDamagePilotingSkillRoll
        };
    }

    public MechStandUpCommand? ToMechStandUpCommand()
    {
        if (IsFalling || PilotingSkillRoll is null)
            return null;
            
        return new MechStandUpCommand
        {
            UnitId = UnitId,
            GameOriginId = GameId,
            Timestamp = DateTime.UtcNow,
            PilotingSkillRoll = PilotingSkillRoll
        };
    }
}

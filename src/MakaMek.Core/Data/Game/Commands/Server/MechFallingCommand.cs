using System.Text;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

/// <summary>
/// Command for applying falling damage to a mech
/// </summary>
public record struct MechFallingCommand : IGameCommand
{
    /// <summary>
    /// The ID of the mech that fell
    /// </summary>
    public required Guid UnitId { get; init; }

    /// <summary>
    /// The number of levels the mech fell
    /// </summary>
    public int LevelsFallen { get; set; } 

    /// <summary>
    /// Whether the mech was jumping when it fell
    /// </summary>
    public bool WasJumping { get; set; } 
    
    /// <summary>
    /// The falling damage data
    /// </summary>
    public required FallingDamageData? DamageData { get; init; }

    /// <summary>
    /// Whether a piloting skill roll is required for this fall
    /// </summary>
    public bool IsPilotingSkillRollRequired => FallPilotingSkillRoll != null;
    
    /// <summary>
    /// Whether the pilot is taking damage from the fall
    /// </summary>
    public bool IsPilotTakingDamage => PilotDamagePilotingSkillRoll != null;

    /// <summary>
    /// The piloting skill roll data for the fall check
    /// </summary>
    public PilotingSkillRollData? FallPilotingSkillRoll { get; set; }

    /// <summary>
    /// The piloting skill roll data for pilot damage check
    /// </summary>
    public PilotingSkillRollData? PilotDamagePilotingSkillRoll { get; set; }

    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var unit = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == command.UnitId);
            
        if (unit == null)
        {
            return string.Empty;
        }
        
        var stringBuilder = new StringBuilder();
        
        // Render the fall PSR details if it exists, regardless of outcome.
        if (FallPilotingSkillRoll != null)
        {
            stringBuilder.AppendLine(FallPilotingSkillRoll.Render(localizationService));
        }
        
        // Check if an actual fall occurred (indicated by damage or levels fallen)
        if (DamageData!=null && FallPilotingSkillRoll?.IsSuccessful==false)
        {
            // Base message about falling
            stringBuilder.Append(string.Format(
                localizationService.GetString("Command_MechFalling_Base"),
                unit.Name));
            
            // Add levels fallen if applicable
            if (LevelsFallen > 0)
            {
                stringBuilder.Append(string.Format(
                    localizationService.GetString("Command_MechFalling_Levels"),
                    LevelsFallen));
            }
            
            // Add jumping status if applicable
            if (WasJumping)
            {
                stringBuilder.Append(localizationService.GetString("Command_MechFalling_Jumping"));
            }
            
            // Add damage information
            stringBuilder.Append(string.Format(
                localizationService.GetString("Command_MechFalling_Damage"),
                DamageData.HitLocations.TotalDamage));
            
            // Add pilot injury information if applicable
            if (IsPilotTakingDamage && PilotDamagePilotingSkillRoll != null)
            {
                stringBuilder.Append(localizationService.GetString("Command_MechFalling_PilotInjury"));
                // If there's a pilot damage PSR, render it
                stringBuilder.Append(PilotDamagePilotingSkillRoll.Render(localizationService));
            }
        }
        else 
        {
            // PSR was successful, and no fall occurred.
            string key = FallPilotingSkillRoll.RollType switch
            {
                PilotingSkillRollType.GyroHit => "Command_PsrSuccess_Gyro",
                PilotingSkillRollType.LowerLegActuatorHit => "Command_PsrSuccess_LowerLegActuator",
                // Add other specific success messages as needed
                _ => "Command_PsrSuccess_General"
            };
            stringBuilder.Append(string.Format(
                localizationService.GetString(key),
                unit.Name));
        }
        // If FallPilotingSkillRoll is null but actualFallOccurred is true (e.g. auto-fall without PSR like gyro destroyed)
        // the existing logic for actualFallOccurred handles this.
        // If FallPilotingSkillRoll is null and actualFallOccurred is false, nothing specific to render here for this command's purpose.

        return stringBuilder.ToString().TrimEnd();
    }
}

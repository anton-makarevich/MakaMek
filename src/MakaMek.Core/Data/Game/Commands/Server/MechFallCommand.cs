using System.Text;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

/// <summary>
/// Command for applying falling damage to a mech
/// </summary>
public record struct MechFallCommand : IGameCommand
{
    /// <summary>
    /// The ID of the mech that fell
    /// </summary>
    public required Guid UnitId { get; init; }

    /// <summary>
    /// The number of levels the mech fell
    /// </summary>
    public int LevelsFallen { get; init; } 

    /// <summary>
    /// Whether the mech was jumping when it fell
    /// </summary>
    public bool WasJumping { get; init; } 
    
    /// <summary>
    /// The falling damage data
    /// </summary>
    public required FallingDamageData? DamageData { get; init; }

    /// <summary>
    /// Whether a piloting skill roll is required for this fall
    /// </summary>
    public bool IsPilotingSkillRollRequired => FallPilotingSkillRoll is not null;
    
    /// <summary>
    /// Whether the pilot is taking damage from the fall
    /// </summary>
    public bool IsPilotTakingDamage => PilotDamagePilotingSkillRoll is { IsSuccessful: false };

    /// <summary>
    /// The piloting skill roll data for the fall check
    /// </summary>
    public PilotingSkillRollData? FallPilotingSkillRoll { get; init; }

    /// <summary>
    /// The piloting skill roll data for pilot damage check
    /// </summary>
    public PilotingSkillRollData? PilotDamagePilotingSkillRoll { get; init; }

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
        if (DamageData == null)
            return stringBuilder.ToString().TrimEnd();
        // Base message about falling
        stringBuilder.Append(string.Format(
            localizationService.GetString("Command_MechFalling_Base"),
            unit.Name));
            
        if (LevelsFallen > 0)
        {
            stringBuilder.Append(string.Format(
                localizationService.GetString("Command_MechFalling_Levels"),
                LevelsFallen));
        }
            
        if (WasJumping)
        {
            stringBuilder.Append(localizationService.GetString("Command_MechFalling_Jumping"));
        }
            
        stringBuilder.Append(string.Format(
            localizationService.GetString("Command_MechFalling_Damage"),
            DamageData.HitLocations.TotalDamage));
            
        // Add detailed hit locations information - using the location's Render method
        if (DamageData.HitLocations.HitLocations.Count > 0)
        {
            stringBuilder.AppendLine(); // Add a line break after total damage
            stringBuilder.AppendLine(localizationService.GetString("Command_WeaponAttackResolution_HitLocations"));

            // Render each hit location using the new method
            foreach (var hitLocation in DamageData.HitLocations.HitLocations)
            {
                stringBuilder.Append(hitLocation.Render(localizationService, unit));
            }
        }

        if (PilotDamagePilotingSkillRoll == null) return stringBuilder.ToString().TrimEnd();
        stringBuilder.AppendLine(PilotDamagePilotingSkillRoll.Render(localizationService));
        if (!PilotDamagePilotingSkillRoll.IsSuccessful)
        {
            stringBuilder.Append(localizationService.GetString("Command_MechFalling_PilotInjury"));
        }

        return stringBuilder.ToString().TrimEnd();
    }
}

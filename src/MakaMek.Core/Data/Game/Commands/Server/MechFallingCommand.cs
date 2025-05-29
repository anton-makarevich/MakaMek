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
    public required FallingDamageData DamageData { get; init; }

    /// <summary>
    /// Whether a piloting skill roll is required for this fall
    /// </summary>
    public bool IsPilotingSkillRollRequired { get; set; }
    
    public bool IsPilotTakingDamage { get; set; }

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
        
        // If there's a fall PSR, render it first
        if (IsPilotingSkillRollRequired && FallPilotingSkillRoll !=null)
        {
            stringBuilder.AppendLine(FallPilotingSkillRoll.Render(localizationService));
            stringBuilder.AppendLine();
        }
        
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
        if (!IsPilotTakingDamage) return stringBuilder.ToString();
        stringBuilder.Append(localizationService.GetString("Command_MechFalling_PilotInjury"));
            
        // If there's a pilot damage PSR, render it
        if (PilotDamagePilotingSkillRoll == null) return stringBuilder.ToString();
        stringBuilder.AppendLine();
        stringBuilder.AppendLine();
        stringBuilder.Append(PilotDamagePilotingSkillRoll.Render(localizationService));

        return stringBuilder.ToString();
    }
}

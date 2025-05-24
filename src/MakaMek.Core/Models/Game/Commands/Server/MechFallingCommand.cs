using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Services.Localization;
using System.Text;

namespace Sanet.MakaMek.Core.Models.Game.Commands.Server;

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

    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    public string Format(ILocalizationService localizationService, IGame game)
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
            DamageData.TotalDamage));
        
        // Add pilot injury information if applicable
        if (DamageData.PilotTakesDamage)
        {
            stringBuilder.Append(localizationService.GetString("Command_MechFalling_PilotInjury"));
        }
        
        return stringBuilder.ToString();
    }
}

using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Services.Localization;

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
    public required bool PilotTakesDamage { get; init; }
    public List<DiceResult>? PilotDamageRoll { get; init; }

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
        
        string message = $"{unit.Name} fell";
        
        if (LevelsFallen > 0)
        {
            message += $" {LevelsFallen} level(s)";
        }
        
        if (WasJumping)
        {
            message += " while jumping";
        }
        
        message += $" and took {DamageData.TotalDamage} damage";
        
        if (PilotTakesDamage)
        {
            message += ", pilot was injured";
        }
        
        return message;
    }
}

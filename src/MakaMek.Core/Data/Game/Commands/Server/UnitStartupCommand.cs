using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

/// <summary>
/// Command sent when a mech attempts to restart from shutdown
/// </summary>
public record struct UnitStartupCommand : IGameCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// The ID of the unit attempting restart
    /// </summary>
    public required Guid UnitId { get; init; }
    
    /// <summary>
    /// Whether the restart was automatic (heat below threshold)
    /// </summary>
    public bool IsAutomaticRestart { get; init; }
    
    public required AvoidShutdownRollData? AvoidShutdownRoll { get; init; }

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

        return command switch
        {
            { IsAutomaticRestart: true } =>
                string.Format(localizationService.GetString("Command_MechRestart_Automatic"), 
                    unit.Name, command.AvoidShutdownRoll?.HeatLevel),

            { AvoidShutdownRoll.IsSuccessful: true } =>
                string.Format(localizationService.GetString("Command_MechRestart_Successful"), 
                    unit.Name, 
                    command.AvoidShutdownRoll?.HeatLevel,
                    string.Join(", ", command.AvoidShutdownRoll?.DiceResults??[]),
                    command.AvoidShutdownRoll?.DiceResults.Sum(),
                    command.AvoidShutdownRoll?.AvoidNumber),

            { AvoidShutdownRoll.IsSuccessful: false } =>
                string.Format(localizationService.GetString("Command_MechRestart_Failed"), 
                    unit.Name, 
                    command.AvoidShutdownRoll?.HeatLevel,
                    string.Join(", ", command.AvoidShutdownRoll?.DiceResults??[]),
                    command.AvoidShutdownRoll?.DiceResults.Sum(),
                    command.AvoidShutdownRoll?.AvoidNumber),
            
            _ => string.Format(localizationService.GetString("Command_MechRestart_Generic"), 
                unit.Name)
        };
    }
}

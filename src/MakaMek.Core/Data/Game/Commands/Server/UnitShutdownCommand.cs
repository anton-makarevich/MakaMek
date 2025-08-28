using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

/// <summary>
/// Command sent when a mech shuts down due to heat or other reasons
/// </summary>
public record struct UnitShutdownCommand : IGameCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// The ID of the unit that shut down
    /// </summary>
    public required Guid UnitId { get; init; }
    
    public AvoidShutdownRollData? AvoidShutdownRoll { get; init; }
    
    /// <summary>
    /// Information about the shutdown event
    /// </summary>
    public required ShutdownData ShutdownData { get; init; }
    
    /// <summary>
    /// Whether the shutdown was automatic (heat 30+) or due to failed roll
    /// </summary>
    public bool IsAutomaticShutdown { get; init; }

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
        
        var isPilotUnconscious = unit.Pilot?.IsConscious == false;

        return command.ShutdownData.Reason switch
        {
            ShutdownReason.Heat when command.AvoidShutdownRoll?.IsSuccessful == true =>
                string.Format(localizationService.GetString("Command_MechShutdown_Avoided"),
                    unit.Model,
                    command.AvoidShutdownRoll?.HeatLevel,
                    string.Join(", ", command.AvoidShutdownRoll?.DiceResults ?? []),
                    command.AvoidShutdownRoll?.DiceResults.Sum(),
                    command.AvoidShutdownRoll?.AvoidNumber),

            ShutdownReason.Heat when isPilotUnconscious =>
                            string.Format(localizationService.GetString("Command_MechShutdown_UnconsciousPilot"),
                                unit.Model, unit.CurrentHeat),

            ShutdownReason.Heat when command.IsAutomaticShutdown =>
                string.Format(localizationService.GetString("Command_MechShutdown_AutomaticHeat"),
                    unit.Model, unit.CurrentHeat),

            ShutdownReason.Heat when command.AvoidShutdownRoll != null =>
                string.Format(localizationService.GetString("Command_MechShutdown_FailedRoll"),
                    unit.Model,
                    command.AvoidShutdownRoll?.HeatLevel,
                    string.Join(", ", command.AvoidShutdownRoll?.DiceResults ?? []),
                    command.AvoidShutdownRoll?.DiceResults.Sum(),
                    command.AvoidShutdownRoll?.AvoidNumber),

            ShutdownReason.Voluntary =>
                string.Format(localizationService.GetString("Command_MechShutdown_Voluntary"),
                    unit.Model),

            _ => string.Format(localizationService.GetString("Command_MechShutdown_Generic"),
                unit.Model)
        };
    }
}

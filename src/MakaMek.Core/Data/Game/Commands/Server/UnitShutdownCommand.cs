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

        return command.ShutdownData.Reason switch
        {
            ShutdownReason.Heat when command.AvoidShutdownRoll?.IsSuccessful == true =>
                string.Format(localizationService.GetString("Command_MechShutdown_Avoided"),
                    unit.Name,
                    command.AvoidShutdownRoll?.HeatLevel,
                    string.Join(", ", command.AvoidShutdownRoll?.DiceResults ?? []),
                    command.AvoidShutdownRoll?.DiceResults.Sum(),
                    command.AvoidShutdownRoll?.AvoidNumber),

            ShutdownReason.Heat when command.IsAutomaticShutdown =>
                string.Format(localizationService.GetString("Command_MechShutdown_AutomaticHeat"),
                    unit.Name, command.AvoidShutdownRoll?.HeatLevel),

            ShutdownReason.Heat when command.AvoidShutdownRoll?.DiceResults.Length == 0 =>
                string.Format(localizationService.GetString("Command_MechShutdown_UnconsciousPilot"),
                    unit.Name, command.AvoidShutdownRoll?.HeatLevel),

            ShutdownReason.Heat when command.AvoidShutdownRoll != null =>
                string.Format(localizationService.GetString("Command_MechShutdown_FailedRoll"),
                    unit.Name,
                    command.AvoidShutdownRoll?.HeatLevel,
                    string.Join(", ", command.AvoidShutdownRoll?.DiceResults ?? []),
                    command.AvoidShutdownRoll?.DiceResults.Sum(),
                    command.AvoidShutdownRoll?.AvoidNumber),

            ShutdownReason.Voluntary =>
                string.Format(localizationService.GetString("Command_MechShutdown_Voluntary"),
                    unit.Name),

            _ => string.Format(localizationService.GetString("Command_MechShutdown_Generic"),
                unit.Name)
        };
    }
}

using System.Text;
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
    public required bool IsAutomaticRestart { get; init; }

    /// <summary>
    /// Whether restart is possible (e.g., heat is low enough)
    /// </summary>
    public required bool IsRestartPossible { get; init; }

    /// <summary>
    /// Optional roll data if a restart roll was made
    /// </summary>
    public AvoidShutdownRollData? AvoidShutdownRoll { get; init; }

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
                    unit.Model, unit.CurrentHeat),

            { IsRestartPossible: false } =>
                string.Format(localizationService.GetString("Command_MechRestart_Impossible"),
                    unit.Model, unit.CurrentHeat),

            { AvoidShutdownRoll.IsSuccessful: true } =>
                BuildRollMessage(localizationService, unit.Model, "Command_MechRestart_Successful",
                    command.AvoidShutdownRoll),

            { AvoidShutdownRoll.IsSuccessful: false } =>
                BuildRollMessage(localizationService, unit.Model, "Command_MechRestart_Failed",
                    command.AvoidShutdownRoll),

            _ => string.Format(localizationService.GetString("Command_MechRestart_Generic"),
                unit.Model)
        };
    }

    private static string BuildRollMessage(ILocalizationService localizationService, string unitModel,
        string messageKey, AvoidShutdownRollData? rollData)
    {
        if (rollData == null) return string.Empty;

        var stringBuilder = new StringBuilder();
        var total = rollData.DiceResults.Sum();

        var template = localizationService.GetString(messageKey);
        stringBuilder.AppendLine(string.Format(template, unitModel, rollData.HeatLevel));
        stringBuilder.AppendLine(string.Format(localizationService.GetString("Command_AvoidNumber"), rollData.AvoidNumber));
        stringBuilder.AppendLine(string.Format(localizationService.GetString("Command_RollResult"), total));

        return stringBuilder.ToString().TrimEnd();
    }
}

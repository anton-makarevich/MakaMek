using System.Text;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

public record HullBreachCommand : IGameCommand
{
    public required Guid GameOriginId { get; set; }
    public required Guid UnitId { get; init; }
    public required List<LocationHullBreachData> BreachedLocations { get; init; }
    public DateTime Timestamp { get; set; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var target = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == UnitId);

        if (target == null)
        {
            return string.Empty;
        }

        var stringBuilder = new StringBuilder();

        stringBuilder.AppendFormat(
            localizationService.GetString("Command_HullBreach_Header"),
            target.Model).AppendLine();

        foreach (var breachData in BreachedLocations)
        {
            var localizedLocation = localizationService.GetString($"MechPart_{breachData.Location}_Short");

            if (breachData.IsAutomatic)
            {
                stringBuilder.AppendFormat(
                    localizationService.GetString("Command_HullBreach_Automatic"),
                    localizedLocation).AppendLine();
            }
            else if (breachData.BreachRoll != null)
            {
                stringBuilder.AppendFormat(
                    localizationService.GetString("Command_HullBreach_Roll"),
                    localizedLocation,
                    breachData.BreachRoll.Sum()).AppendLine();
            }

            if (breachData.EngineHitsApplied > 0)
            {
                stringBuilder.AppendFormat(
                    localizationService.GetString("Command_HullBreach_EngineDamage"),
                    breachData.EngineHitsApplied).AppendLine();
            }
        }

        return stringBuilder.ToString().TrimEnd();
    }
}

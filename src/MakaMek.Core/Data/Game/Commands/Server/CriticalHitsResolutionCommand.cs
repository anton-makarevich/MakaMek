using System.Text;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

/// <summary>
/// Command sent from server to clients to apply critical hits resolution data
/// </summary>
public record CriticalHitsResolutionCommand : IGameCommand
{
    public required Guid GameOriginId { get; set; }
    public required Guid TargetId { get; init; }
    public required List<LocationCriticalHitsData> CriticalHits { get; init; }
    public DateTime Timestamp { get; set; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var target = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == TargetId);

        if (target == null)
        {
            return string.Empty;
        }

        var stringBuilder = new StringBuilder();

        // Add a header message
        stringBuilder.AppendFormat(
            localizationService.GetString("Command_CriticalHitsResolution_Header"),
            target.Model).AppendLine();

        foreach (var criticalHitData in CriticalHits)
        {
            // Use the centralized rendering method
            stringBuilder.Append(criticalHitData.Render(localizationService, target));
        }
        return stringBuilder.ToString();
    }
}

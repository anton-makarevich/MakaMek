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

        foreach (var criticalHitData in CriticalHits)
        {
            var localizedLocation = localizationService.GetString($"MechPart_{criticalHitData.Location}_Short");
            // Show location if there are multiple locations
            if (CriticalHits.Count > 1)
            {
                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_CriticalHitsResolution_Location"),
                    localizedLocation));
            }

            // Show critical hit roll and results
            stringBuilder.AppendLine(string.Format(
                localizationService.GetString("Command_CriticalHitsResolution_CritRoll"),
                criticalHitData.Roll.Sum()));

            // Check if the location is blown off
            if (criticalHitData.IsBlownOff)
            {
                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_CriticalHitsResolution_BlownOff"),
                    localizedLocation));
                continue;
            }

            var part = target.Parts.FirstOrDefault(p => p.Location == criticalHitData.Location);
                    
            // Show the number of critical hits
            stringBuilder.AppendLine(string.Format(
                                localizationService.GetString("Command_CriticalHitsResolution_NumCrits"),
                                criticalHitData.NumCriticalHits));
            if (criticalHitData.NumCriticalHits <= 0) continue;

            // Show hit components
            if (criticalHitData.HitComponents != null)
            {
                foreach (var componentHit in criticalHitData.HitComponents)
                {
                    var component = part?.GetComponentAtSlot(componentHit.Slot);
                    if (component == null || component.ComponentType != componentHit.Type) continue;
                    stringBuilder.AppendLine(string.Format(
                        localizationService.GetString("Command_CriticalHitsResolution_CriticalHit"),
                        componentHit.Slot + 1,
                        component.Name));
                    var explosionDamage = componentHit.ExplosionDamage;
                    if (explosionDamage > 0)
                    {
                        stringBuilder.AppendLine(string.Format(
                            localizationService.GetString("Command_CriticalHitsResolution_Explosion"),
                            component.Name,
                            explosionDamage));
                    }
                }
            }
        }
        return stringBuilder.ToString();
    }
}

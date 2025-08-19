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
    public required List<LocationCriticalHitsResolutionData> CriticalHitsData { get; init; }
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

        foreach (var criticalHitData in CriticalHitsData)
        {
            // Show location if there are multiple locations
            if (CriticalHitsData.Count > 1)
            {
                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_CriticalHitsResolution_Location"),
                    criticalHitData.Location));
            }

            // Show structure damage that triggered the critical hits
            if (criticalHitData.StructureDamageReceived > 0)
            {
                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_CriticalHitsResolution_StructureDamage"),
                    criticalHitData.StructureDamageReceived));
            }

            // Show critical hit roll and results
            stringBuilder.AppendLine(string.Format(
                localizationService.GetString("Command_CriticalHitsResolution_CritRoll"),
                criticalHitData.CriticalHitRoll));

            // Check if the location is blown off
            if (criticalHitData.IsBlownOff)
            {
                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_CriticalHitsResolution_BlownOff"),
                    criticalHitData.Location));
                continue;
            }

            // Show number of critical hits
            if (criticalHitData.NumCriticalHits > 0)
            {
                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_CriticalHitsResolution_NumCrits"),
                    criticalHitData.NumCriticalHits));

                // Show hit components
                if (criticalHitData.HitComponents != null)
                {
                    var part = target.Parts.FirstOrDefault(p => p.Location == criticalHitData.Location);
                    foreach (var component in criticalHitData.HitComponents)
                    {
                        var comp = part?.GetComponentAtSlot(component.Slot);
                        if (comp != null)
                        {
                            stringBuilder.AppendLine(string.Format(
                                localizationService.GetString("Command_CriticalHitsResolution_CriticalHit"),
                                criticalHitData.Location,
                                component.Slot + 1,
                                comp.Name));
                        }
                    }
                }
            }

            // Show explosions
            if (criticalHitData.Explosions != null && criticalHitData.Explosions.Any())
            {
                foreach (var explosion in criticalHitData.Explosions)
                {
                    var part = target.Parts.FirstOrDefault(p => p.Location == criticalHitData.Location);
                    var comp = part?.GetComponentAtSlot(explosion.Slot);
                    if (comp != null)
                    {
                        stringBuilder.AppendLine(string.Format(
                            localizationService.GetString("Command_CriticalHitsResolution_Explosion"),
                            comp.Name,
                            explosion.ExplosionDamage));
                    }
                }
            }
        }

        return stringBuilder.ToString();
    }
}

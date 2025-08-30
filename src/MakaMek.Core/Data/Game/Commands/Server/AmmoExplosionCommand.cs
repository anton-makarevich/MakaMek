using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;
using System.Text;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

/// <summary>
/// Command sent when a mech experiences heat-triggered ammo explosion
/// </summary>
public record struct AmmoExplosionCommand : IGameCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// The ID of the unit that experienced the ammo explosion
    /// </summary>
    public required Guid UnitId { get; init; }

    /// <summary>
    /// The roll data for the ammo explosion avoidance attempt
    /// </summary>
    public AvoidAmmoExplosionRollData? AvoidExplosionRoll { get; init; }

    /// <summary>
    /// Critical hits resolution data for the explosion
    /// </summary>
    public List<LocationCriticalHitsData> CriticalHits { get; init; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var unitId = UnitId; // Copy to a local variable to avoid struct access issues
        var unit = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == unitId);

        if (unit == null)
        {
            return string.Empty;
        }

        var stringBuilder = new StringBuilder();

        // Check if an explosion occurred
        var explosionOccurred = AvoidExplosionRoll?.IsSuccessful == false;

        if (AvoidExplosionRoll != null)
        {
            var rollTotal = AvoidExplosionRoll.DiceResults.Sum();

            if (AvoidExplosionRoll.IsSuccessful)
            {
                // Explosion avoided
                var successTemplate = localizationService.GetString("Command_AmmoExplosion_Avoided");
                stringBuilder.AppendLine(string.Format(successTemplate, unit.Model));

                // Add roll details
            }
            else
            {
                // Explosion occurred due to a failed roll
                var failureTemplate = localizationService.GetString("Command_AmmoExplosion_Failed");
                stringBuilder.AppendLine(string.Format(failureTemplate, unit.Model));

                // Add roll details
            }

            stringBuilder.AppendLine(string.Format(
                localizationService.GetString("Command_AmmoExplosion_RollDetails"),
                AvoidExplosionRoll.HeatLevel,
                rollTotal,
                AvoidExplosionRoll.AvoidNumber));
        }

        // If an explosion occurred, show the critical hits details
        if (explosionOccurred)
        {
            stringBuilder.AppendLine(localizationService.GetString("Command_AmmoExplosion_CriticalHits"));

            foreach (var criticalHitData in CriticalHits)
            {
                if (criticalHitData.HitComponents != null)
                {
                    foreach (var componentHit in criticalHitData.HitComponents)
                    {
                        var part = unit.Parts.FirstOrDefault(p => p.Location == criticalHitData.Location);
                        var component = part?.GetComponentAtSlot(componentHit.Slot);
                        if (component != null)
                        {
                            var localizedLocation = localizationService.GetString($"MechPart_{criticalHitData.Location}_Short");
                            stringBuilder.AppendLine(string.Format(
                                localizationService.GetString("Command_AmmoExplosion_ComponentDestroyed"),
                                component.Name,
                                localizedLocation));
                            var explosionDamage = componentHit.ExplosionDamage;
                            if (explosionDamage > 0)
                            {
                                stringBuilder.AppendLine(string.Format(
                                    localizationService.GetString("Command_AmmoExplosion_Explosion"),
                                    component.Name,
                                    explosionDamage));
                            }
                        }
                    }
                }
            }
        }

        return stringBuilder.ToString().TrimEnd();
    }
}

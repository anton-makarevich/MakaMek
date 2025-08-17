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
    /// Hit location data resulting from the explosion, ready for damage application
    /// </summary>
    public List<HitLocationData> ExplosionDamage { get; init; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var unitId = UnitId; // Copy to local variable to avoid struct access issues
        var unit = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == unitId);

        if (unit == null)
        {
            return string.Empty;
        }
        
        var stringBuilder = new StringBuilder();
        
        // Check if explosion occurred
        var explosionOccurred = AvoidExplosionRoll?.IsSuccessful == false;
        
        if (AvoidExplosionRoll != null)
        {
            var rollTotal = AvoidExplosionRoll.DiceResults.Sum();
            
            if (AvoidExplosionRoll.IsSuccessful)
            {
                // Explosion avoided
                var successTemplate = localizationService.GetString("Command_AmmoExplosion_Avoided");
                stringBuilder.AppendLine(string.Format(successTemplate, unit.Name));
                
                // Add roll details
                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_AmmoExplosion_RollDetails"),
                    AvoidExplosionRoll.HeatLevel,
                    rollTotal,
                    AvoidExplosionRoll.AvoidNumber));
            }
            else
            {
                // Explosion occurred due to failed roll
                var failureTemplate = localizationService.GetString("Command_AmmoExplosion_Failed");
                stringBuilder.AppendLine(string.Format(failureTemplate, unit.Name));
                
                // Add roll details
                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_AmmoExplosion_RollDetails"),
                    AvoidExplosionRoll.HeatLevel,
                    rollTotal,
                    AvoidExplosionRoll.AvoidNumber));
            }
        }
        
        // If explosion occurred, show the critical hits details
        if (explosionOccurred && ExplosionDamage.Count > 0)
        {
            stringBuilder.AppendLine(localizationService.GetString("Command_AmmoExplosion_CriticalHits"));

            var criticalHits = ExplosionDamage.SelectMany(ht => ht.CriticalHits??[]).ToList();
            foreach (var criticalHit in criticalHits)
            {
                if (criticalHit.HitComponents != null)
                {
                    foreach (var componentHit in criticalHit.HitComponents)
                    {
                        var part = unit.Parts.FirstOrDefault(p => p.Location == criticalHit.Location);
                        var component = part?.GetComponentAtSlot(componentHit.Slot);
                        if (component != null)
                        {
                            stringBuilder.AppendLine(string.Format(
                                localizationService.GetString("Command_AmmoExplosion_ComponentDestroyed"),
                                component.Name,
                                criticalHit.Location));
                        }
                    }
                }
            }
        }
        
        return stringBuilder.ToString().TrimEnd();
    }
}

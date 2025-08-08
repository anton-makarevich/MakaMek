using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using System.Text;

namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Represents a single hit location with its damage and optional critical hits (slot indexes)
/// </summary>
public record HitLocationData(
    PartLocation Location,
    int Damage,
    int[] AimedShotRoll,
    int[] LocationRoll,
    List<LocationCriticalHitsData>? CriticalHits = null, // Optional: detailed critical hits info for all affected locations, null if none
    PartLocation? InitialLocation = null
)
{
    /// <summary>
    /// Renders the hit location information including damage and any critical hits
    /// </summary>
    /// <param name="localizationService">Service used to get localized strings</param>
    /// <param name="unit">Unit to get component names</param>
    /// <returns>String representation of the hit location with damage and criticals</returns>
    public string Render(ILocalizationService localizationService, Unit unit)
    {
        var stringBuilder = new StringBuilder();

        // Check if this was an aimed shot
        var isAimedShot = AimedShotRoll.Length > 0;
        var aimedShotTotal = isAimedShot ? AimedShotRoll.Sum() : 0;
        var aimedShotSuccessful = isAimedShot && aimedShotTotal is >= 6 and <= 8;
        var locationRollTotal = LocationRoll.Length > 0 ? LocationRoll.Sum() : 0;

        // If there was a location transfer, show both the initial and final locations
        if (InitialLocation.HasValue && InitialLocation.Value != Location)
        {
            if (isAimedShot)
            {
                if (aimedShotSuccessful)
                {
                    stringBuilder.AppendLine(string.Format(
                        localizationService.GetString("Command_WeaponAttackResolution_AimedShotTransferSuccessful"),
                        InitialLocation.Value,
                        Location,
                        Damage,
                        aimedShotTotal));
                }
                else
                {
                    stringBuilder.AppendLine(string.Format(
                        localizationService.GetString("Command_WeaponAttackResolution_AimedShotTransferFailed"),
                        InitialLocation.Value,
                        Location,
                        Damage,
                        aimedShotTotal,
                        locationRollTotal));
                }
            }
            else
            {
                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_WeaponAttackResolution_HitLocationTransfer"),
                    InitialLocation.Value,
                    Location,
                    Damage,
                    locationRollTotal));
            }
        }
        else
        {
            if (isAimedShot)
            {
                if (aimedShotSuccessful)
                {
                    stringBuilder.AppendLine(string.Format(
                        localizationService.GetString("Command_WeaponAttackResolution_AimedShotSuccessful"),
                        Location,
                        Damage,
                        aimedShotTotal));
                }
                else
                {
                    stringBuilder.AppendLine(string.Format(
                        localizationService.GetString("Command_WeaponAttackResolution_AimedShotFailed"),
                        Location,
                        Damage,
                        aimedShotTotal,
                        locationRollTotal));
                }
            }
            else
            {
                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_WeaponAttackResolution_HitLocation"),
                    Location,
                    Damage,
                    locationRollTotal));
            }
        }
        
        // Process all critical hits for this hit location
        if (CriticalHits == null || !CriticalHits.Any())
            return stringBuilder.ToString();
        
        // Process all critical hits in order
        foreach (var criticalHit in CriticalHits)
        {
            // Show location if different from the primary hit location
            if (criticalHit.Location != Location)
            {
                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_WeaponAttackResolution_LocationCriticals"),
                    criticalHit.Location));
            }
            
            stringBuilder.AppendLine(string.Format(
                localizationService.GetString("Command_WeaponAttackResolution_CritRoll"),
                criticalHit.Roll));
            
            // Check if the location is blown off
            if (criticalHit.IsBlownOff)
            {
                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_WeaponAttackResolution_BlownOff"),
                    criticalHit.Location));
                continue;
            }
            
            stringBuilder.AppendLine(string.Format(
                localizationService.GetString("Command_WeaponAttackResolution_NumCrits"),
                criticalHit.NumCriticalHits));
            
            if (criticalHit.HitComponents == null || criticalHit.HitComponents.Length == 0)
                continue;
            
            var part = unit.Parts.FirstOrDefault(p => p.Location == criticalHit.Location);

            foreach (var component in criticalHit.HitComponents)
            {
                var slot = component.Slot;
                var comp = part?.GetComponentAtSlot(slot);
                if (comp == null) continue;
                var compName = comp.Name;

                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_WeaponAttackResolution_CriticalHit"),
                    criticalHit.Location,
                    slot + 1,
                    compName));
                    
                // Check if this component can explode
                if (comp is not { CanExplode: true, HasExploded: false }) continue;
                var damage = comp.GetExplosionDamage();
                if (damage <= 0) continue;
                var explosionTemplate =
                    localizationService.GetString("Command_WeaponAttackResolution_Explosion");

                stringBuilder.AppendLine(string.Format(explosionTemplate,
                    compName,
                    damage));
            }
        }
        
        return stringBuilder.ToString();
    }
}
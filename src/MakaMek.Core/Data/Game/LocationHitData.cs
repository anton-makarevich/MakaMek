using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using System.Text;

namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Represents a single hit location with its damage and optional critical hits (slot indexes)
/// </summary>
public record LocationHitData(
    LocationDamageData DamageData,
    int[] AimedShotRoll,
    int[] LocationRoll,
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
        // Note: Aimed shots should never have location transfers since they only target non-destroyed locations
        if (InitialLocation.HasValue && InitialLocation.Value != DamageData.Location)
        {
            stringBuilder.AppendLine(string.Format(
                localizationService.GetString("Command_WeaponAttackResolution_HitLocationTransfer"),
                InitialLocation.Value,
                DamageData.Location,
                DamageData.ArmorDamage,
                DamageData.StructureDamage,
                locationRollTotal));
        }
        else
        {
            if (isAimedShot)
            {
                if (aimedShotSuccessful)
                {
                    stringBuilder.AppendLine(string.Format(
                        localizationService.GetString("Command_WeaponAttackResolution_AimedShotSuccessful"),
                        DamageData.Location,
                        DamageData.ArmorDamage,
                        DamageData.StructureDamage,
                        aimedShotTotal));
                }
                else
                {
                    stringBuilder.AppendLine(string.Format(
                        localizationService.GetString("Command_WeaponAttackResolution_AimedShotFailed"),
                        DamageData.Location,
                        DamageData.ArmorDamage,
                        DamageData.StructureDamage,
                        aimedShotTotal,
                        locationRollTotal));
                }
            }
            else
            {
                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_WeaponAttackResolution_HitLocation"),
                    DamageData.Location,
                    DamageData.ArmorDamage,
                    DamageData.StructureDamage,
                    locationRollTotal));
            }
        }
        
        return stringBuilder.ToString();
    }
}
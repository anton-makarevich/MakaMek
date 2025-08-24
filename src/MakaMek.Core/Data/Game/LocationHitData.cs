using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using System.Text;

namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Represents a single hit location with its damage and optional critical hits (slot indexes)
/// </summary>
public record LocationHitData(
    List<LocationDamageData> Damage,
    int[] AimedShotRoll,
    int[] LocationRoll,
    PartLocation InitialLocation
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

        if (isAimedShot)
        {
            if (aimedShotSuccessful)
            {
                var template = localizationService.GetString("Command_WeaponAttackResolution_AimedShotSuccessful");
                stringBuilder.AppendLine(string.Format(
                    template,
                    InitialLocation,
                    aimedShotTotal));
            }
            else
            {
                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_WeaponAttackResolution_AimedShotFailed"),
                    InitialLocation,
                    aimedShotTotal));
            }
        }

        if (Damage.Count == 0)
        {
            return stringBuilder.ToString();
        }
        
        // If there was a location transfer, show both the initial and final locations
        if (InitialLocation != Damage[0].Location)
        {
            stringBuilder.AppendLine(string.Format(
                localizationService.GetString("Command_WeaponAttackResolution_HitLocationTransfer"),
                InitialLocation,
                Damage[0].Location,
                Damage[0].ArmorDamage+Damage[0].StructureDamage,
                locationRollTotal));
        }
        else
        {
            stringBuilder.AppendLine(string.Format(
                localizationService.GetString("Command_WeaponAttackResolution_HitLocation"),
                Damage[0].Location,
                Damage[0].ArmorDamage+Damage[0].StructureDamage,
                locationRollTotal));
        }
        
        if (Damage.Count > 1)
        {
            for (var i = 1; i < Damage.Count; i++)
            {
                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_WeaponAttackResolution_HitLocationExcessDamage"),
                    Damage[i].Location,
                    Damage[i].ArmorDamage+Damage[i].StructureDamage));
            }
        }

        return stringBuilder.ToString();
    }
}
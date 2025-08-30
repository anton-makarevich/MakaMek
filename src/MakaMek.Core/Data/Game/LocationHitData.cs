using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using System.Text;

namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Represents a single hit location with its damage and optional critical hits (slot indexes)
/// </summary>
public record LocationHitData(
    IReadOnlyList<LocationDamageData> Damage,
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
        
        if (Damage.Count == 0)
        {
            return stringBuilder.ToString();
        }

        // Check if this was an aimed shot
        var isAimedShot = AimedShotRoll.Length > 0;
        var aimedShotTotal = isAimedShot ? AimedShotRoll.Sum() : 0;
        var aimedShotSuccessful = isAimedShot && aimedShotTotal is >= 6 and <= 8;
        var locationRollTotal = LocationRoll.Length > 0 ? LocationRoll.Sum() : 0;
        var localizedInitialLocation = localizationService.GetString($"MechPart_{InitialLocation}_Short");

        if (isAimedShot)
        {
            if (aimedShotSuccessful)
            {
                var template = localizationService.GetString("Command_WeaponAttackResolution_AimedShotSuccessful");
                stringBuilder.AppendLine(string.Format(
                    template,
                    localizedInitialLocation,
                    aimedShotTotal));
            }
            else
            {
                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_WeaponAttackResolution_AimedShotFailed"),
                    localizedInitialLocation,
                    aimedShotTotal));
            }
        }
        
        // If there was a location transfer, show both the initial and final locations
        if (InitialLocation != Damage[0].Location)
        {
            stringBuilder.AppendLine(FormatDamageMessage(
                localizationService,
                "Command_WeaponAttackResolution_HitLocationTransfer",
                InitialLocation,
                Damage[0].Location,
                Damage[0].ArmorDamage,
                Damage[0].StructureDamage,
                locationRollTotal));
        }
        else
        {
            stringBuilder.AppendLine(FormatDamageMessage(
                localizationService,
                "Command_WeaponAttackResolution_HitLocation",
                Damage[0].Location,
                null,
                Damage[0].ArmorDamage,
                Damage[0].StructureDamage,
                locationRollTotal));
        }
        
        if (Damage.Count > 1)
        {
            for (var i = 1; i < Damage.Count; i++)
            {
                stringBuilder.AppendLine(FormatExcessDamageMessage(
                    localizationService,
                    Damage[i].Location,
                    Damage[i].ArmorDamage,
                    Damage[i].StructureDamage));
            }
        }

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Formats a damage message using the appropriate localization key based on damage types
    /// </summary>
    private static string FormatDamageMessage(
        ILocalizationService localizationService,
        string baseKey,
        PartLocation location1,
        PartLocation? location2,
        int armorDamage,
        int structureDamage,
        int rollInfo)
    {
        var hasArmor = armorDamage > 0;
        var hasStructure = structureDamage > 0;

        string key;
        if (hasArmor && hasStructure)
        {
            key = $"{baseKey}_ArmorAndStructure";
        }
        else if (hasArmor)
        {
            key = $"{baseKey}_ArmorOnly";
        }
        else if (hasStructure)
        {
            key = $"{baseKey}_StructureOnly";
        } 
        else
        {
            return string.Empty;
        }

        var template = localizationService.GetString(key);
        var localizedLocation1 = localizationService.GetString($"MechPart_{location1}_Short");

        // Handle different parameter counts based on whether it's a transfer or regular hit
        if (location2 != null) // Transfer case
        {
            var localizedLocation2 = localizationService.GetString($"MechPart_{location2}_Short");
            if (hasArmor && hasStructure)
            {
                return string.Format(template, localizedLocation1, localizedLocation2, armorDamage, structureDamage, rollInfo);
            }

            if (hasArmor)
            {
                return string.Format(template, localizedLocation1, localizedLocation2, armorDamage, rollInfo);
            }

            if (!hasStructure) return string.Empty;
            
            return string.Format(template, localizedLocation1, localizedLocation2, structureDamage, rollInfo);
        }

        // Regular hit case
        if (hasArmor && hasStructure)
        {
            return string.Format(template, localizedLocation1, armorDamage, structureDamage, rollInfo);
        }

        if (hasArmor)
        {
            return string.Format(template, localizedLocation1, armorDamage, rollInfo);
        }

        if (!hasStructure) return string.Empty;
        
        return string.Format(template, localizedLocation1, structureDamage, rollInfo);
    }

    /// <summary>
    /// Formats an excess damage message using the appropriate localization key
    /// </summary>
    private static string FormatExcessDamageMessage(
        ILocalizationService localizationService,
        PartLocation location,
        int armorDamage,
        int structureDamage)
    {
        var hasArmor = armorDamage > 0;
        var hasStructure = structureDamage > 0;

        var localizedLocation = localizationService.GetString($"MechPart_{location}_Short");

        string key;
        if (hasArmor && hasStructure)
        {
            key = "Command_WeaponAttackResolution_HitLocationExcessDamage_ArmorAndStructure";
            var template = localizationService.GetString(key);
            return string.Format(template, localizedLocation, armorDamage, structureDamage);
        }

        if (hasArmor)
        {
            key = "Command_WeaponAttackResolution_HitLocationExcessDamage_ArmorOnly";
            var template = localizationService.GetString(key);
            return string.Format(template, localizedLocation, armorDamage);
        }

        if (!hasStructure) return string.Empty;

        key = "Command_WeaponAttackResolution_HitLocationExcessDamage_StructureOnly";
        return string.Format(localizationService.GetString(key), localizedLocation, structureDamage);
    }
}
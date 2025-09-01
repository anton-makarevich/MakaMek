using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using System.Text;

namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Contains detailed information about critical hits for a specific location: the location, roll made, number of crits, and the slots hit.
/// </summary>
public record LocationCriticalHitsData(
    PartLocation Location,
    int[] Roll,
    int NumCriticalHits,
    ComponentHitData[]? HitComponents,
    bool IsBlownOff // Indicates if the location is blown off (for head and limbs on critical roll of 12)
)
{
    /// <summary>
    /// Renders the critical hits information for this location
    /// </summary>
    /// <param name="localizationService">Service used to get localized strings</param>
    /// <param name="unit">Unit to get component names and parts</param>
    /// <returns>String representation of the critical hits for this location</returns>
    public string Render(ILocalizationService localizationService, Unit unit)
    {
        var stringBuilder = new StringBuilder();
        var localizedLocation = localizationService.GetString($"MechPart_{Location}_Short");

        // Show location header
        stringBuilder.AppendLine(string.Format(
                localizationService.GetString("Command_CriticalHitsResolution_Location"),
                localizedLocation));

        if (Roll.Length > 0)
        {
            // Show critical hit roll
            stringBuilder.AppendLine(string.Format(
                localizationService.GetString("Command_CriticalHitsResolution_CritRoll"),
                Roll.Sum()));

            // Handle blown off location
            if (IsBlownOff)
            {
                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_CriticalHitsResolution_BlownOff"),
                    localizedLocation));
                return stringBuilder.ToString();
            }

            // Show the number of critical hits
            stringBuilder.AppendLine(string.Format(
                localizationService.GetString("Command_CriticalHitsResolution_NumCrits"),
                NumCriticalHits));
        }

        if (NumCriticalHits <= 0) return stringBuilder.ToString();

        // Show hit components
        if (HitComponents != null)
        {
            var part = unit.Parts.FirstOrDefault(p => p.Location == Location);
            foreach (var componentHit in HitComponents)
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

                // Add explosion damage distribution if present
                if (componentHit.ExplosionDamageDistribution.Length <= 0) continue;
                stringBuilder.AppendLine(localizationService.GetString("Command_CriticalHitsResolution_ExplosionDamageDistribution"));
                foreach (var damageData in componentHit.ExplosionDamageDistribution)
                {
                    stringBuilder.AppendLine(FormatExplosionDamageMessage(localizationService, damageData));
                }
            }
        }

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Formats an explosion damage message using the appropriate localization key based on damage types
    /// </summary>
    private static string FormatExplosionDamageMessage(
        ILocalizationService localizationService,
        LocationDamageData damageData)
    {
        var hasArmor = damageData.ArmorDamage > 0;
        var hasStructure = damageData.StructureDamage > 0;

        if (!hasArmor && !hasStructure)
        {
            return string.Empty;
        }

        var localizedLocation = localizationService.GetString($"MechPart_{damageData.Location}_Short");

        string key;
        string template;
        if (hasArmor && hasStructure)
        {
            key = "Command_WeaponAttackResolution_HitLocationExcessDamage_ArmorAndStructure";
            template = localizationService.GetString(key);
            return string.Format(template, localizedLocation, damageData.ArmorDamage, damageData.StructureDamage);
        }

        if (hasArmor)
        {
            key = "Command_WeaponAttackResolution_HitLocationExcessDamage_ArmorOnly";
            template = localizationService.GetString(key);
            return string.Format(template, localizedLocation, damageData.ArmorDamage);
        }

        // hasStructure must be true
        key = "Command_WeaponAttackResolution_HitLocationExcessDamage_StructureOnly";
        template = localizationService.GetString(key);
        return string.Format(template, localizedLocation, damageData.StructureDamage);
    }
};

using Sanet.MakaMek.Core.Models.Units;
using System.Text;
using Sanet.Localization;

namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Represents a single hit location with its damage and optional critical hits (slot indexes)
/// </summary>
public record LocationHitData(
    IReadOnlyList<LocationDamageData> Damage,
    int[] AimedShotRoll,
    int[] LocationRoll,
    PartLocation InitialLocation,
    CoveringHexData? CoveringHexAbsorption = null
)
{
    /// <summary>
    /// Renders the hit location information including damage and any critical hits
    /// </summary>
    /// <param name="localizationService">Service used to get localized strings</param>
    /// <returns>String representation of the hit location with damage and criticals</returns>
    public string Render(ILocalizationService localizationService)
    {
        if (Damage.Count == 0)
            return RenderTerrainAbsorption(localizationService);

        var stringBuilder = new StringBuilder();
        AppendAimedShot(stringBuilder, localizationService);
        AppendFirstHit(stringBuilder, localizationService);
        AppendExcessDamage(stringBuilder, localizationService);
        return stringBuilder.ToString();
    }

    private string RenderTerrainAbsorption(ILocalizationService localizationService)
    {
        if (CoveringHexAbsorption == null) return string.Empty;

        return string.Format(
            localizationService.GetString("Command_WeaponAttackResolution_DamageAbsorbedByTerrain"),
            CoveringHexAbsorption.CoveringHex,
            CoveringHexAbsorption.AbsorbedDamage);
    }

    private void AppendAimedShot(StringBuilder stringBuilder, ILocalizationService localizationService)
    {
        if (AimedShotRoll.Length == 0) return;

        var aimedShotTotal = AimedShotRoll.Sum();
        var localizedInitialLocation = localizationService.GetString($"MechPart_{InitialLocation}_Short");
        var key = aimedShotTotal is >= 6 and <= 8
            ? "Command_WeaponAttackResolution_AimedShotSuccessful"
            : "Command_WeaponAttackResolution_AimedShotFailed";

        stringBuilder.AppendFormat(
            localizationService.GetString(key),
            localizedInitialLocation,
            aimedShotTotal).AppendLine();
    }

    private void AppendFirstHit(StringBuilder stringBuilder, ILocalizationService localizationService)
    {
        var locationRollTotal = LocationRoll.Length > 0 ? LocationRoll.Sum() : 0;
        var firstDamage = Damage[0];
        var isTransfer = InitialLocation != firstDamage.Location;

        stringBuilder.AppendLine(FormatDamageMessage(
            localizationService,
            isTransfer
                ? "Command_WeaponAttackResolution_HitLocationTransfer"
                : "Command_WeaponAttackResolution_HitLocation",
            isTransfer ? InitialLocation : firstDamage.Location,
            isTransfer ? firstDamage.Location : null,
            firstDamage.ArmorDamage,
            firstDamage.StructureDamage,
            locationRollTotal));
    }

    private void AppendExcessDamage(StringBuilder stringBuilder, ILocalizationService localizationService)
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
        var key = GetDamageLocalizationKey(baseKey, armorDamage > 0, structureDamage > 0);
        if (key == null) return string.Empty;

        var template = localizationService.GetString(key);
        var localizedLocation1 = localizationService.GetString($"MechPart_{location1}_Short");

        // A transfer reports both the initial and the final location, a regular hit only one.
        if (location2 is not { } location2Value)
            return FormatDamageValues(template, localizedLocation1, armorDamage, structureDamage, rollInfo);

        var localizedLocation2 = localizationService.GetString($"MechPart_{location2Value}_Short");
        return FormatDamageValues(template, localizedLocation1, localizedLocation2,
            armorDamage, structureDamage, rollInfo);
    }

    private static string? GetDamageLocalizationKey(string baseKey, bool hasArmor, bool hasStructure) =>
        (hasArmor, hasStructure) switch
        {
            (true, true) => $"{baseKey}_ArmorAndStructure",
            (true, false) => $"{baseKey}_ArmorOnly",
            (false, true) => $"{baseKey}_StructureOnly",
            _ => null
        };

    private static string FormatDamageValues(
        string template,
        string localizedLocation,
        int armorDamage,
        int structureDamage,
        int rollInfo)
    {
        if (structureDamage > 0 && armorDamage > 0)
            return string.Format(template, localizedLocation, armorDamage, structureDamage, rollInfo);

        return armorDamage > 0
            ? string.Format(template, localizedLocation, armorDamage, rollInfo)
            : string.Format(template, localizedLocation, structureDamage, rollInfo);
    }

    private static string FormatDamageValues(
        string template,
        string localizedLocation1,
        string localizedLocation2,
        int armorDamage,
        int structureDamage,
        int rollInfo)
    {
        if (structureDamage > 0 && armorDamage > 0)
            return string.Format(template, localizedLocation1, localizedLocation2, armorDamage, structureDamage, rollInfo);

        return armorDamage > 0
            ? string.Format(template, localizedLocation1, localizedLocation2, armorDamage, rollInfo)
            : string.Format(template, localizedLocation1, localizedLocation2, structureDamage, rollInfo);
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
        var key = GetDamageLocalizationKey(
            "Command_WeaponAttackResolution_HitLocationExcessDamage",
            armorDamage > 0,
            structureDamage > 0);
        if (key == null) return string.Empty;

        var localizedLocation = localizationService.GetString($"MechPart_{location}_Short");
        var template = localizationService.GetString(key);

        if (armorDamage > 0 && structureDamage > 0)
            return string.Format(template, localizedLocation, armorDamage, structureDamage);

        return armorDamage > 0
            ? string.Format(template, localizedLocation, armorDamage)
            : string.Format(template, localizedLocation, structureDamage);
    }
}

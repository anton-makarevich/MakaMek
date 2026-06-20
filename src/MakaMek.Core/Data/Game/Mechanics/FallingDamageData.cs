using System.Text;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics;

/// <summary>
/// Contains the results of a falling damage calculation
/// </summary>
public record FallingDamageData(
    HexDirection FacingAfterFall,
    HitLocationsData HitLocations,
    DiceResult FacingDiceRoll,
    HitDirection FallDirection)
{
    public void Render(StringBuilder stringBuilder, ILocalizationService localizationService)
    {
        stringBuilder.AppendFormat(
            localizationService.GetString("Command_MechFalling_Damage"),
            HitLocations.TotalDamage);

        if (HitLocations.HitLocations.Count <= 0) return;
        stringBuilder.AppendLine();
        stringBuilder.AppendLine(localizationService.GetString("Command_WeaponAttackResolution_HitLocations"));

        foreach (var hitLocation in HitLocations.HitLocations)
        {
            stringBuilder.Append(hitLocation.Render(localizationService));
        }
    }
}

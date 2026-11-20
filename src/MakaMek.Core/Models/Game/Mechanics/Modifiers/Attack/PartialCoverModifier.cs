using Sanet.Localization;
using System.Globalization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

/// <summary>
/// Modifier applied when a mechanism has partial cover from terrain
/// </summary>
public record PartialCoverModifier : RollModifier
{
    public override string Render(ILocalizationService localizationService) =>
        string.Format(CultureInfo.InvariantCulture, localizationService.GetString("Modifier_PartialCover"), Value);

    public const int DefaultValue = 1;
}

using Sanet.Localization;
using System.Globalization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

public record SkiddingAttackerModifier : RollModifier
{
    public override string Render(ILocalizationService localizationService) =>
        string.Format(CultureInfo.InvariantCulture, localizationService.GetString("Modifier_SkiddingAttacker"), Value);

    public const int DefaultValue = 1;
}

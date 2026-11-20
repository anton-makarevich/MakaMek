using Sanet.Localization;
using System.Globalization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

public record SkiddingTargetModifier : RollModifier
{
    public override string Render(ILocalizationService localizationService) =>
        string.Format(CultureInfo.InvariantCulture, localizationService.GetString("Modifier_SkiddingTarget"), Value);

    public const int DefaultValue = 2;
}

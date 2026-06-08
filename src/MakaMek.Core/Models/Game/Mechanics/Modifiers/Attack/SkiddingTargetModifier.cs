using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

public record SkiddingTargetModifier : RollModifier
{
    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_SkiddingTarget"), Value);

    public const int DefaultValue = -2;
}

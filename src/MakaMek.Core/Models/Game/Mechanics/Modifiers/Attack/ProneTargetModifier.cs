using Sanet.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

public record ProneTargetModifier : RollModifier
{
    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_ProneTarget"), Value);
}

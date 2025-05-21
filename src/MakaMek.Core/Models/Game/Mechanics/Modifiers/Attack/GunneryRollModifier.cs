using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

public record GunneryRollModifier : RollModifier
{
    public override string Format(ILocalizationService localizationService) => 
        string.Format(localizationService.GetString("Modifier_GunnerySkill"), Value);
}

using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Combat.Modifiers.Attack;

public record HeatRollModifier : RollModifier
{
    public required int HeatLevel { get; init; }

    public override string Format(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_Heat"), 
            HeatLevel, Value);
}

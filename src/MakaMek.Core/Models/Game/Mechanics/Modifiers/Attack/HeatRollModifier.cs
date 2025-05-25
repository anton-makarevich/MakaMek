using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

public record HeatRollModifier : RollModifier
{
    public required int HeatLevel { get; init; }

    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_Heat"), 
            HeatLevel, Value);
}

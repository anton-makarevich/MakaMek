using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;

public record SkiddingModifier : RollModifier
{
    public required int HexesMoved { get; init; }

    public override string Render(ILocalizationService localizationService)
    {
        return string.Format(localizationService.GetString("Modifier_SkidDistance"), HexesMoved, Value.ToString("+0;-0"));
    }
}

using Sanet.Localization;
using System.Globalization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;

public record SkiddingModifier : RollModifier
{
    public required int HexesMoved { get; init; }

    public override string Render(ILocalizationService localizationService)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            localizationService.GetString("Modifier_SkidDistance"),
            HexesMoved,
            Value.ToString("+0;-0", CultureInfo.InvariantCulture));
    }
}

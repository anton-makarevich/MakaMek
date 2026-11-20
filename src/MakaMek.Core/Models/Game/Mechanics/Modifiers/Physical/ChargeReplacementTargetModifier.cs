using Sanet.Localization;
using System.Globalization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;

/// <summary>
/// A charge resolved against a replacement target instead of the originally declared one gets +1
/// to hit (Total Warfare, p. 148).
/// </summary>
public record ChargeReplacementTargetModifier : RollModifier
{
    public const int DefaultValue = 1;

    public override string Render(ILocalizationService localizationService) =>
        string.Format(
            CultureInfo.InvariantCulture,
            localizationService.GetString("Modifier_ChargeReplacementTarget"),
            Value);
}

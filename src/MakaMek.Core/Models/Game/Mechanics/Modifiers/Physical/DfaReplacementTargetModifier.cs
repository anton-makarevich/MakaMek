using Sanet.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;

/// <summary>
/// A Death From Above resolved against a replacement target instead of the originally declared one
/// gets +1 to hit (Total Warfare, Initiative and Displacement).
/// </summary>
public record DfaReplacementTargetModifier : RollModifier
{
    public const int DefaultValue = 1;

    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_DfaReplacementTarget"), Value);
}

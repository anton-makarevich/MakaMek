using Sanet.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;

/// <summary>
/// A Death From Above resolved against a replacement target instead of the originally declared one
/// gets the rules provider's replacement-target modifier (Total Warfare, Initiative and
/// Displacement).
/// </summary>
public record DfaReplacementTargetModifier : RollModifier
{
    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_DfaReplacementTarget"), Value);
}

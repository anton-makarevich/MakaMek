using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

/// <summary>
/// Modifier applied when a mech is prone and firing weapons
/// </summary>
public record ProneAttackerModifier : RollModifier
{
    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_ProneFiring"), Value);
}

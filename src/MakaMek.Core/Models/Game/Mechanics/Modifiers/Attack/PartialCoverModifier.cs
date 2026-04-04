using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

/// <summary>
/// Modifier applied when a mechanism has partial cover from terrain
/// </summary>
public record PartialCoverModifier : RollModifier
{
    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_PartialCover"), Value);

    public const int DefaultValue = 1;
}

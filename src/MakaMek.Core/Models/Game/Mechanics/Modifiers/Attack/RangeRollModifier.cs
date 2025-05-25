using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

public record RangeRollModifier : RollModifier
{
    public required WeaponRange Range { get; init; }
    public required int Distance { get; init; }
    public required string WeaponName { get; init; }

    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_Range"), 
            WeaponName, Distance, Range, Value);
}

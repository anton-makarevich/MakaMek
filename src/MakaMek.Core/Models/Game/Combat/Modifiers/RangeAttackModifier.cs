using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Combat.Modifiers;

public record RangeAttackModifier : AttackModifier
{
    public required WeaponRange Range { get; init; }
    public required int Distance { get; init; }
    public required string WeaponName { get; init; }

    public override string Format(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_Range"), 
            WeaponName, Distance, Range, Value);
}

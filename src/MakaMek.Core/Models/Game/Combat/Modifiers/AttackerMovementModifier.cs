using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Combat.Modifiers;

public record AttackerMovementModifier : AttackModifier
{
    public required MovementType MovementType { get; init; }

    public override string Format(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_AttackerMovement"), 
            MovementType, Value);
}

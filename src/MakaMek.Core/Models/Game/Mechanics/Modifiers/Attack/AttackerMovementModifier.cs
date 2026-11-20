using Sanet.Localization;
using Sanet.MakaMek.Map.Models;
using System.Globalization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

public record AttackerMovementModifier : RollModifier
{
    public required MovementType MovementType { get; init; }

    public override string Render(ILocalizationService localizationService) =>
        string.Format(CultureInfo.InvariantCulture, localizationService.GetString("Modifier_AttackerMovement"), 
            MovementType, Value);
}

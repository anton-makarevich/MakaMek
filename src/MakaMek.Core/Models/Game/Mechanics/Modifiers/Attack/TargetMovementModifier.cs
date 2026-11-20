using Sanet.Localization;
using System.Globalization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

public record TargetMovementModifier : RollModifier
{
    public required int HexesMoved { get; init; }

    public override string Render(ILocalizationService localizationService) =>
        string.Format(CultureInfo.InvariantCulture, localizationService.GetString("Modifier_TargetMovement"), 
            HexesMoved, Value);
}

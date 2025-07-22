using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

/// <summary>
/// Modifier applied when a lower arm actuator is destroyed, affecting weapons in the same arm
/// </summary>
public record LowerArmActuatorHitModifier : RollModifier
{
    /// <summary>
    /// The location of the arm with the destroyed actuator
    /// </summary>
    public required PartLocation ArmLocation { get; init; }

    public override string Render(ILocalizationService localizationService) =>
        string.Format(
            localizationService.GetString("Modifier_LowerArmActuatorHit"), 
            ArmLocation.ToString().ToLower(), 
            Value
        );
}

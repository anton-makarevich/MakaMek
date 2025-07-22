using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

/// <summary>
/// Modifier applied when a shoulder actuator is destroyed, affecting weapons in the same arm
/// This modifier overrides other arm critical hit modifiers
/// </summary>
public record ShoulderActuatorHitModifier : RollModifier
{
    /// <summary>
    /// The location of the arm with the destroyed shoulder actuator
    /// </summary>
    public required PartLocation ArmLocation { get; init; }

    public override string Render(ILocalizationService localizationService) =>
        string.Format(
            localizationService.GetString("Modifier_ShoulderActuatorHit"), 
            ArmLocation.ToString().ToLower(), 
            Value
        );
}

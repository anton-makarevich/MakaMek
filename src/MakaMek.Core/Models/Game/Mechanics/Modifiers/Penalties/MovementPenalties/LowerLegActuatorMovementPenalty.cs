using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;

/// <summary>
/// Movement penalty applied due to destroyed lower leg actuators (-1 MP per destroyed actuator)
/// </summary>
public record LowerLegActuatorMovementPenalty : RollModifier
{
    /// <summary>
    /// Number of destroyed lower leg actuators
    /// </summary>
    public required int DestroyedCount { get; init; }
    
    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Penalty_LowerLegActuatorMovement"), 
            DestroyedCount, Value);
}
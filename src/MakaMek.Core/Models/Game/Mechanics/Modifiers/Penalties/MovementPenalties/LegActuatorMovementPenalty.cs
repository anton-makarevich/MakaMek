using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;

/// <summary>
/// Movement penalty applied due to destroyed foot/leg actuators (-1 MP per destroyed actuator)
/// </summary>
public record LegActuatorMovementPenalty : RollModifier
{
    /// <summary>
    /// Type of actuator that was destroyed
    /// </summary>
    public required MakaMekComponent DestroyedActuator { get; init; }
    
    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Penalty_LegActuatorMovement"), 
            DestroyedActuator, Value);
}

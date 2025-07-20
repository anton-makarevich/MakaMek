using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.AttackPenalties;

/// <summary>
/// Attack penalty applied due to excessive heat levels
/// </summary>
public record HeatAttackPenalty : RollModifier
{
    /// <summary>
    /// Current heat level of the unit
    /// </summary>
    public required int HeatLevel { get; init; }
    
    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Penalty_HeatAttack"), 
            HeatLevel, Value);
}

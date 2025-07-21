using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.HeatPenalties;

/// <summary>
/// Heat penalty applied due to engine damage (+5/+10 heat per turn)
/// </summary>
public record EngineHeatPenalty : RollModifier
{
    /// <summary>
    /// Number of engine hits
    /// </summary>
    public required int EngineHits { get; init; }
    
    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Penalty_EngineHeat"), 
            EngineHits, Value);
}

using Sanet.Localization;
using System.Globalization;

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
        string.Format(CultureInfo.InvariantCulture, localizationService.GetString("Penalty_EngineHeat"), 
            EngineHits, Value);
}

using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.HeatPenalties;

namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Comprehensive heat data for a unit, including sources, dissipation, and totals
/// </summary>
public readonly record struct HeatData()
{
    /// <summary>
    /// The maximum amount of external heat that can be applied to the unit
    /// </summary>
    public int ExternalHeatCap { get; init; } = 15;
    
    /// <summary>
    /// Heat generated from movement
    /// </summary>
    public required List<MovementHeatData> MovementHeatSources { get; init; }

    /// <summary>
    /// Heat generated from weapons
    /// </summary>
    public required List<WeaponHeatData> WeaponHeatSources { get; init; }

    /// <summary>
    /// Heat applied from external sources (e.g., Flamers), capped at 15 points maximum
    /// </summary>
    public required List<ExternalHeatData> ExternalHeatSources { get; init; }

    /// <summary>
    /// Heat dissipation information
    /// </summary>
    public required HeatDissipationData DissipationData { get; init; }

    /// <summary>
    /// Heat penalty from engine damage
    /// </summary>
    public EngineHeatPenalty? EngineHeatSource { get; init; }
    
    public int ExternalHeatPoints => Math.Min(ExternalHeatSources.Sum(source => source.HeatPoints), ExternalHeatCap);

    /// <summary>
    /// Total heat to apply to the unit
    /// </summary>
    public int TotalHeatPoints =>
        MovementHeatSources.Sum(source => source.HeatPoints) +
        WeaponHeatSources.Sum(source => source.HeatPoints) +
        ExternalHeatPoints +
        (EngineHeatSource?.Value ?? 0);

    /// <summary>
    /// Total heat to dissipate
    /// </summary>
    public int TotalHeatDissipationPoints => DissipationData.DissipationPoints;
}

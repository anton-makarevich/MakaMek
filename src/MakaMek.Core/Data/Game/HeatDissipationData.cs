namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Heat dissipation source
/// </summary>
public record struct HeatDissipationData
{
    public required int HeatSinks { get; init; }
    public required int EngineHeatSinks { get; init; }
    public required int DissipationPoints { get; init; }
    public required int WaterDissipationBonus { get; init; }
}

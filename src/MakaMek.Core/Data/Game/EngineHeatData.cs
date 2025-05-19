namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Heat generated from engine damage
/// </summary>
public readonly record struct EngineHeatData
{
    /// <summary>
    /// Number of hits the engine has taken
    /// </summary>
    public required int Hits { get; init; }
    
    /// <summary>
    /// Heat points generated due to engine damage
    /// </summary>
    public required int HeatPoints { get; init; }
}

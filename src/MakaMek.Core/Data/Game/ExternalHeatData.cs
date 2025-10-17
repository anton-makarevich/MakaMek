namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Heat applied to a target from external sources (e.g., Flamers)
/// </summary>
public record struct ExternalHeatData
{
    public required string WeaponName { get; init; }
    public required int HeatPoints { get; init; }
}


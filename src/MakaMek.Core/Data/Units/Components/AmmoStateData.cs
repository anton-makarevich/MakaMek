namespace Sanet.MakaMek.Core.Data.Units.Components;

/// <summary>
/// State data specific to ammunition components
/// </summary>
public record AmmoStateData(int RemainingShots) : ComponentSpecificData;
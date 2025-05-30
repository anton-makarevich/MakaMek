namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Represents all locations receiving damage
/// </summary>
public record HitLocationsData(
    List<HitLocationData> HitLocations,
    int TotalDamage);
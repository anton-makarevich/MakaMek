namespace Sanet.MakaMek.Core.Data.Game;

using Sanet.MakaMek.Core.Models.Game.Dice;

/// <summary>
/// Represents all hit locations from a weapon attack
/// </summary>
public record AttackHitLocationsData(
    List<HitLocationData> HitLocations,
    int TotalDamage,
    List<DiceResult> ClusterRoll,
    int MissilesHit):HitLocationsData(HitLocations,TotalDamage);
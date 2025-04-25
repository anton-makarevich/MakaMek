using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Represents a single hit location with its damage and optional critical hits (slot indexes)
/// </summary>
public record HitLocationData(
    PartLocation Location,
    int Damage,
    List<DiceResult> LocationRoll,
    List<LocationCriticalHitsData>? CriticalHits = null, // Optional: detailed critical hits info for all affected locations, null if none
    PartLocation? InitialLocation = null
);
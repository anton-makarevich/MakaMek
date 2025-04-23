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
    CriticalHitsData? CriticalHits = null, // Optional: detailed critical hits info, null if none
    PartLocation? InitialLocation = null, // Optional: the initial hit location before transfer, null if no transfer occurred
    bool IsBlownOff = false // Indicates if the location is blown off (for head and limbs on critical roll of 12)
);
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Data.Game;

namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Represents a single hit location with its damage and optional critical hits (slot indexes)
/// </summary>
public record HitLocationData(
    PartLocation Location,
    int Damage,
    List<DiceResult> LocationRoll,
    CriticalHitsData? CriticalHits = null // Optional: detailed critical hits info, null if none
);
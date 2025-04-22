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
    int[]? CriticalHits = null // Optional: indexes of slots hit by critical hits, null if none
);
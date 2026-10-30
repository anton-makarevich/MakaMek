namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;

/// <summary>
/// Why a forced entry into a hex is impossible.
/// </summary>
public enum DisplacementFailureReason
{
    /// <summary>The destination is outside the map or has no hex data.</summary>
    OffMap = 0,

    /// <summary>The destination has no surface the unit can stand on.</summary>
    ProhibitedTerrain = 1,

    /// <summary>The destination is two or more levels above the source.</summary>
    TooHigh = 2,

    /// <summary>The chain did not terminate within the iteration limit.</summary>
    IterationLimit = 3
}
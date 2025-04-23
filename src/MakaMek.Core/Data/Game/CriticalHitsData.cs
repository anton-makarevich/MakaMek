namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Contains detailed information about critical hits: the roll made, number of crits, and the slots hit.
/// </summary>
public record CriticalHitsData(
    int Roll,
    int NumCriticalHits,
    int[]? CriticalHits
);

using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Contains detailed information about critical hits for a specific location: the location, roll made, number of crits, and the slots hit.
/// </summary>
public record LocationCriticalHitsData(
    PartLocation Location,
    int Roll,
    int NumCriticalHits,
    ComponentHitData[]? HitComponents,
    bool IsBlownOff = false // Indicates if the location is blown off (for head and limbs on critical roll of 12)
);

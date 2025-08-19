using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Contains complete critical hits resolution data for a specific location, including explosions and cascading effects
/// </summary>
public record LocationCriticalHitsResolutionData(
    PartLocation Location,
    int StructureDamageReceived,
    int CriticalHitRoll,
    int NumCriticalHits,
    ComponentHitData[]? HitComponents,
    bool IsBlownOff = false,
    List<ExplosionData>? Explosions = null
);

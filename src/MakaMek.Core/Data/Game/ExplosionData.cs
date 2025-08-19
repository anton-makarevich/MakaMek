using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Contains information about a component explosion and its cascading effects
/// </summary>
public record ExplosionData(
    MakaMekComponent ComponentType,
    int Slot,
    int ExplosionDamage
);

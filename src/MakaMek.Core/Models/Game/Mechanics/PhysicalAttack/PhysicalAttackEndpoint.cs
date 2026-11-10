namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// The map-derived facts a physical attack needs about one endpoint: the effective standing
/// level (water depth counts as negative levels), whether the unit is fully submerged, and
/// whether it occupies water.
/// </summary>
public readonly record struct PhysicalAttackEndpoint(
    int EffectiveStandingLevel,
    bool IsSubmerged,
    bool IsInWater);

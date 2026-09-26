using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// The outcome of simulating a heat-induced ammo explosion.
/// The critical hits are applied to the authoritative unit later, when the resulting command is
/// handled, so the destruction metadata has to travel with them rather than being observed on the
/// unit itself.
/// </summary>
/// <param name="CriticalHits">The critical hits caused by the explosion, starting at the component's location.</param>
/// <param name="DestroyedParts">Locations the explosion destroys, or <see langword="null"/> when it destroys none.</param>
/// <param name="UnitDestroyed">Whether the explosion destroys a unit that was not already destroyed.</param>
public record HeatExplosionResolution(
    List<LocationCriticalHitsData> CriticalHits,
    List<PartLocation>? DestroyedParts,
    bool UnitDestroyed)
{
    public static HeatExplosionResolution None => new([], null, false);
}

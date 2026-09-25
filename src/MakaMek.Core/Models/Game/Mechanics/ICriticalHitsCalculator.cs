using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

public interface ICriticalHitsCalculator
{
    /// <summary>
    /// Calculates critical hits for locations that received structure damage without mutating the supplied unit.
    /// Returns a command describing all per-location results (including zero-crit entries) or null when no locations with structure damage produced any result.
    /// </summary>
    /// <param name="unit">The target unit</param>
    /// <param name="hitLocationsData">The hit locations data containing damage information</param>
    CriticalHitsResolutionCommand? CalculateCriticalHits(IUnit unit, List<LocationDamageData> hitLocationsData);

    /// <summary>
    /// Calculates critical hits for a heat-induced component explosion without mutating the supplied unit.
    /// </summary>
    /// <param name="unit">The owning unit receiving the explosion effects</param>
    /// <param name="explodingComponent">The component that exploded due to heat</param>
    /// <returns>
    /// The critical-hit data beginning at the component's location, together with the destruction the
    /// explosion causes. The destruction is reported rather than applied, so it cannot be read back
    /// off <paramref name="unit"/>.
    /// </returns>
    HeatExplosionResolution CalculateCriticalHitsForHeatExplosion(
        Unit unit,
        Ammo explodingComponent);
}

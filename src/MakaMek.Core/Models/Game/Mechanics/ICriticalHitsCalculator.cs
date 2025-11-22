using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

public interface ICriticalHitsCalculator
{
    /// <summary>
    /// Calculates and applies critical hits for locations that received structure damage.
    /// Returns a command describing all per-location results (including zero-crit entries) or null when no locations with structure damage produced any result.
    /// </summary>
    /// <param name="unit">The target unit</param>
    /// <param name="hitLocationsData">The hit locations data containing damage information</param>
    CriticalHitsResolutionCommand? CalculateAndApplyCriticalHits(IUnit unit, List<LocationDamageData> hitLocationsData);

    /// <summary>
    /// Calculates critical hits for heat-induced component explosion
    /// </summary>
    /// <param name="unit">The owning unit receiving the explosion effects</param>
    /// <param name="explodingComponent">The component that exploded due to heat</param>
    /// <returns>Critical-hit data beginning at the component's location</returns>
    List<LocationCriticalHitsData> CalculateCriticalHitsForHeatExplosion(
        Unit unit,
        Ammo explodingComponent);
}
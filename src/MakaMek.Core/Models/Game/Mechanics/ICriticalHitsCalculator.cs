using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

public interface ICriticalHitsCalculator
{
    /// <summary>
    /// Calculates critical hits for all locations that received structure damage
    /// </summary>
    /// <param name="unit">The unit receiving damage</param>
    /// <param name="structureDamageByLocation">Dictionary mapping locations to their structure damage</param>
    /// <returns>A list of LocationCriticalHitsResolutionData for all affected locations</returns>
    List<LocationCriticalHitsResolutionData> CalculateCriticalHitsForStructureDamage(
        Unit unit,
        LocationDamageData damageData);

    /// <summary>
    /// Calculates critical hits for heat-induced component explosion
    /// </summary>
    /// <param name="unit">The owning unit receiving the explosion effects</param>
    /// <param name="explodingComponent">The component that exploded due to heat</param>
    /// <returns>Critical-hit data beginning at the component's location</returns>
    List<LocationCriticalHitsResolutionData> CalculateCriticalHitsForHeatExplosion(
        Unit unit,
        Component explodingComponent);
}
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

public interface ICriticalHitsCalculator
{
    /// <summary>
    /// Calculates critical hits for all locations that received structure damage
    /// </summary>
    /// <param name="unit">The unit receiving damage</param>
    /// <param name="damageData">Precomputed damage for a specific location (armor/structure and destroyed flag).</param>
    /// <returns>A LocationCriticalHitsData originating at the damaged location (including explosions).</returns>
    LocationCriticalHitsData? CalculateCriticalHitsForStructureDamage(
        Unit unit,
        LocationDamageData damageData);

    /// <summary>
    /// Calculates critical hits for heat-induced component explosion
    /// </summary>
    /// <param name="unit">The owning unit receiving the explosion effects</param>
    /// <param name="explodingComponent">The component that exploded due to heat</param>
    /// <returns>Critical-hit data beginning at the component's location</returns>
    LocationCriticalHitsData? CalculateCriticalHitsForHeatExplosion(
        Unit unit,
        Ammo explodingComponent);
}
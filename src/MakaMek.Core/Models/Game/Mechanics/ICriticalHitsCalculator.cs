using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

public interface ICriticalHitsCalculator
{
    /// <summary>
    /// Calculates critical hits for all locations in a damage chain without applying damage
    /// </summary>
    /// <param name="unit">The unit receiving damage</param>
    /// <param name="initialLocation">The initial hit location</param>
    /// <param name="damage">The total damage to apply</param>
    /// <returns>A list of LocationCriticalHitsData for all affected locations</returns>
    List<LocationCriticalHitsData> CalculateCriticalHits(
        Unit unit, 
        PartLocation initialLocation, 
        int damage);
    
    /// <summary>
    /// Builds a forced critical-hit chain starting from a destroyed component (e.g., ammo explosion)
    /// without applying any damage or mutating unit state.
    /// </summary>
    /// <param name="unit">The owning unit receiving the explosion effects.</param>
    /// <param name="component">The component considered destroyed; its location anchors the crit chain.</param>
    /// <returns>Critical-hit data beginning at the component's location.</returns>
    List<LocationCriticalHitsData> GetCriticalHitsForDestroyedComponent(
        Unit unit, 
        Component component);
}
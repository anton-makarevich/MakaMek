using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Service for calculating structure damage distribution without applying it to units
/// </summary>
public interface IStructureDamageCalculator
{
    /// <summary>
    /// Calculates how damage would be distributed across locations without applying it
    /// </summary>
    /// <param name="unit">The unit receiving damage</param>
    /// <param name="initialLocation">The initial hit location</param>
    /// <param name="totalDamage">The total damage to distribute</param>
    /// <param name="hitDirection">The direction of the hit for armor calculations</param>
    /// <returns>Mapping of locations to their armor and structure damage</returns>
    List<LocationDamageData> CalculateStructureDamage(
        Unit unit,
        PartLocation initialLocation,
        int totalDamage,
        HitDirection hitDirection);

    /// <summary>
    /// Calculates how explosion damage would be distributed across locations without applying it.
    /// Explosion damage bypasses armor entirely and applies directly to structure.
    /// </summary>
    /// <param name="unit">The unit receiving damage</param>
    /// <param name="initialLocation">The initial hit location</param>
    /// <param name="totalDamage">The total damage to distribute</param>
    /// <returns>Mapping of locations to their structure damage (armor damage will always be 0)</returns>
    List<LocationDamageData> CalculateExplosionDamage(
        Unit unit,
        PartLocation initialLocation,
        int totalDamage);
}
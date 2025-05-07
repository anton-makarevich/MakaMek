using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Combat;

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
}

/// <summary>
/// Calculator for determining critical hits for all locations in a damage chain
/// </summary>
public class CriticalHitsCalculator : ICriticalHitsCalculator
{
    private readonly IDiceRoller _diceRoller;

    public CriticalHitsCalculator(IDiceRoller diceRoller)
    {
        _diceRoller = diceRoller;
    }

    /// <summary>
    /// Calculates critical hits for all locations in a damage chain without applying damage
    /// </summary>
    /// <param name="unit">The unit receiving damage</param>
    /// <param name="initialLocation">The initial hit location</param>
    /// <param name="damage">The total damage to apply</param>
    /// <returns>A list of LocationCriticalHitsData for all affected locations</returns>
    public List<LocationCriticalHitsData> CalculateCriticalHits(
        Unit unit, 
        PartLocation initialLocation, 
        int damage)
    {
        var criticalHits = new List<LocationCriticalHitsData>();
        CalculateCriticalHitsRecursively(unit, initialLocation, damage, criticalHits);
        return criticalHits;
    }

    /// <summary>
    /// Recursively calculates critical hits for a location and any following locations in the damage chain
    /// </summary>
    /// <param name="unit">The unit receiving damage</param>
    /// <param name="location">The current location</param>
    /// <param name="damage">The damage to apply</param>
    /// <param name="criticalHits">The list to collect critical hits data</param>
    /// <returns>Any remaining damage after this location</returns>
    private int CalculateCriticalHitsRecursively(
        Unit unit,
        PartLocation location, 
        int damage, 
        List<LocationCriticalHitsData> criticalHits)
    {
        var part = unit.Parts.FirstOrDefault(p => p.Location == location);
        if (part == null)
            return damage;

        // Calculate armor damage
        var armorDamage = Math.Min(damage, part.CurrentArmor);
        var remainingDamage = damage - armorDamage;

        // Calculate structure damage if armor is depleted
        if (remainingDamage > 0 && part.CurrentStructure > 0)
        {
            var structureDamage = Math.Min(remainingDamage, part.CurrentStructure);
            remainingDamage -= structureDamage;

            // Calculate critical hits if the structure was damaged
            var criticalHitsData = unit.CalculateCriticalHitsData(location, _diceRoller);
            if (criticalHitsData != null)
            {
                criticalHits.Add(criticalHitsData);
                
                // Check for explodable components that would be hit by these critical hits
                if (criticalHitsData.CriticalHits is { Length: > 0 })
                {
                    var explosionDamage = 0;
                    
                    foreach (var slot in criticalHitsData.CriticalHits)
                    {
                        var component = part.GetComponentAtSlot(slot);
                        if (component is { CanExplode: true, HasExploded: false })
                        {
                            // Add explosion damage to the total
                            explosionDamage += component.GetExplosionDamage();
                        }
                    }
                    
                    // Add explosion damage to remaining damage to propagate through the damage chain
                    if (explosionDamage > 0)
                    {
                        remainingDamage += explosionDamage;
                    }
                }
            }
        }

        if (remainingDamage <= 0) return remainingDamage;
        // If the structure is destroyed and there's still damage remaining, propagate to the next location
        var nextLocation = unit.GetTransferLocation(location);
        if (nextLocation.HasValue)
        {
            // Recursively calculate critical hits for the next location
            remainingDamage = CalculateCriticalHitsRecursively(
                unit, nextLocation.Value, remainingDamage, criticalHits);
        }

        return remainingDamage;
    }
}
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Calculator for determining critical hits based on structure damage
/// </summary>
public class CriticalHitsCalculator : ICriticalHitsCalculator
{
    private readonly IDiceRoller _diceRoller;

    public CriticalHitsCalculator(IDiceRoller diceRoller)
    {
        _diceRoller = diceRoller;
    }

    public List<LocationCriticalHitsData> CalculateCriticalHitsForStructureDamage(
        Unit unit,
        LocationDamageData damageData)
    {
        var criticalHitsData = new List<LocationCriticalHitsData>();

        if (damageData.StructureDamage <= 0) return criticalHitsData;

        var locationCriticalHits = CalculateCriticalHitsForLocation(
            unit, damageData.Location, damageData.StructureDamage);
        if (locationCriticalHits != null)
        {
            criticalHitsData.Add(locationCriticalHits);
        }

        return criticalHitsData;
    }

    public List<LocationCriticalHitsData> CalculateCriticalHitsForHeatExplosion(
        Unit unit,
        Ammo explodingComponent) // only ammo can explode from heat
    {
        var explosionDamage = explodingComponent.GetExplosionDamage();
        if (explosionDamage <= 0) return []; //no possible damage, no explosion
        
        var location = explodingComponent.GetLocation();
        if (!location.HasValue) return [];

        // Ensure the component has a resolvable slot
        var slots = explodingComponent.MountedAtSlots;
        if (slots.Length == 0)
            return [];

        // Create the initial forced critical hit for the exploding component
        var componentHitData = new ComponentHitData
        {
            Type = explodingComponent.ComponentType,
            Slot = slots[0]
        };

        // Get explosion damage if the component can explode
        var explosions = new List<ExplosionData>();
        if (explodingComponent is { CanExplode: true, HasExploded: false })
        {
                explosions.Add(new ExplosionData(
                    explodingComponent.ComponentType,
                    slots[0],
                    explosionDamage));
        }

        var criticalHit = new LocationCriticalHitsData(
            location.Value,
            [], // No roll for forced critical hit
            1, // One forced critical hit
            [componentHitData],
            false, // Not blown off
            explosions
        );

        var criticalHitsData = new List<LocationCriticalHitsData> { criticalHit };

        // If there was an explosion, calculate cascading critical hits from the explosion damage
        if (explosions.Count != 0)
        {
            var totalExplosionDamage = explosions.Sum(e => e.ExplosionDamage);
            // Calculate critical hits caused by explosion structure damage cascading across locations
            var cascadeHits = CalculateCriticalHitsForExplosionCascade(unit, location.Value, totalExplosionDamage);
            if (cascadeHits.Count > 0)
                criticalHitsData.AddRange(cascadeHits);
        }

        return criticalHitsData;
    }

    /// <summary>
    /// Calculates critical hits for a specific location that received structure damage
    /// </summary>
    private LocationCriticalHitsData? CalculateCriticalHitsForLocation(
        Unit unit,
        PartLocation location,
        int structureDamage)
    {
        var part = unit.Parts.FirstOrDefault(p => p.Location == location);
        if (part is not { CurrentStructure: > 0 } || structureDamage <= 0)
            return null;

        // Roll for critical hits
        var criticalHitsData = unit.CalculateCriticalHitsData(location, _diceRoller);
        if (criticalHitsData == null)
            return null;

        // Check for explosions from hit components
        var explosions = new List<ExplosionData>();
        if (criticalHitsData.HitComponents == null)
            return criticalHitsData with
            {
                Location = location, Explosions = explosions
            };
        foreach (var componentData in criticalHitsData.HitComponents)
        {
            var component = part.GetComponentAtSlot(componentData.Slot);
            if (component is not { CanExplode: true, HasExploded: false }) continue;
            var explosionDamage = component.GetExplosionDamage();
            if (explosionDamage > 0)
            {
                explosions.Add(new ExplosionData(
                    component.ComponentType,
                    componentData.Slot,
                    explosionDamage));
            }
        }

        return criticalHitsData with { Location = location, Explosions = explosions };
    }

    /// <summary>
    /// Calculates critical hits resulting from an explosion's structure-only damage that cascades
    /// through the damage transfer chain. This does not mutate the unit; it simulates using current
    /// structure values and transfers excess damage to the next location as per rules.
    /// </summary>
    private List<LocationCriticalHitsData> CalculateCriticalHitsForExplosionCascade(
        Unit unit,
        PartLocation startLocation,
        int explosionDamage)
    {
        var result = new List<LocationCriticalHitsData>();
        if (explosionDamage <= 0) return result;

        var remaining = explosionDamage;
        PartLocation? current = startLocation;

        while (remaining > 0 && current.HasValue)
        {
            var part = unit.Parts.FirstOrDefault(p => p.Location == current.Value);
            if (part is null) break;

            // Explosion applies structure-only damage; armor is ignored
            var availableStructure = Math.Max(0, part.CurrentStructure);
            if (availableStructure <= 0)
            {
                // Already destroyed, continue transfer without rolling here
                current = unit.GetTransferLocation(current.Value);
                continue;
            }

            var structureDamageHere = Math.Min(remaining, availableStructure);

            // Roll for critical hits at this location if any structure damage is applied
            if (structureDamageHere > 0)
            {
                var crits = CalculateCriticalHitsForLocation(unit, current.Value, structureDamageHere);
                if (crits != null)
                {
                    result.Add(crits);
                }
            }

            // Determine if we transfer further
            var locationDestroyedByThis = structureDamageHere >= availableStructure;
            remaining -= structureDamageHere;
            if (remaining > 0 && locationDestroyedByThis)
            {
                current = unit.GetTransferLocation(current.Value);
            }
            else
            {
                break;
            }
        }

        return result;
    }
}
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
        if (explosionDamage <= 0) return []; //no possible damage no explosion
        
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
        if (explosions.Any())
        {
            var totalExplosionDamage = explosions.Sum(e => e.ExplosionDamage);
            var cascadingCriticalHits = CalculateCriticalHitsForLocation(unit, location.Value, totalExplosionDamage);
            if (cascadingCriticalHits != null)
            {
                criticalHitsData.Add(cascadingCriticalHits);
            }
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
}
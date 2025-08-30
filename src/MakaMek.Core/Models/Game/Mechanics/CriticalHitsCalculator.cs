using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
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
    private readonly IDamageTransferCalculator _damageTransferCalculator;

    public CriticalHitsCalculator(IDiceRoller diceRoller, IDamageTransferCalculator damageTransferCalculator)
    {
        _diceRoller = diceRoller;
        _damageTransferCalculator = damageTransferCalculator;
    }
    
    public CriticalHitsResolutionCommand? CalculateCriticalHits(Unit target, AttackHitLocationsData hitLocationsData)
    {
        var allCriticalHitsData = new List<LocationCriticalHitsData>();

        // Process each location that received damage
        var locationsWithStructureDamage = new Queue<LocationDamageData>( hitLocationsData.HitLocations
            .SelectMany(l => l.Damage)
            .Where(d => d.StructureDamage > 0));
        
        while (locationsWithStructureDamage.Count > 0)
        {
            var locationHitDamage = locationsWithStructureDamage.Dequeue();
            var criticalHitsData = CalculateCriticalHitsForStructureDamage(target, locationHitDamage);
            if (criticalHitsData != null)
            {
                target.ApplyCriticalHits([criticalHitsData]);
                allCriticalHitsData.Add(criticalHitsData);
            }
            var explosions = criticalHitsData?.ExplosionsDamage ?? [];
            foreach (var explosion in explosions)
            {
                if (explosion.StructureDamage > 0)
                    locationsWithStructureDamage.Enqueue(explosion); // Add any explosion damage to the queue
            }
        }

        // If no critical hits occurred, no need to send a command
        if (allCriticalHitsData.Count == 0)
            return null;

        // Send critical hits resolution command
        return new CriticalHitsResolutionCommand
        {
            GameOriginId = Guid.Empty,
            TargetId = target.Id,
            CriticalHits = allCriticalHitsData
        };
    }

    private LocationCriticalHitsData? CalculateCriticalHitsForStructureDamage(
        Unit unit,
        LocationDamageData damageData)
    {
        if (damageData.StructureDamage <= 0) return null;

        return CalculateCriticalHitsForLocation(
            unit, damageData.Location, damageData.StructureDamage);
    }

    public LocationCriticalHitsData? CalculateCriticalHitsForHeatExplosion(
        Unit unit,
        Ammo explodingComponent) // only ammo can explode from heat
    {
        var explosionDamage = explodingComponent.GetExplosionDamage();
        if (explosionDamage <= 0) return null; //no possible damage, no explosion
        
        var location = explodingComponent.GetLocation();
        if (!location.HasValue) return null;

        // Ensure the component has a resolvable slot
        var slots = explodingComponent.MountedAtSlots;
        if (slots.Length == 0)
            return null;

        // Create the initial forced critical hit for the exploding component
        var componentHitData = new ComponentHitData
        {
            Type = explodingComponent.ComponentType,
            Slot = slots[0],
            ExplosionDamage = explosionDamage
        };

        // Get explosion damage if the component can explode
        var explosions = _damageTransferCalculator.CalculateExplosionDamage(unit, location.Value, explosionDamage);

        return new LocationCriticalHitsData(
            location.Value,
            [], // No roll for forced critical hit
            1, // One forced critical hit
            [componentHitData],
            false, // Not blown off
            explosions
        );
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
        var explosions = new List<LocationDamageData>();
        if (criticalHitsData.HitComponents == null)
            return criticalHitsData with
            {
                Location = location, ExplosionsDamage = explosions
            };
        foreach (var componentData in criticalHitsData.HitComponents)
        {
            var component = part.GetComponentAtSlot(componentData.Slot);
            if (component is not { CanExplode: true, HasExploded: false }) continue;
            var explosionDamage = component.GetExplosionDamage();
            if (explosionDamage <= 0) continue;
            var perComponent = _damageTransferCalculator
                .CalculateExplosionDamage(unit, location, explosionDamage);
            if (perComponent is { Count: > 0 })
                explosions.AddRange(perComponent);
        }

        return criticalHitsData with { Location = location, ExplosionsDamage = explosions };
    }
}
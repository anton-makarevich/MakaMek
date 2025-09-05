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
    
    public CriticalHitsResolutionCommand? CalculateAndApplyCriticalHits(Unit unit, List<LocationDamageData> hitLocationsData)
    {
        var allCriticalHitsData = ProcessAndApplyCriticalHitsDamage(unit, hitLocationsData);

        // If no critical hits occurred, no need to send a command
        if (allCriticalHitsData.Count == 0)
            return null;

        // Send critical hits resolution command
        return new CriticalHitsResolutionCommand
        {
            GameOriginId = Guid.Empty,
            TargetId = unit.Id,
            CriticalHits = allCriticalHitsData
        };
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
        
        var explosionDamageData = _damageTransferCalculator
            .CalculateExplosionDamage(unit, location.Value, explosionDamage);

        // Create the initial forced critical hit for the exploding component
        var componentHitData = new ComponentHitData
        {
            Type = explodingComponent.ComponentType,
            Slot = slots[0],
            ExplosionDamage = explosionDamage,
            ExplosionDamageDistribution = explosionDamageData.ToArray()
        };
        
        var explosionConsequences = 
            ProcessAndApplyCriticalHitsDamage(unit, explosionDamageData.ToList());

        return new List<LocationCriticalHitsData>
        {
            new(
            location.Value,
            [], // No roll for forced critical hit
            1, // One forced critical hit
            [componentHitData],
            false // Not blown off
        )}
            .Concat(explosionConsequences).ToList();
    }
    
    private List<LocationCriticalHitsData> ProcessAndApplyCriticalHitsDamage(Unit unit, List<LocationDamageData> hitLocationsData)
    {
        var allCriticalHitsData = new List<LocationCriticalHitsData>();

        // Process each location that received damage
        var locationsWithStructureDamage = new Queue<LocationDamageData>( hitLocationsData
            .Where(d => d.StructureDamage > 0));
        
        while (locationsWithStructureDamage.Count > 0)
        {
            var locationHitDamage = locationsWithStructureDamage.Dequeue();
            var criticalHitsData = CalculateCriticalHitsForLocation(unit, locationHitDamage.Location, locationHitDamage.StructureDamage);
            if (criticalHitsData != null)
            {
                // KNOWN ISSUE
                // This is a side effect and a deviation from the common design of applying server commands via BaseGame 
                unit.ApplyCriticalHits([criticalHitsData]);
                allCriticalHitsData.Add(criticalHitsData);
            }
            var explosions = criticalHitsData?
                .HitComponents?.SelectMany(c => c.ExplosionDamageDistribution ?? []) ?? [];
            foreach (var explosion in explosions)
            {
                if (explosion.StructureDamage > 0)
                    locationsWithStructureDamage.Enqueue(explosion); // Add any explosion damage to the queue
            }
        }

        return allCriticalHitsData;
    }

    /// <summary>
    /// Calculates critical hits for a specific location that received structure damage
    /// </summary>
    private LocationCriticalHitsData? CalculateCriticalHitsForLocation(
        Unit unit,
        PartLocation location,
        int structureDamage)
    {
        if (!unit.Parts.TryGetValue(location, out var part) 
            || part is not { CurrentStructure: > 0 } || structureDamage <= 0)
            return null;

        // Roll for critical hits
        var criticalHitsData = unit.CalculateCriticalHitsData(location, _diceRoller, _damageTransferCalculator);
        if (criticalHitsData == null)
            return null;
        
        return criticalHitsData with { Location = location };
    }
}
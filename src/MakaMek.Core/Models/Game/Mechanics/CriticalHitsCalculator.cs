using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Utils;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Calculator for determining critical hits based on structure damage
/// </summary>
public class CriticalHitsCalculator : ICriticalHitsCalculator
{
    private readonly IDiceRoller _diceRoller;
    private readonly IDamageTransferCalculator _damageTransferCalculator;
    private readonly IMechFactory _mechFactory;

    /// <summary>
    /// Initializes a calculator that resolves critical hits against a private simulation copy of each unit.
    /// </summary>
    /// <param name="diceRoller">The dice roller used for critical-hit resolution.</param>
    /// <param name="damageTransferCalculator">The calculator used for component explosion damage.</param>
    /// <param name="mechFactory">The factory used to create isolated simulation copies.</param>
    public CriticalHitsCalculator(
        IDiceRoller diceRoller,
        IDamageTransferCalculator damageTransferCalculator,
        IMechFactory mechFactory)
    {
        _diceRoller = diceRoller;
        _damageTransferCalculator = damageTransferCalculator;
        _mechFactory = mechFactory;
    }
    
    /// <summary>
    /// Calculates critical hits without mutating the authoritative unit, including the newly destroyed
    /// locations and unit-destruction state they would cause.
    /// </summary>
    /// <param name="unit">The unit receiving the critical-hit effects.</param>
    /// <param name="hitLocationsData">The structure-damage locations that require critical-hit resolution.</param>
    /// <returns>A command containing the calculated results and destruction metadata, or <see langword="null"/> when no critical hits occurred.</returns>
    public CriticalHitsResolutionCommand? CalculateCriticalHits(IUnit unit, List<LocationDamageData> hitLocationsData)
    {
        if (!hitLocationsData.Any(damage => damage.StructureDamage > 0))
            return null;
        
        var destroyedPartsBefore = unit.Parts.Values
            .Where(part => part.IsDestroyed)
            .Select(part => part.Location)
            .ToHashSet();
        var wasDestroyedBefore = unit.IsDestroyed;
        
        var simulationUnit = unit.CloneUnit(_mechFactory);
        var allCriticalHitsData = ProcessAndApplyCriticalHitsDamage(simulationUnit, hitLocationsData);

        // If no critical hits occurred, no need to send a command
        if (allCriticalHitsData.Count == 0)
            return null;

        // The authoritative unit is deliberately left untouched, so the destruction metadata must be
        // read from the simulation copy the critical hits were actually applied to.
        var newlyDestroyedParts = simulationUnit.Parts.Values
            .Where(part => part.IsDestroyed && !destroyedPartsBefore.Contains(part.Location))
            .Select(part => part.Location)
            .ToList();

        return new CriticalHitsResolutionCommand
        {
            GameOriginId = Guid.Empty,
            TargetId = unit.Id,
            CriticalHits = allCriticalHitsData,
            DestroyedParts = newlyDestroyedParts.Count > 0 ? newlyDestroyedParts : null,
            UnitDestroyed = !wasDestroyedBefore && simulationUnit.IsDestroyed
        };
    }
    
    public HeatExplosionResolution CalculateCriticalHitsForHeatExplosion(
        Unit unit,
        Ammo explodingComponent) // only ammo can explode from heat
    {
        var explosionDamage = explodingComponent.GetExplosionDamage();
        if (explosionDamage <= 0) return HeatExplosionResolution.None; //no possible damage, no explosion
        
        var location = explodingComponent.FirstMountPartLocation;
        if (!location.HasValue) return HeatExplosionResolution.None;

        // Ensure the component has a resolvable slot
        var slots = explodingComponent.MountedAtFirstLocationSlots;
        if (slots.Length == 0)
            return HeatExplosionResolution.None;
        
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

        var forcedCriticalHit = new LocationCriticalHitsData(
            location.Value,
            [], // No roll for forced critical hit
            1, // One forced critical hit
            [componentHitData],
            false // Not blown off
        );
        
        var destroyedPartsBefore = unit.Parts.Values
            .Where(part => part.IsDestroyed)
            .Select(part => part.Location)
            .ToHashSet();
        var wasDestroyedBefore = unit.IsDestroyed;

        var simulationUnit = unit.CloneUnit(_mechFactory);
        simulationUnit.ApplyCriticalHits([forcedCriticalHit]);
        var explosionConsequences =
            ProcessAndApplyCriticalHitsDamage(simulationUnit, explosionDamageData.ToList());

        var criticalHits = new List<LocationCriticalHitsData> { forcedCriticalHit }
            .Concat(explosionConsequences).ToList();

        // Read the destruction off the simulation copy: the authoritative unit is only damaged later,
        // when the resulting command is handled.
        var newlyDestroyedParts = simulationUnit.Parts.Values
            .Where(part => part.IsDestroyed && !destroyedPartsBefore.Contains(part.Location))
            .Select(part => part.Location)
            .ToList();

        return new HeatExplosionResolution(
            criticalHits,
            newlyDestroyedParts.Count > 0 ? newlyDestroyedParts : null,
            !wasDestroyedBefore && simulationUnit.IsDestroyed);
    }
    
    private List<LocationCriticalHitsData> ProcessAndApplyCriticalHitsDamage(IUnit unit, List<LocationDamageData> hitLocationsData)
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
                // Apply intermediate results only to the simulation unit so chained explosions
                // can resolve against the updated state without mutating the authoritative unit.
                unit.ApplyCriticalHits([criticalHitsData]);
                allCriticalHitsData.Add(criticalHitsData);
            }
            var explosions = criticalHitsData?
                .HitComponents?.SelectMany(c => c.ExplosionDamageDistribution) ?? [];
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
        IUnit unit,
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

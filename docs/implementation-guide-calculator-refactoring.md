# Implementation Guide: Calculator Refactoring Approach

## Overview

This guide provides exact implementation details for fixing the cluster hit damage resolution bug using the Calculator Refactoring approach (passing damage history).

---

## Implementation Summary

### Core Changes

1. Add optional `previousDamageGroups` parameter to `CalculateStructureDamage`
2. Implement accumulated damage calculation in `DamageTransferCalculator`
3. Pass previous damage groups when calculating cluster weapon hits
4. Add comprehensive tests

### Files to Modify

1. `src/MakaMek.Core/Models/Game/Mechanics/IDamageTransferCalculator.cs`
2. `src/MakaMek.Core/Models/Game/Mechanics/DamageTransferCalculator.cs`
3. `src/MakaMek.Core/Models/Game/Phases/WeaponAttackResolutionPhase.cs`
4. `tests/MakaMek.Core.Tests/Models/Game/Mechanics/DamageTransferCalculatorTests.cs`

---

## Step 1: Update Interface

**File:** `src/MakaMek.Core/Models/Game/Mechanics/IDamageTransferCalculator.cs`

**Change:** Add optional parameter to `CalculateStructureDamage` method

```csharp
/// <summary>
/// Service for calculating structure damage distribution without applying it to units
/// </summary>
public interface IDamageTransferCalculator
{
    /// <summary>
    /// Calculates how damage would be distributed across locations without applying it
    /// </summary>
    /// <param name="unit">The unit receiving damage</param>
    /// <param name="initialLocation">The initial hit location</param>
    /// <param name="totalDamage">The total damage to distribute</param>
    /// <param name="hitDirection">The direction of the hit for armor calculations</param>
    /// <param name="previousDamageGroups">Previously calculated damage groups from the same attack (for cluster weapons)</param>
    /// <returns>List of locations to their armor and structure damage</returns>
    IReadOnlyList<LocationDamageData> CalculateStructureDamage(
        Unit unit,
        PartLocation initialLocation,
        int totalDamage,
        HitDirection hitDirection,
        IReadOnlyList<LocationHitData>? previousDamageGroups = null);

    /// <summary>
    /// Calculates how explosion damage would be distributed across locations without applying it.
    /// Explosion damage bypasses armor entirely and applies directly to the structure.
    /// </summary>
    /// <param name="unit">The unit receiving damage</param>
    /// <param name="initialLocation">The initial hit location</param>
    /// <param name="totalDamage">The total damage to distribute</param>
    /// <returns>List of locations to their structure damage (armor damage will always be 0)</returns>
    IReadOnlyList<LocationDamageData> CalculateExplosionDamage(
        Unit unit,
        PartLocation initialLocation,
        int totalDamage);
}
```

**Lines to modify:** 19-23

---

## Step 2: Implement Accumulated Damage Calculation

**File:** `src/MakaMek.Core/Models/Game/Mechanics/DamageTransferCalculator.cs`

### Change 2.1: Update public method signature

**Location:** Lines 12-19

```csharp
public IReadOnlyList<LocationDamageData> CalculateStructureDamage(
    Unit unit,
    PartLocation initialLocation,
    int totalDamage,
    HitDirection hitDirection,
    IReadOnlyList<LocationHitData>? previousDamageGroups = null)
{
    return CalculateDamageDistribution(unit, initialLocation, totalDamage, hitDirection, false, previousDamageGroups);
}
```

### Change 2.2: Update private method signature

**Location:** Lines 29-31

```csharp
private List<LocationDamageData> CalculateDamageDistribution(
    Unit unit, 
    PartLocation initialLocation, 
    int totalDamage,
    HitDirection hitDirection, 
    bool isExplosion = false,
    IReadOnlyList<LocationHitData>? previousDamageGroups = null)
{
    // Calculate accumulated damage from previous groups
    var accumulatedDamage = CalculateAccumulatedDamage(previousDamageGroups);
    
    var damageDistribution = new List<LocationDamageData>();
    var remainingDamage = totalDamage;
    PartLocation? currentLocation = initialLocation;

    while (remainingDamage > 0 && currentLocation.HasValue)
    {
        if (!unit.Parts.TryGetValue(currentLocation.Value, out var part))
            break;

        // Check if the location is already destroyed - if so, skip to transfer location
        if (part.CurrentStructure <= 0)
        {
            currentLocation = unit.GetTransferLocation(currentLocation.Value);
            continue;
        }

        var locationDamage = isExplosion
            ? CalculateExplosionLocationDamage(part, remainingDamage, accumulatedDamage)
            : CalculateLocationDamage(part, remainingDamage, hitDirection, accumulatedDamage);
        damageDistribution.Add(locationDamage);

        // Calculate remaining damage after this location
        remainingDamage -= (locationDamage.ArmorDamage + locationDamage.StructureDamage);

        // If a location is destroyed and there's remaining damage, transfer to the next location
        if (locationDamage.IsLocationDestroyed && remainingDamage > 0)
        {
            currentLocation = unit.GetTransferLocation(currentLocation.Value);
        }
        else
        {
            break;
        }
    }

    return damageDistribution;
}
```

### Change 2.3: Add accumulated damage calculation helper

**Location:** Add new method after `CalculateDamageDistribution` (around line 68)

```csharp
/// <summary>
/// Calculates accumulated damage from previous damage groups in the same attack
/// </summary>
/// <param name="previousDamageGroups">Previously calculated damage groups</param>
/// <returns>Dictionary mapping location to accumulated front armor, rear armor, and structure damage</returns>
private Dictionary<PartLocation, (int frontArmorDamage, int rearArmorDamage, int structureDamage)> 
    CalculateAccumulatedDamage(IReadOnlyList<LocationHitData>? previousDamageGroups)
{
    var accumulated = new Dictionary<PartLocation, (int, int, int)>();
    
    if (previousDamageGroups == null || previousDamageGroups.Count == 0)
        return accumulated;
    
    foreach (var group in previousDamageGroups)
    {
        foreach (var damage in group.Damage)
        {
            if (!accumulated.ContainsKey(damage.Location))
                accumulated[damage.Location] = (0, 0, 0);
            
            var current = accumulated[damage.Location];
            
            // Track front and rear armor separately
            if (damage.IsRearArmor)
            {
                accumulated[damage.Location] = (
                    current.frontArmorDamage,
                    current.rearArmorDamage + damage.ArmorDamage,
                    current.structureDamage + damage.StructureDamage
                );
            }
            else
            {
                accumulated[damage.Location] = (
                    current.frontArmorDamage + damage.ArmorDamage,
                    current.rearArmorDamage,
                    current.structureDamage + damage.StructureDamage
                );
            }
        }
    }
    
    return accumulated;
}
```

### Change 2.4: Update CalculateLocationDamage to use accumulated damage

**Location:** Lines 70-100

```csharp
private LocationDamageData CalculateLocationDamage(
    UnitPart part, 
    int incomingDamage, 
    HitDirection hitDirection,
    Dictionary<PartLocation, (int frontArmorDamage, int rearArmorDamage, int structureDamage)> accumulatedDamage)
{
    var armorDamage = 0;
    var structureDamage = 0;
    var remainingDamage = incomingDamage;

    // Get accumulated damage for this location
    var accumulated = accumulatedDamage.GetValueOrDefault(part.Location, (0, 0, 0));

    // Calculate armor damage first
    var (availableArmor, isRearArmor) = GetAvailableArmor(part, hitDirection);
    
    // Subtract accumulated armor damage from available armor
    var accumulatedArmorDamage = isRearArmor ? accumulated.rearArmorDamage : accumulated.frontArmorDamage;
    availableArmor = Math.Max(0, availableArmor - accumulatedArmorDamage);
    
    if (availableArmor > 0)
    {
        armorDamage = Math.Min(remainingDamage, availableArmor);
        remainingDamage -= armorDamage;
    }

    // Calculate structure damage if armor is depleted
    if (remainingDamage > 0)
    {
        var availableStructure = part.CurrentStructure;
        
        // Subtract accumulated structure damage from available structure
        availableStructure = Math.Max(0, availableStructure - accumulated.structureDamage);
        
        structureDamage = Math.Min(remainingDamage, availableStructure);
    }

    // Location is destroyed if total structure damage (accumulated + new) >= current structure
    var totalStructureDamage = accumulated.structureDamage + structureDamage;
    var locationDestroyed = totalStructureDamage >= part.CurrentStructure;

    return new LocationDamageData(
        part.Location,
        armorDamage,
        structureDamage,
        locationDestroyed,
        isRearArmor
    );
}
```

### Change 2.5: Update CalculateExplosionLocationDamage to use accumulated damage

**Location:** Lines 102-115

```csharp
private LocationDamageData CalculateExplosionLocationDamage(
    UnitPart part, 
    int incomingDamage,
    Dictionary<PartLocation, (int frontArmorDamage, int rearArmorDamage, int structureDamage)> accumulatedDamage)
{
    // Get accumulated damage for this location
    var accumulated = accumulatedDamage.GetValueOrDefault(part.Location, (0, 0, 0));
    
    // Explosion damage bypasses armor entirely
    var availableStructure = part.CurrentStructure;
    
    // Subtract accumulated structure damage from available structure
    availableStructure = Math.Max(0, availableStructure - accumulated.structureDamage);
    
    var structureDamage = Math.Min(incomingDamage, availableStructure);
    
    // Location is destroyed if total structure damage (accumulated + new) >= current structure
    var totalStructureDamage = accumulated.structureDamage + structureDamage;
    var locationDestroyed = totalStructureDamage >= part.CurrentStructure;

    return new LocationDamageData(
        part.Location,
        0,
        structureDamage,
        locationDestroyed 
    );
}
```

---

## Step 3: Update WeaponAttackResolutionPhase

**File:** `src/MakaMek.Core/Models/Game/Phases/WeaponAttackResolutionPhase.cs`

### Change 3.1: Modify ResolveClusterWeaponHit to pass previous groups

**Location:** Lines 187-244

```csharp
private AttackHitLocationsData ResolveClusterWeaponHit(Weapon weapon, Unit target, HitDirection attackDirection, WeaponTargetData weaponTargetData)
{
    // Roll for cluster hits
    var clusterRoll = Game.DiceRoller.Roll2D6();
    var clusterRollTotal = clusterRoll.Sum(d => d.Result);
    
    // Determine how many missiles hit using the cluster hit table
    var missilesHit = Game.RulesProvider.GetClusterHits(clusterRollTotal, weapon.WeaponSize);
    
    // Calculate damage per missile
    var damagePerMissile = weapon.Damage / weapon.WeaponSize;
    
    // Calculate how many complete clusters hit and if there's a partial cluster
    var completeClusterHits = missilesHit / weapon.ClusterSize;
    var remainingMissiles = missilesHit % weapon.ClusterSize;
    
    var hitLocations = new List<LocationHitData>();
    var totalDamage = 0;
    
    // For each complete cluster that hit
    for (var i = 0; i < completeClusterHits; i++)
    {
        // Calculate damage for this cluster
        var clusterDamage = weapon.ClusterSize * damagePerMissile;
        
        // Determine the hit location for this cluster
        // Pass previously calculated groups so this group sees accumulated damage
        var hitLocationData = DetermineHitLocation(
            attackDirection,
            clusterDamage, 
            target, 
            weapon, 
            weaponTargetData,
            hitLocations);  // NEW: Pass previous groups
        
        // Add to hit locations and update total damage
        hitLocations.Add(hitLocationData);
        totalDamage += clusterDamage;
    }
    
    // If there are remaining missiles (partial cluster)
    if (remainingMissiles > 0)
    {
        // Calculate damage for the partial cluster
        var partialClusterDamage = remainingMissiles * damagePerMissile;
        
        // Determine the hit location for the partial cluster
        // Pass previously calculated groups so this group sees accumulated damage
        var hitLocationData = DetermineHitLocation(
            attackDirection,
            partialClusterDamage,
            target, 
            weapon,
            weaponTargetData,
            hitLocations);  // NEW: Pass previous groups
        
        // Add to hit locations and update total damage
        hitLocations.Add(hitLocationData);
        totalDamage += partialClusterDamage;
    }

    return new AttackHitLocationsData(hitLocations, totalDamage, clusterRoll, missilesHit);
}
```

### Change 3.2: Update DetermineHitLocation signature and implementation

**Location:** Lines 255-318

```csharp
/// <summary>
/// Determines the hit location for an attack
/// </summary>
/// <param name="attackDirection">The direction of the attack</param>
/// <param name="damage">The damage to be applied to this location</param>
/// <param name="target">The target unit</param>
/// <param name="weapon">The firing weapon</param>
/// <param name="weaponTargetData">Weapon's target data</param>
/// <param name="previousDamageGroups">Previously calculated damage groups from the same attack</param>
/// <returns>Hit location data with location, damage and dice roll</returns>
private LocationHitData DetermineHitLocation(
    HitDirection attackDirection,
    int damage,
    Unit target,
    Weapon weapon,
    WeaponTargetData weaponTargetData,
    IReadOnlyList<LocationHitData>? previousDamageGroups = null)  // NEW parameter
{
    // ... existing aimed shot logic (lines 262-274) ...
    
    int[] locationRoll = [];
    // If the aimed shot location is null, determine the hit location normally
    var hitLocation = aimedShotLocation ?? GetHitLocation(out locationRoll);
    
    // Store the initial location in case we need to transfer
    var initialLocation = hitLocation;
    
    // Check if the location is already destroyed and transfer if needed
    while (target.Parts.TryGetValue(hitLocation, out var part) && part.IsDestroyed)
    {
        var nextLocation = part.GetNextTransferLocation();
        if (nextLocation == null || nextLocation == hitLocation)
            break;

        hitLocation = nextLocation.Value;
    }
    
    // Use DamageTransferCalculator to calculate damage distribution
    // Pass previous damage groups so calculator can account for accumulated damage
    var damageData = Game.DamageTransferCalculator.CalculateStructureDamage(
        target, 
        hitLocation, 
        damage, 
        attackDirection,
        previousDamageGroups);  // NEW: Pass previous groups

    return new LocationHitData(
        damageData,
        aimedShotRollResult,
        locationRoll,
        initialLocation);

    // ... rest of method unchanged ...
}
```

---

## Summary of Changes

### Interface Changes
- ✅ `IDamageTransferCalculator.CalculateStructureDamage`: Added optional `previousDamageGroups` parameter

### Implementation Changes
- ✅ `DamageTransferCalculator.CalculateStructureDamage`: Pass parameter to private method
- ✅ `DamageTransferCalculator.CalculateDamageDistribution`: Accept and use previous groups
- ✅ `DamageTransferCalculator.CalculateAccumulatedDamage`: NEW method to calculate accumulated damage
- ✅ `DamageTransferCalculator.CalculateLocationDamage`: Account for accumulated damage
- ✅ `DamageTransferCalculator.CalculateExplosionLocationDamage`: Account for accumulated damage
- ✅ `WeaponAttackResolutionPhase.ResolveClusterWeaponHit`: Pass previous groups to DetermineHitLocation
- ✅ `WeaponAttackResolutionPhase.DetermineHitLocation`: Accept and pass previous groups to calculator

### No Changes Required
- ❌ `AttackResolutionData` - Remains unchanged
- ❌ `FinalizeAttackResolution` - Remains unchanged
- ❌ `Unit.ApplyDamage` - Remains unchanged
- ❌ `FallingDamageCalculator` - Remains unchanged (doesn't pass previous groups)

---

## Next Document

See `implementation-guide-testing.md` for comprehensive test implementation details.


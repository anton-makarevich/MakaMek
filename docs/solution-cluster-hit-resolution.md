# Solution: Cluster Hit Damage Resolution Fix

## Overview

This document outlines the implementation approach for fixing the cluster hit damage resolution bug where multiple damage groups hitting the same location calculate damage based on the target's initial state rather than the updated state after each group.

## Solution Approach

### Core Strategy: Incremental Damage Application

Apply damage to the target after each damage group is calculated, ensuring subsequent groups see the updated armor/structure values.

### Key Principles

1. **Sequential Processing**: Process each damage group in order, applying damage before calculating the next group
2. **Preserve Command Structure**: Maintain single `WeaponAttackResolutionCommand` per weapon attack for client synchronization
3. **Defer Side Effects**: Continue to defer PSRs and critical hits until all damage groups are processed
4. **Minimal Changes**: Limit modifications to `ResolveClusterWeaponHit` and related methods

## Implementation Details

### Modified Method: `ResolveClusterWeaponHit`

**Current Flow:**
```
1. Roll for cluster hits
2. Calculate all damage groups (each sees initial state)
3. Return all LocationHitData
4. Later: Apply all damage at once in FinalizeAttackResolution
```

**New Flow:**
```
1. Roll for cluster hits
2. For each damage group:
   a. Calculate damage distribution (sees current state)
   b. Apply damage to target immediately
   c. Store LocationHitData for command/rendering
3. Return all LocationHitData (for command publishing)
4. Later: Calculate PSRs and critical hits in FinalizeAttackResolution
```

### Code Changes

#### Change 1: Modify `ResolveClusterWeaponHit`

**Location:** `WeaponAttackResolutionPhase.cs`, lines 187-244

**Modifications:**
1. After calculating each damage group's `hitLocationData`, apply the damage immediately
2. Continue collecting all `LocationHitData` for the command
3. Return the complete list for rendering/logging

**Pseudocode:**
```csharp
private AttackHitLocationsData ResolveClusterWeaponHit(...)
{
    // ... existing cluster roll logic ...
    
    var hitLocations = new List<LocationHitData>();
    var totalDamage = 0;
    
    // For each complete cluster that hit
    for (var i = 0; i < completeClusterHits; i++)
    {
        var clusterDamage = weapon.ClusterSize * damagePerMissile;
        var hitLocationData = DetermineHitLocation(...);
        
        // NEW: Apply damage immediately so next group sees updated state
        ApplyDamageGroup(target, hitLocationData, attackDirection);
        
        hitLocations.Add(hitLocationData);
        totalDamage += clusterDamage;
    }
    
    // Handle partial cluster similarly
    if (remainingMissiles > 0)
    {
        var partialClusterDamage = remainingMissiles * damagePerMissile;
        var hitLocationData = DetermineHitLocation(...);
        
        // NEW: Apply damage immediately
        ApplyDamageGroup(target, hitLocationData, attackDirection);
        
        hitLocations.Add(hitLocationData);
        totalDamage += partialClusterDamage;
    }
    
    return new AttackHitLocationsData(hitLocations, totalDamage, clusterRoll, missilesHit);
}
```

#### Change 2: Add Helper Method `ApplyDamageGroup`

**Location:** `WeaponAttackResolutionPhase.cs` (new method)

**Purpose:** Apply damage from a single damage group without triggering side effects

**Implementation:**
```csharp
/// <summary>
/// Applies damage from a single damage group to the target.
/// Does NOT trigger critical hits or PSRs - those are handled later in FinalizeAttackResolution.
/// </summary>
private void ApplyDamageGroup(Unit target, LocationHitData hitLocationData, HitDirection attackDirection)
{
    // Apply the damage using the existing Unit.ApplyDamage method
    // This updates armor and structure values
    target.ApplyDamage([hitLocationData], attackDirection);
}
```

#### Change 3: Modify `FinalizeAttackResolution`

**Location:** `WeaponAttackResolutionPhase.cs`, lines 352-441

**Modifications:**
1. Skip the damage application step for cluster weapons (already applied)
2. Continue to handle critical hits and PSRs as before
3. Ensure destroyed parts tracking still works correctly

**Pseudocode:**
```csharp
private void FinalizeAttackResolution(...)
{
    // Track destroyed parts before damage
    var destroyedPartsBefore = target.Parts.Values
        .Where(p => p.IsDestroyed).Select(p => p.Location).ToList();
    var wasDestroyedBefore = target.IsDestroyed;

    // Apply damage to the target
    // NEW: Skip for cluster weapons (damage already applied incrementally)
    if (resolution is { IsHit: true, HitLocationsData.HitLocations: not null })
    {
        if (weapon.WeaponSize <= 1) // Non-cluster weapon
        {
            target.ApplyDamage(resolution.HitLocationsData.HitLocations, resolution.AttackDirection);
        }
        // For cluster weapons, damage was already applied in ResolveClusterWeaponHit
    }

    // ... rest of the method unchanged (critical hits, PSRs, etc.) ...
}
```

### Alternative Approach: Flag-Based

Instead of checking `weapon.WeaponSize`, we could add a flag to `AttackResolutionData`:

```csharp
public record AttackResolutionData(
    // ... existing fields ...
    bool DamageAlreadyApplied = false  // NEW field
);
```

Then in `FinalizeAttackResolution`:
```csharp
if (resolution is { IsHit: true, HitLocationsData.HitLocations: not null, DamageAlreadyApplied: false })
{
    target.ApplyDamage(resolution.HitLocationsData.HitLocations, resolution.AttackDirection);
}
```

**Recommendation:** Use the flag-based approach for clarity and future extensibility.

## Testing Strategy

### Unit Tests to Add

1. **Test: Multiple groups hit same location with limited armor**
   ```
   Scenario: LRM20, 9 missiles hit, both groups hit CT with 2 armor
   Expected: First group depletes armor, second group only damages structure
   Verify: Total armor damage = 2 (not 4)
   ```

2. **Test: Multiple groups hit same location causing destruction**
   ```
   Scenario: LRM15, 12 missiles hit, all groups hit RA with low armor/structure
   Expected: First group depletes armor, second group destroys location, third group transfers
   Verify: Damage transfer occurs correctly
   ```

3. **Test: Multiple groups hit different locations**
   ```
   Scenario: LRM10, 6 missiles hit, groups hit different locations
   Expected: Each location's damage calculated independently
   Verify: No interference between groups
   ```

4. **Test: Single group (non-cluster weapon)**
   ```
   Scenario: AC/5 hits CT
   Expected: Behavior unchanged from current implementation
   Verify: Regression test
   ```

### Integration Tests

1. **Test: Full attack sequence with cluster weapon**
   - Verify command publishing includes all damage groups
   - Verify critical hits calculated correctly
   - Verify PSRs triggered at correct time

2. **Test: Client synchronization**
   - Verify clients can reconstruct damage sequence from command
   - Verify UI displays correct damage values

## Edge Cases to Consider

### 1. Damage Transfer Between Groups

**Scenario:** First group destroys a location, second group's damage transfers

**Example:**
- LRM10, 6 missiles hit
- Group 1 (5 missiles) hits RA, destroys it
- Group 2 (1 missile) should transfer to RT

**Handling:**
- `DetermineHitLocation` already handles destroyed locations (lines 284-291)
- After Group 1 is applied, RA is destroyed
- Group 2's calculation will see RA as destroyed and transfer correctly

**Verification:** Add specific test for this scenario

### 2. Unit Destruction Mid-Attack

**Scenario:** First group destroys the unit (head blown off, CT destroyed, etc.)

**Example:**
- LRM5, 3 missiles hit
- Group 1 destroys head
- Group 2 should still be calculated/applied (for completeness)

**Handling:**
- Continue processing all groups even if unit is destroyed
- Existing code already handles this (line 113: "Take all units not just alive")
- Damage application to destroyed units is safe (no-op)

**Verification:** Add test to ensure no exceptions thrown

### 3. Critical Hits Timing

**Scenario:** Ensure critical hits are calculated after all damage groups

**Current Behavior:**
- Critical hits calculated in `FinalizeAttackResolution` (lines 404-421)
- Uses all `LocationDamageData` from all groups

**New Behavior:**
- Same - critical hits still calculated after all groups processed
- All structure damage from all groups is included

**Verification:** Existing tests should cover this

### 4. PSR Timing

**Scenario:** Ensure PSRs are calculated at end of phase, not per group

**Current Behavior:**
- PSRs calculated in `CalculateEndOfPhasePsrs` (lines 467-513)
- Accumulated damage tracked per phase

**New Behavior:**
- Same - PSRs still calculated at end of phase
- Damage accumulation unchanged

**Verification:** Existing tests should cover this

## Rollout Plan

### Phase 1: Implementation
1. Add `DamageAlreadyApplied` flag to `AttackResolutionData`
2. Modify `ResolveClusterWeaponHit` to apply damage incrementally
3. Add `ApplyDamageGroup` helper method
4. Modify `FinalizeAttackResolution` to skip damage application when flag is set

### Phase 2: Testing
1. Add unit tests for multiple groups hitting same location
2. Add tests for damage transfer scenarios
3. Add tests for edge cases (unit destruction, etc.)
4. Run full regression test suite

### Phase 3: Validation
1. Manual testing with various cluster weapons (LRM5, LRM10, LRM15, LRM20, SRM2, SRM4, SRM6)
2. Verify game log output is correct
3. Verify client UI displays correct values
4. Performance testing (ensure no degradation)

## Risks and Mitigations

### Risk 1: Breaking Client Synchronization

**Description:** Clients might not correctly reconstruct damage sequence

**Mitigation:**
- Keep command structure unchanged
- All damage groups still included in single command
- Clients apply damage from command, not from incremental updates

### Risk 2: Side Effects Triggered Too Early

**Description:** Critical hits or PSRs might be triggered per group instead of per attack

**Mitigation:**
- Use `ApplyDamageGroup` helper that only updates armor/structure
- Keep critical hit and PSR logic in `FinalizeAttackResolution`
- Add tests to verify timing

### Risk 3: Performance Impact

**Description:** Multiple damage applications might be slower

**Mitigation:**
- Damage application is already fast (simple arithmetic)
- Number of groups is small (typically 1-4)
- Performance impact should be negligible
- Add performance tests if concerned

## Success Criteria

1. **Correctness:** Multiple damage groups hitting the same location calculate damage based on updated state
2. **No Regressions:** All existing tests pass
3. **Command Structure:** Single command per weapon attack maintained
4. **Side Effects:** Critical hits and PSRs still triggered at correct time
5. **Client Sync:** Clients correctly display damage values

## Future Enhancements

1. **Detailed Damage Logging:** Show per-group damage in game log
2. **Animation Support:** Enable per-group damage animations in UI
3. **Damage Replay:** Allow clients to replay damage sequence for debugging


# Bug Investigation Summary: Cluster Hit Damage Resolution

**Date:** 2025-10-07  
**Issue:** Cluster weapon damage groups calculate damage based on initial target state instead of updated state  
**Severity:** High - Causes incorrect damage calculations that violate game rules  
**Status:** Root cause identified, solution proposed

---

## Executive Summary

The weapon attack system has a critical bug in cluster hit resolution. When multiple damage groups from a single cluster weapon (like LRM20) hit the same location, each group calculates its damage distribution based on the target's **initial** armor/structure values, rather than the **updated** values after previous groups have been applied.

This results in impossible damage values (e.g., dealing 4 armor damage to a location with only 2 armor points) and violates BattleTech rules which specify that damage should be applied sequentially.

---

## Problem Statement

### Specific Test Case

**Setup:**
- Weapon: LRM20 (20 missiles, 1 damage each, ClusterSize = 5)
- Cluster roll: 4 → 9 missiles hit
- Damage groups: Group 1 (5 missiles = 5 damage), Group 2 (4 missiles = 4 damage)
- Hit location: Both groups roll 7 → Center Torso
- Target's CT: 2 armor points, 8 internal structure points

**Current (Incorrect) Result:**
- Group 1: 2 armor damage, 3 structure damage
- Group 2: 2 armor damage, 2 structure damage
- **Total: 4 armor damage, 5 structure damage** ❌
- **Problem:** CT only has 2 armor points!

**Expected (Correct) Result:**
- Group 1: 2 armor damage, 3 structure damage (depletes all armor)
- Group 2: 0 armor damage, 4 structure damage (armor already depleted)
- **Total: 2 armor damage, 7 structure damage** ✅

---

## Root Cause Analysis

### Location
`src/MakaMek.Core/Models/Game/Phases/WeaponAttackResolutionPhase.cs`

### The Bug

**In `ResolveClusterWeaponHit` (lines 187-244):**

```csharp
// For each complete cluster that hit
for (var i = 0; i < completeClusterHits; i++)
{
    var clusterDamage = weapon.ClusterSize * damagePerMissile;
    
    // This calls DamageTransferCalculator which reads current armor/structure
    var hitLocationData = DetermineHitLocation(attackDirection,
        clusterDamage, 
        target, 
        weapon, 
        weaponTargetData);
    
    // Damage is NOT applied here - just stored
    hitLocations.Add(hitLocationData);
    totalDamage += clusterDamage;
}
```

**The problem:** Each call to `DetermineHitLocation` → `DamageTransferCalculator.CalculateStructureDamage` reads the target's current armor/structure values. Since damage is not applied until later, all groups see the same initial state.

**In `FinalizeAttackResolution` (line 363):**

```csharp
// Apply damage to the target
if (resolution is { IsHit: true, HitLocationsData.HitLocations: not null })
{
    target.ApplyDamage(resolution.HitLocationsData.HitLocations, resolution.AttackDirection);
}
```

**The problem:** Damage is applied all at once, after all groups have been calculated. This is too late.

### Why This Happens

1. **Separation of Calculation and Application:** The system separates damage calculation from damage application for good architectural reasons (command pattern, client synchronization)
2. **Batch Processing:** All damage groups are calculated in a batch, then applied in a batch
3. **State Reading:** `DamageTransferCalculator` reads the current state of the target unit
4. **Timing Issue:** The current state doesn't change between group calculations

---

## Proposed Solution

### Approach: Incremental Damage Application

**Key Idea:** Apply damage after each group is calculated, before calculating the next group.

### Changes Required

1. **Modify `ResolveClusterWeaponHit`:**
   - After calculating each group's damage, apply it immediately
   - Continue collecting all `LocationHitData` for command publishing
   - Return complete list for rendering/logging

2. **Add `ApplyDamageGroup` helper method:**
   - Applies damage from a single group
   - Updates armor/structure values
   - Does NOT trigger critical hits or PSRs (deferred to later)

3. **Modify `FinalizeAttackResolution`:**
   - Skip damage application for cluster weapons (already applied)
   - Continue to handle critical hits and PSRs as before

4. **Add flag to `AttackResolutionData`:**
   - `DamageAlreadyApplied` flag to indicate incremental application
   - Used by `FinalizeAttackResolution` to skip redundant application

### Benefits

✅ **Minimal Changes:** Only affects cluster weapon resolution  
✅ **Preserves Architecture:** Maintains command pattern and client synchronization  
✅ **Correct Behavior:** Matches BattleTech rules for sequential damage  
✅ **No Side Effects:** Critical hits and PSRs still calculated at correct time  
✅ **Backward Compatible:** Non-cluster weapons unchanged  

---

## Impact Assessment

### Affected Systems

1. **Cluster Weapons:**
   - LRM5, LRM10, LRM15, LRM20
   - SRM2, SRM4, SRM6
   - Any weapon with `WeaponSize > 1`

2. **Damage Calculation:**
   - `DamageTransferCalculator` (no changes needed)
   - `WeaponAttackResolutionPhase` (changes needed)

3. **Command Publishing:**
   - `WeaponAttackResolutionCommand` (no changes needed)
   - Command structure remains the same

4. **Client Synchronization:**
   - No changes needed
   - Clients receive same command structure

### Not Affected

- Non-cluster weapons (AC/5, PPC, Laser, etc.)
- Critical hit calculation
- PSR calculation
- Damage transfer between locations
- Client rendering

---

## Testing Requirements

### Unit Tests

1. **Multiple groups, same location, limited armor**
   - Verify first group depletes armor
   - Verify second group only damages structure
   - Verify total armor damage ≤ available armor

2. **Multiple groups, same location, location destroyed**
   - Verify damage transfer occurs correctly
   - Verify destroyed location tracking

3. **Multiple groups, different locations**
   - Verify no interference between groups
   - Verify each location calculated independently

4. **Regression: Non-cluster weapons**
   - Verify behavior unchanged
   - Verify all existing tests pass

### Integration Tests

1. **Full attack sequence**
   - Verify command publishing
   - Verify critical hits
   - Verify PSRs

2. **Client synchronization**
   - Verify clients receive correct data
   - Verify UI displays correct values

---

## Implementation Plan

### Phase 1: Code Changes (Estimated: 2-3 hours)
1. Add `DamageAlreadyApplied` flag to `AttackResolutionData`
2. Modify `ResolveClusterWeaponHit` to apply damage incrementally
3. Add `ApplyDamageGroup` helper method
4. Modify `FinalizeAttackResolution` to skip damage when flag is set

### Phase 2: Testing (Estimated: 3-4 hours)
1. Write unit tests for new behavior
2. Run full regression test suite
3. Manual testing with various cluster weapons
4. Verify game log output

### Phase 3: Validation (Estimated: 1-2 hours)
1. Code review
2. Performance testing
3. Client synchronization verification
4. Documentation updates

**Total Estimated Time:** 6-9 hours

---

## Risks and Mitigations

### Risk 1: Breaking Client Synchronization
**Likelihood:** Low  
**Impact:** High  
**Mitigation:** Keep command structure unchanged, all damage groups in single command

### Risk 2: Side Effects Triggered Too Early
**Likelihood:** Medium  
**Impact:** High  
**Mitigation:** Use dedicated helper method that only updates armor/structure

### Risk 3: Performance Impact
**Likelihood:** Low  
**Impact:** Low  
**Mitigation:** Damage application is fast, number of groups is small (1-4)

### Risk 4: Regression in Non-Cluster Weapons
**Likelihood:** Low  
**Impact:** Medium  
**Mitigation:** Flag-based approach ensures non-cluster weapons unchanged

---

## Related Files

### Files to Modify
- `src/MakaMek.Core/Models/Game/Phases/WeaponAttackResolutionPhase.cs`
- `src/MakaMek.Core/Data/Game/AttackResolutionData.cs`

### Files to Review (No Changes)
- `src/MakaMek.Core/Models/Game/Mechanics/DamageTransferCalculator.cs`
- `src/MakaMek.Core/Models/Units/Unit.cs`
- `src/MakaMek.Core/Data/Game/Commands/Server/WeaponAttackResolutionCommand.cs`

### Test Files to Add/Modify
- `tests/MakaMek.Core.Tests/Models/Game/Phases/WeaponAttackResolutionPhaseTests.cs`
- New test file: `tests/MakaMek.Core.Tests/Models/Game/Phases/ClusterHitResolutionTests.cs`

---

## Recommendations

1. **Implement Solution 1 (Incremental Application):** This is the cleanest approach with minimal architectural changes

2. **Add Comprehensive Tests:** Focus on edge cases (armor depletion, location destruction, damage transfer)

3. **Update Documentation:** Document the sequential damage application behavior

4. **Consider Future Enhancements:** 
   - Per-group damage logging in game log
   - Per-group damage animations in UI
   - Damage replay for debugging

---

## References

- **Bug Analysis:** `docs/bug-analysis-cluster-hit-resolution.md`
- **Solution Details:** `docs/solution-cluster-hit-resolution.md`
- **BattleTech Rules:** Total Warfare, Cluster Hits Table (p. 108)
- **Related Code:** `WeaponAttackResolutionPhase.cs`, `DamageTransferCalculator.cs`

---

## Conclusion

The cluster hit damage resolution bug is a critical issue that causes incorrect damage calculations. The root cause is well-understood: damage groups are calculated in batch based on initial state, rather than applied sequentially.

The proposed solution (incremental damage application) is straightforward, requires minimal changes, and preserves the existing architecture. With proper testing, this fix can be implemented safely without breaking existing functionality.

**Recommendation:** Proceed with implementation using the incremental damage application approach.


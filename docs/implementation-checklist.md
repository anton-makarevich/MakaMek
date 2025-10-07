# Implementation Checklist: Cluster Hit Damage Resolution Fix

## Overview
This checklist provides step-by-step instructions for implementing the cluster hit damage resolution fix.

---

## Pre-Implementation

- [ ] Review bug analysis document: `docs/bug-analysis-cluster-hit-resolution.md`
- [ ] Review solution document: `docs/solution-cluster-hit-resolution.md`
- [ ] Review investigation summary: `docs/bug-investigation-summary.md`
- [ ] Ensure all existing tests pass
- [ ] Create feature branch: `fix/cluster-hit-resolution` or similar

---

## Code Changes

### Step 1: Add Flag to AttackResolutionData

**File:** `src/MakaMek.Core/Data/Game/AttackResolutionData.cs`

- [ ] Add `DamageAlreadyApplied` property to record
- [ ] Set default value to `false`
- [ ] Update constructor/with expressions if needed

**Expected Change:**
```csharp
public record AttackResolutionData(
    int ToHitNumber,
    DiceResult[] AttackRoll,
    bool IsHit,
    HitDirection AttackDirection,
    AttackHitLocationsData? HitLocationsData,
    List<PartLocation>? DestroyedParts = null,
    bool UnitDestroyed = false,
    bool DamageAlreadyApplied = false  // NEW
);
```

### Step 2: Add ApplyDamageGroup Helper Method

**File:** `src/MakaMek.Core/Models/Game/Phases/WeaponAttackResolutionPhase.cs`

- [ ] Add private method `ApplyDamageGroup`
- [ ] Method should accept: `Unit target`, `LocationHitData hitLocationData`, `HitDirection attackDirection`
- [ ] Method should call `target.ApplyDamage([hitLocationData], attackDirection)`
- [ ] Add XML documentation comment

**Expected Addition:**
```csharp
/// <summary>
/// Applies damage from a single damage group to the target.
/// Does NOT trigger critical hits or PSRs - those are handled later in FinalizeAttackResolution.
/// </summary>
/// <param name="target">The target unit</param>
/// <param name="hitLocationData">The damage data for this group</param>
/// <param name="attackDirection">The direction of the attack</param>
private void ApplyDamageGroup(Unit target, LocationHitData hitLocationData, HitDirection attackDirection)
{
    target.ApplyDamage([hitLocationData], attackDirection);
}
```

### Step 3: Modify ResolveClusterWeaponHit Method

**File:** `src/MakaMek.Core/Models/Game/Phases/WeaponAttackResolutionPhase.cs`  
**Method:** `ResolveClusterWeaponHit` (lines 187-244)

- [ ] After calculating each complete cluster's `hitLocationData`, call `ApplyDamageGroup`
- [ ] After calculating partial cluster's `hitLocationData`, call `ApplyDamageGroup`
- [ ] Ensure `hitLocationData` is still added to `hitLocations` list (for command)
- [ ] Add comment explaining incremental damage application

**Expected Changes:**

In the complete clusters loop (around line 217):
```csharp
var hitLocationData = DetermineHitLocation(attackDirection,
    clusterDamage, 
    target, 
    weapon, 
    weaponTargetData);

// Apply damage immediately so next group sees updated state
ApplyDamageGroup(target, hitLocationData, attackDirection);

hitLocations.Add(hitLocationData);
totalDamage += clusterDamage;
```

In the partial cluster section (around line 236):
```csharp
var hitLocationData = DetermineHitLocation(
    attackDirection,
    partialClusterDamage,
    target, 
    weapon,
    weaponTargetData);

// Apply damage immediately so next group sees updated state
ApplyDamageGroup(target, hitLocationData, attackDirection);

hitLocations.Add(hitLocationData);
totalDamage += partialClusterDamage;
```

### Step 4: Modify ResolveAttack Method

**File:** `src/MakaMek.Core/Models/Game/Phases/WeaponAttackResolutionPhase.cs`  
**Method:** `ResolveAttack` (lines 130-185)

- [ ] Set `DamageAlreadyApplied = true` when returning cluster weapon resolution
- [ ] Keep `DamageAlreadyApplied = false` for non-cluster weapons (default)

**Expected Change:**

Around line 170 (cluster weapon return):
```csharp
// Create hit locations data with multiple hits
return new AttackResolutionData(
    toHitNumber, 
    attackRoll, 
    isHit, 
    attackDirection, 
    hitLocationsData,
    DamageAlreadyApplied: true  // NEW: Damage applied incrementally
);
```

### Step 5: Modify FinalizeAttackResolution Method

**File:** `src/MakaMek.Core/Models/Game/Phases/WeaponAttackResolutionPhase.cs`  
**Method:** `FinalizeAttackResolution` (lines 352-441)

- [ ] Check `DamageAlreadyApplied` flag before applying damage
- [ ] Skip damage application if flag is `true`
- [ ] Add comment explaining why damage is skipped
- [ ] Ensure critical hits and PSRs still execute

**Expected Change:**

Around line 361:
```csharp
// Apply damage to the target
if (resolution is { IsHit: true, HitLocationsData.HitLocations: not null })
{
    // For cluster weapons, damage was already applied incrementally in ResolveClusterWeaponHit
    // to ensure each damage group sees the updated armor/structure state
    if (!resolution.DamageAlreadyApplied)
    {
        target.ApplyDamage(resolution.HitLocationsData.HitLocations, resolution.AttackDirection);
    }
}
```

---

## Testing

### Step 6: Add Unit Tests

**File:** `tests/MakaMek.Core.Tests/Models/Game/Phases/WeaponAttackResolutionPhaseTests.cs`

- [ ] Test: Multiple groups hit same location with limited armor
- [ ] Test: Multiple groups hit same location causing destruction
- [ ] Test: Multiple groups hit different locations
- [ ] Test: Damage transfer between groups
- [ ] Test: Non-cluster weapon regression

**Test Template:**
```csharp
[Fact]
public void ResolveClusterWeaponHit_MultipleGroupsSameLocation_AppliesDamageSequentially()
{
    // Arrange
    // - Create target with limited armor (e.g., 2 armor, 8 structure on CT)
    // - Create LRM20 weapon
    // - Setup cluster roll to return 9 missiles (2 groups: 5 + 4)
    // - Setup location rolls to hit same location (CT)
    
    // Act
    // - Call ResolveClusterWeaponHit or trigger full attack resolution
    
    // Assert
    // - Verify total armor damage = 2 (not 4)
    // - Verify total structure damage = 7 (not 5)
    // - Verify target's CT has 0 armor, 1 structure remaining
}
```

### Step 7: Run Existing Tests

- [ ] Run all tests in `WeaponAttackResolutionPhaseTests.cs`
- [ ] Run all tests in `DamageTransferCalculatorTests.cs`
- [ ] Run all tests in `UnitTests.cs`
- [ ] Run full test suite
- [ ] Fix any failing tests

### Step 8: Manual Testing

- [ ] Test LRM5 attack with multiple groups
- [ ] Test LRM10 attack with multiple groups
- [ ] Test LRM15 attack with multiple groups
- [ ] Test LRM20 attack with multiple groups
- [ ] Test SRM2, SRM4, SRM6 attacks
- [ ] Test non-cluster weapons (AC/5, PPC, Laser)
- [ ] Verify game log output is correct
- [ ] Verify UI displays correct damage values

---

## Validation

### Step 9: Code Review

- [ ] Review all code changes
- [ ] Verify no unintended side effects
- [ ] Check for code style consistency
- [ ] Verify XML documentation is complete
- [ ] Check for potential null reference issues

### Step 10: Performance Testing

- [ ] Measure attack resolution time before changes
- [ ] Measure attack resolution time after changes
- [ ] Verify no significant performance degradation
- [ ] Profile if needed

### Step 11: Integration Testing

- [ ] Test full game scenario with multiple cluster weapons
- [ ] Verify command publishing works correctly
- [ ] Verify client synchronization
- [ ] Test edge cases (unit destruction, location destruction)

---

## Documentation

### Step 12: Update Documentation

- [ ] Add comments to modified code
- [ ] Update any relevant architecture documents
- [ ] Update changelog/release notes
- [ ] Document any breaking changes (should be none)

---

## Deployment

### Step 13: Pre-Deployment Checklist

- [ ] All tests pass
- [ ] Code review approved
- [ ] No merge conflicts
- [ ] Branch is up to date with main
- [ ] Performance validated

### Step 14: Merge and Deploy

- [ ] Create pull request
- [ ] Get approval from team
- [ ] Merge to main branch
- [ ] Deploy to test environment
- [ ] Verify in test environment
- [ ] Deploy to production

---

## Post-Deployment

### Step 15: Monitoring

- [ ] Monitor for any issues in production
- [ ] Check error logs
- [ ] Verify player feedback
- [ ] Monitor performance metrics

### Step 16: Follow-Up

- [ ] Close related issues/tickets
- [ ] Update project board
- [ ] Document lessons learned
- [ ] Consider future enhancements (per-group logging, animations)

---

## Rollback Plan

If issues are discovered after deployment:

1. **Immediate Actions:**
   - [ ] Revert the merge commit
   - [ ] Deploy previous version
   - [ ] Notify team

2. **Investigation:**
   - [ ] Identify root cause of issue
   - [ ] Determine if fix is needed or design change required
   - [ ] Create new ticket for investigation

3. **Resolution:**
   - [ ] Fix identified issues
   - [ ] Re-test thoroughly
   - [ ] Re-deploy with fixes

---

## Success Criteria

- ✅ Multiple damage groups hitting same location calculate damage correctly
- ✅ Total armor damage never exceeds available armor
- ✅ Damage transfer works correctly when location is destroyed
- ✅ All existing tests pass
- ✅ No performance degradation
- ✅ Client synchronization works correctly
- ✅ Game log displays correct values
- ✅ No regressions in non-cluster weapons

---

## Notes

- Keep changes minimal and focused
- Preserve existing architecture
- Maintain backward compatibility
- Document all changes clearly
- Test thoroughly before deployment

---

## Estimated Time

- Code changes: 2-3 hours
- Testing: 3-4 hours
- Validation: 1-2 hours
- **Total: 6-9 hours**

---

## Contact

If you encounter issues during implementation:
- Review the detailed solution document: `docs/solution-cluster-hit-resolution.md`
- Check the bug analysis: `docs/bug-analysis-cluster-hit-resolution.md`
- Consult the investigation summary: `docs/bug-investigation-summary.md`


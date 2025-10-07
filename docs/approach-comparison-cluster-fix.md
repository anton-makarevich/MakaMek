# Approach Comparison: Cluster Hit Damage Resolution Fix

## Executive Summary

After thorough analysis, **I recommend the Calculator Refactoring Approach (Strategy 1: Pass Damage History)** over the original Incremental Application approach. This recommendation is based on superior architectural alignment, better separation of concerns, and lower maintenance complexity.

---

## Detailed Comparison

### Approach A: Incremental Damage Application (Original Proposal)

**Description:** Apply damage after each damage group is calculated, before calculating the next group.

#### Implementation
```csharp
// In ResolveClusterWeaponHit
for (var i = 0; i < completeClusterHits; i++)
{
    var hitLocationData = DetermineHitLocation(...);
    
    // Apply damage immediately
    target.ApplyDamage([hitLocationData], attackDirection);
    
    hitLocations.Add(hitLocationData);
}

// In FinalizeAttackResolution
if (!resolution.DamageAlreadyApplied)  // Check flag
{
    target.ApplyDamage(...);
}
```

#### Pros
- ✅ Simple to understand
- ✅ Minimal code changes
- ✅ Direct solution to the problem

#### Cons
- ❌ **Dual damage application points:** Damage applied in two different places (`ResolveClusterWeaponHit` for cluster weapons, `FinalizeAttackResolution` for non-cluster weapons)
- ❌ **Flag pollution:** `DamageAlreadyApplied` flag in `AttackResolutionData` violates SRP - this DTO shouldn't control execution flow
- ❌ **Breaks separation of concerns:** Calculation phase now includes side effects (state mutation)
- ❌ **Harder to test:** Need to verify damage application happens at correct time, not just correct calculation
- ❌ **Fragile:** Future developers might not understand why cluster weapons apply damage differently
- ❌ **Command/Query Separation violation:** `ResolveClusterWeaponHit` becomes both a query (calculate damage) and a command (apply damage)

---

### Approach B: Calculator Refactoring (Recommended)

**Description:** Refactor `DamageTransferCalculator.CalculateStructureDamage` to account for previously calculated damage groups.

#### Strategy 1: Pass Damage History (RECOMMENDED)

**Implementation:**
```csharp
// New interface signature
IReadOnlyList<LocationDamageData> CalculateStructureDamage(
    Unit unit,
    PartLocation initialLocation,
    int totalDamage,
    HitDirection hitDirection,
    IReadOnlyList<LocationHitData>? previousDamageGroups = null);  // NEW

// In ResolveClusterWeaponHit
var hitLocations = new List<LocationHitData>();

for (var i = 0; i < completeClusterHits; i++)
{
    var damageData = Game.DamageTransferCalculator.CalculateStructureDamage(
        target, 
        hitLocation, 
        clusterDamage, 
        attackDirection,
        hitLocations);  // Pass previously calculated groups
    
    var hitLocationData = new LocationHitData(damageData, ...);
    hitLocations.Add(hitLocationData);
}
```

**Calculator Implementation:**
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
    
    // Rest of calculation uses accumulatedDamage to adjust available armor/structure
    ...
}

private Dictionary<PartLocation, (int armorDamage, int structureDamage)> 
    CalculateAccumulatedDamage(IReadOnlyList<LocationHitData>? previousDamageGroups)
{
    var accumulated = new Dictionary<PartLocation, (int, int)>();
    
    if (previousDamageGroups == null) return accumulated;
    
    foreach (var group in previousDamageGroups)
    {
        foreach (var damage in group.Damage)
        {
            if (!accumulated.ContainsKey(damage.Location))
                accumulated[damage.Location] = (0, 0);
            
            var current = accumulated[damage.Location];
            accumulated[damage.Location] = (
                current.armorDamage + damage.ArmorDamage,
                current.structureDamage + damage.StructureDamage
            );
        }
    }
    
    return accumulated;
}
```

#### Pros
- ✅ **Single damage application point:** Damage still applied only in `FinalizeAttackResolution`
- ✅ **No flag pollution:** `AttackResolutionData` remains a pure DTO
- ✅ **Preserves separation of concerns:** Calculation remains pure, application remains separate
- ✅ **Easier to test:** Calculator is pure function, easier to unit test
- ✅ **Command/Query Separation maintained:** `ResolveClusterWeaponHit` remains a query
- ✅ **Self-documenting:** Parameter name `previousDamageGroups` clearly indicates purpose
- ✅ **Backward compatible:** Optional parameter means existing callers don't break
- ✅ **Functional approach:** Calculator becomes more functional/pure

#### Cons
- ⚠️ **Slightly more complex calculator:** Need to track accumulated damage
- ⚠️ **More parameters:** Calculator signature becomes longer
- ⚠️ **Performance:** Small overhead from calculating accumulated damage (negligible in practice)

---

#### Strategy 2: Temporary Unit Copy (NOT RECOMMENDED)

**Why NOT recommended:**

1. **`ToData()` loses current state:** The `UnitExtensions.ToData()` method uses `MaxArmor` and `MaxRearArmor` (lines 26-27, 32), NOT `CurrentArmor`. This means:
   ```csharp
   // UnitExtensions.cs, lines 24-27
   SideTorso sideTorso => new ArmorLocation
   {
       FrontArmor = sideTorso.MaxArmor,      // ❌ NOT CurrentArmor!
       RearArmor = sideTorso.MaxRearArmor    // ❌ NOT CurrentRearArmor!
   },
   ```
   - Creating a unit from `ToData()` would give a fresh unit with full armor
   - Would need to modify `ToData()` to capture current state
   - Would need to modify `MechFactory.Create()` to apply current damage state

2. **Expensive operation:** Creating a full unit involves:
   - Creating all parts
   - Creating all components
   - Mounting all components
   - Setting up all relationships
   - Much more expensive than tracking accumulated damage

3. **Complexity:** Would need to:
   - Modify `ToData()` to include current armor/structure
   - Modify `UnitData` to include current state fields
   - Modify `MechFactory` to apply current state
   - Handle all edge cases (destroyed parts, damaged components, etc.)

4. **Memory overhead:** Creating temporary units for each calculation

**Verdict:** Strategy 2 is architecturally unsound and unnecessarily complex.

---

## Architectural Impact Analysis

### Separation of Concerns

| Aspect | Incremental Application | Calculator Refactoring |
|--------|------------------------|------------------------|
| Calculation vs Application | ❌ Mixed | ✅ Separated |
| DTO purity | ❌ Polluted with flags | ✅ Pure data |
| Single Responsibility | ❌ Violated | ✅ Maintained |
| Command/Query Separation | ❌ Violated | ✅ Maintained |

### Code Maintainability

| Aspect | Incremental Application | Calculator Refactoring |
|--------|------------------------|------------------------|
| Damage application points | ❌ Two places | ✅ One place |
| Future developer confusion | ❌ High risk | ✅ Low risk |
| Code clarity | ⚠️ Moderate | ✅ High |
| Debugging complexity | ⚠️ Moderate | ✅ Low |

### Testing Complexity

| Aspect | Incremental Application | Calculator Refactoring |
|--------|------------------------|------------------------|
| Unit test isolation | ❌ Harder (side effects) | ✅ Easier (pure function) |
| Test setup complexity | ⚠️ Moderate | ✅ Simple |
| Mock requirements | ⚠️ More mocks needed | ✅ Fewer mocks needed |
| Test clarity | ⚠️ Moderate | ✅ High |

### Performance

| Aspect | Incremental Application | Calculator Refactoring |
|--------|------------------------|------------------------|
| Damage application calls | ⚠️ Multiple (per group) | ✅ Single (per attack) |
| Calculation overhead | ✅ None | ⚠️ Minimal (accumulation) |
| Memory allocation | ✅ Minimal | ⚠️ Slightly more (accumulation dict) |
| **Overall** | ✅ Slightly faster | ✅ Negligible difference |

**Performance Verdict:** Both approaches have negligible performance impact. The difference is measured in microseconds for typical cluster attacks (2-4 groups).

### Alignment with Existing Patterns

**Current Codebase Pattern:**
- `DamageTransferCalculator` is a **pure calculator** - it calculates without side effects
- `Unit.ApplyDamage` is the **single point** for damage application
- DTOs like `AttackResolutionData` are **pure data** without business logic

| Pattern | Incremental Application | Calculator Refactoring |
|---------|------------------------|------------------------|
| Pure calculator pattern | ❌ Breaks | ✅ Maintains |
| Single application point | ❌ Breaks | ✅ Maintains |
| Pure DTO pattern | ❌ Breaks | ✅ Maintains |
| **Overall Alignment** | ❌ Poor | ✅ Excellent |

---

## Risk Assessment

### Incremental Application Risks

1. **High Risk:** Future developer adds another weapon type that needs special handling, doesn't realize damage is applied in two places
2. **Medium Risk:** Flag-based control flow becomes confusing when debugging
3. **Medium Risk:** Testing becomes harder due to side effects in calculation phase
4. **Low Risk:** Performance impact (actually slightly better)

### Calculator Refactoring Risks

1. **Low Risk:** Accumulated damage calculation has bugs (easily testable)
2. **Low Risk:** Performance impact from accumulation (negligible)
3. **Very Low Risk:** Breaking existing callers (optional parameter)

---

## Recommendation: Calculator Refactoring (Strategy 1)

### Why This is the Better Approach

1. **Architectural Integrity:** Maintains separation of concerns, pure calculator pattern, and single damage application point

2. **Maintainability:** Future developers will understand the code more easily because it follows established patterns

3. **Testability:** Pure functions are easier to test than functions with side effects

4. **Clarity:** The `previousDamageGroups` parameter clearly communicates intent

5. **Backward Compatibility:** Optional parameter means zero impact on existing code

6. **Functional Programming Principles:** Calculator becomes more functional/pure

### Addressing Your Concerns

**Your Concern 1: Dual damage application points**
- ✅ **Resolved:** Calculator refactoring maintains single application point in `FinalizeAttackResolution`

**Your Concern 2: Flag pollution in DTOs**
- ✅ **Resolved:** No flags needed, `AttackResolutionData` remains pure

---

## Implementation Details (Calculator Refactoring)

### Files to Modify

1. **`IDamageTransferCalculator.cs`** - Add optional parameter to interface
2. **`DamageTransferCalculator.cs`** - Implement accumulated damage logic
3. **`WeaponAttackResolutionPhase.cs`** - Pass previous groups when calling calculator

### No Changes Needed

- ❌ `AttackResolutionData.cs` - Remains unchanged
- ❌ `FinalizeAttackResolution` - Remains unchanged
- ❌ `Unit.ApplyDamage` - Remains unchanged
- ❌ `FallingDamageCalculator.cs` - Remains unchanged (doesn't pass previous groups)

### Exact Signature Changes

**Before:**
```csharp
IReadOnlyList<LocationDamageData> CalculateStructureDamage(
    Unit unit,
    PartLocation initialLocation,
    int totalDamage,
    HitDirection hitDirection);
```

**After:**
```csharp
IReadOnlyList<LocationDamageData> CalculateStructureDamage(
    Unit unit,
    PartLocation initialLocation,
    int totalDamage,
    HitDirection hitDirection,
    IReadOnlyList<LocationHitData>? previousDamageGroups = null);
```

### Impact on Existing Callers

**Callers identified:**
1. `WeaponAttackResolutionPhase.DetermineHitLocation` (line 294)
2. `FallingDamageCalculator.CalculateFallingDamage` (line 82)

**Impact:** ✅ **ZERO** - Optional parameter means existing calls work unchanged

---

## Edge Cases to Consider

### 1. Damage Transfer Between Groups

**Scenario:** First group destroys location, second group transfers

**Handling:**
- Accumulated damage includes destroyed locations
- Calculator sees location has 0 remaining structure
- Transfers to next location (existing logic, lines 42-46)
- ✅ Works correctly

### 2. Multiple Groups Hit Different Locations

**Scenario:** Group 1 hits CT, Group 2 hits RA

**Handling:**
- Accumulated damage tracks per-location
- Group 2's calculation only sees Group 1's damage to CT
- RA calculation unaffected
- ✅ Works correctly

### 3. Rear Armor Handling

**Scenario:** Multiple groups hit rear torso

**Handling:**
- `LocationDamageData` includes `IsRearArmor` flag
- Accumulated damage respects armor type
- ✅ Works correctly (need to track front/rear separately)

**Implementation Note:** Need to track front and rear armor separately in accumulated damage:
```csharp
Dictionary<PartLocation, (int frontArmor, int rearArmor, int structure)>
```

---

## Testing Strategy

### Unit Tests for Calculator

```csharp
[Fact]
public void CalculateStructureDamage_WithPreviousDamageGroups_AccountsForAccumulatedDamage()
{
    // Arrange
    var unit = CreateTestMech();  // CT: 10 armor, 10 structure
    
    // First group: 7 damage to CT
    var group1 = new LocationHitData(
        [new LocationDamageData(PartLocation.CenterTorso, 7, 0, false)],
        [], [], PartLocation.CenterTorso);
    
    // Act: Calculate second group (5 damage) with previous group
    var result = _sut.CalculateStructureDamage(
        unit, 
        PartLocation.CenterTorso, 
        5, 
        HitDirection.Front,
        [group1]);  // Pass previous group
    
    // Assert: Should see only 3 armor remaining (10 - 7)
    result.ShouldHaveSingleItem();
    result[0].ArmorDamage.ShouldBe(3);  // Not 5!
    result[0].StructureDamage.ShouldBe(2);  // Overflow to structure
}
```

### Integration Tests

```csharp
[Fact]
public void ResolveClusterWeaponHit_MultipleGroupsSameLocation_CalculatesDamageSequentially()
{
    // Full integration test with actual weapon attack resolution
    // Verify total armor damage never exceeds available armor
}
```

---

## Performance Analysis

### Accumulated Damage Calculation Cost

**Typical cluster attack:**
- 2-4 damage groups
- Each group hits 1-3 locations
- Accumulation: ~10 dictionary lookups and additions

**Cost:** < 1 microsecond on modern hardware

**Verdict:** ✅ Negligible

### Comparison

| Operation | Incremental Application | Calculator Refactoring |
|-----------|------------------------|------------------------|
| Damage application calls | 2-4 (per group) | 1 (per attack) |
| Accumulation overhead | 0 | < 1 μs |
| **Total difference** | Slightly faster | Negligibly slower |

**Conclusion:** Performance difference is unmeasurable in practice.

---

## Final Recommendation

**Implement Calculator Refactoring (Strategy 1: Pass Damage History)**

### Justification

1. ✅ **Superior architecture:** Maintains all existing design patterns
2. ✅ **Better maintainability:** Single damage application point
3. ✅ **Easier testing:** Pure calculator function
4. ✅ **No DTO pollution:** Data structures remain pure
5. ✅ **Backward compatible:** Optional parameter
6. ✅ **Clear intent:** Self-documenting code
7. ✅ **Negligible performance impact:** < 1 μs overhead

### Why NOT Incremental Application

1. ❌ Breaks separation of concerns
2. ❌ Pollutes DTOs with control flow flags
3. ❌ Creates dual damage application points
4. ❌ Violates command/query separation
5. ❌ Harder to maintain long-term

---

## Next Steps

If you approve this recommendation, I will provide:

1. **Detailed implementation guide** with exact code changes
2. **Complete test suite** for the new functionality
3. **Migration guide** for any affected code
4. **Performance benchmarks** to verify negligible impact

Would you like me to proceed with the detailed implementation plan for the Calculator Refactoring approach?


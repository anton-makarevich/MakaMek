# Bug Analysis: Cluster Hit Damage Resolution

## Issue Summary
When resolving cluster hits with multiple damage groups targeting the same location, the armor/structure damage distribution is calculated incorrectly. The system calculates damage distribution for all groups based on the target's initial state, rather than applying each group's damage sequentially before calculating the next group's distribution.

## Root Cause

### Location
`src/MakaMek.Core/Models/Game/Phases/WeaponAttackResolutionPhase.cs`
- Method: `ResolveClusterWeaponHit` (lines 187-244)
- Method: `DetermineHitLocation` (lines 255-318)
- Method: `FinalizeAttackResolution` (lines 352-441)

### Problem Flow

1. **Damage Calculation Phase** (`ResolveClusterWeaponHit`, lines 207-241):
   ```csharp
   for (var i = 0; i < completeClusterHits; i++)
   {
       var clusterDamage = weapon.ClusterSize * damagePerMissile;
       var hitLocationData = DetermineHitLocation(attackDirection, clusterDamage, target, weapon, weaponTargetData);
       hitLocations.Add(hitLocationData);
   }
   ```
   - Each damage group calls `DetermineHitLocation`
   - `DetermineHitLocation` calls `DamageTransferCalculator.CalculateStructureDamage` (line 294)
   - The calculator reads `part.CurrentArmor` and `part.CurrentStructure` (DamageTransferCalculator.cs, lines 77, 87)
   - **All calculations see the same initial state** because damage hasn't been applied yet

2. **Damage Application Phase** (`FinalizeAttackResolution`, line 363):
   ```csharp
   if (resolution is { IsHit: true, HitLocationsData.HitLocations: not null })
   {
       target.ApplyDamage(resolution.HitLocationsData.HitLocations, resolution.AttackDirection);
   }
   ```
   - Damage is only applied **after all damage groups are calculated**
   - This is too late - each group should see the updated state from previous groups

## Test Scenario

### Setup
- Weapon: LRM20 (20 missiles, 1 damage each, ClusterSize = 5)
- Cluster roll: 4 → 9 missiles hit (from cluster hit table)
- Damage groups: 
  - Group 1: 5 missiles = 5 damage (complete cluster)
  - Group 2: 4 missiles = 4 damage (partial cluster)
- Hit location: Both groups roll 7 → Center Torso
- Target's CT state: 2 armor points, 8 internal structure points

### Current (Incorrect) Behavior

**Group 1 calculation:**
- Sees: 2 armor, 8 structure
- Calculates: 2 armor damage, 3 structure damage

**Group 2 calculation:**
- Sees: **2 armor, 8 structure** (same as Group 1!)
- Calculates: 2 armor damage, 2 structure damage

**Total damage:**
- 4 armor damage, 5 structure damage
- **IMPOSSIBLE**: CT only has 2 armor points!

### Expected (Correct) Behavior

**Group 1 calculation:**
- Sees: 2 armor, 8 structure
- Calculates: 2 armor damage, 3 structure damage
- **Apply damage immediately** → CT now has 0 armor, 5 structure

**Group 2 calculation:**
- Sees: **0 armor, 5 structure** (updated state!)
- Calculates: 0 armor damage, 4 structure damage

**Total damage:**
- 2 armor damage, 7 structure damage
- **CORRECT**: Matches CT's actual armor capacity

## Proposed Solutions

### Solution 1: Apply Damage Incrementally (Recommended)

**Approach:** Apply damage to the target after each damage group is calculated, before calculating the next group.

**Changes Required:**
1. Modify `ResolveClusterWeaponHit` to apply damage incrementally
2. Track damage groups separately for rendering/logging purposes
3. Ensure damage application doesn't trigger side effects (PSRs, critical hits) until all groups are processed

**Pros:**
- Minimal architectural changes
- Maintains separation between calculation and application
- Accurate damage distribution
- Preserves existing command structure for client synchronization

**Cons:**
- Slightly more complex logic in `ResolveClusterWeaponHit`
- Need to carefully manage when side effects (PSRs, criticals) are triggered

### Solution 2: Create Mutable Damage State Tracker

**Approach:** Create a temporary state object that tracks armor/structure changes during calculation without modifying the actual unit.

**Pros:**
- Pure calculation approach (no side effects during calculation)
- Clear separation of concerns

**Cons:**
- More complex implementation
- Requires new abstraction layer
- Potential for state synchronization issues

### Solution 3: Modify DamageTransferCalculator to Accept Current State

**Approach:** Pass current armor/structure values as parameters instead of reading from the unit.

**Pros:**
- Calculator becomes more functional/pure
- Easier to test

**Cons:**
- Breaks existing API
- Requires changes to many call sites
- Doesn't solve the fundamental sequencing issue

## Recommendation

**Solution 1** is recommended because:
1. Minimal changes to existing architecture
2. Maintains the existing command/event flow for client synchronization
3. Accurately models the physical reality (damage groups hit sequentially, not simultaneously)
4. Preserves the separation between damage calculation and side effects (PSRs, critical hits)

## Implementation Notes

### Key Considerations

1. **Damage Application Timing:**
   - Apply each group's damage immediately after calculation
   - Defer PSR calculations until all groups are processed (existing behavior)
   - Defer critical hit calculations until all groups are processed (existing behavior)

2. **Command Structure:**
   - Keep the single `WeaponAttackResolutionCommand` per weapon attack
   - Include all damage groups in the command for proper client rendering
   - Ensure clients can reconstruct the full damage sequence

3. **Testing:**
   - Add specific tests for multiple groups hitting the same location
   - Test armor depletion scenarios
   - Test damage transfer scenarios (when location is destroyed mid-attack)

### Related Code Sections

- `DamageTransferCalculator.CalculateStructureDamage` (lines 12-19, 70-100)
  - Reads current armor/structure from unit parts
  - This is correct - the issue is when it's called, not how it works

- `Unit.ApplyDamage` (lines 430-456)
  - Applies pre-calculated damage to unit parts
  - Updates armor and structure values
  - This works correctly - just needs to be called earlier

- `FinalizeAttackResolution` (lines 352-441)
  - Currently applies all damage at once
  - Handles PSRs and critical hits
  - Need to ensure these side effects still happen at the right time

## BattleTech Rules Reference

According to BattleTech rules:
- Cluster weapons roll once for number of missiles that hit
- Missiles are grouped into clusters (typically 5 missiles per cluster)
- Each cluster rolls separately for hit location
- Damage is applied sequentially, not simultaneously
- This matters when multiple clusters hit the same location with limited armor

The current implementation violates the sequential damage application rule.


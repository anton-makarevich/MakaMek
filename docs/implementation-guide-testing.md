# Testing Guide: Calculator Refactoring Approach

## Overview

This guide provides comprehensive test cases for the cluster hit damage resolution fix using the Calculator Refactoring approach.

---

## Test Strategy

### Test Levels

1. **Unit Tests:** Test `DamageTransferCalculator` in isolation
2. **Integration Tests:** Test full weapon attack resolution flow
3. **Regression Tests:** Ensure existing functionality unchanged

---

## Unit Tests for DamageTransferCalculator

**File:** `tests/MakaMek.Core.Tests/Models/Game/Mechanics/DamageTransferCalculatorTests.cs`

### Test 1: Multiple Groups Same Location - Armor Depletion

```csharp
[Fact]
public void CalculateStructureDamage_WithPreviousDamageGroups_AccountsForAccumulatedArmorDamage()
{
    // Arrange
    var unit = CreateTestMech();
    var centerTorso = unit.Parts[PartLocation.CenterTorso];
    
    // Assume CT has 10 armor, 10 structure
    // First group: 7 damage to CT armor
    var group1Damage = _sut.CalculateStructureDamage(
        unit, 
        PartLocation.CenterTorso, 
        7, 
        HitDirection.Front);
    var group1 = new LocationHitData(group1Damage, [], [], PartLocation.CenterTorso);
    
    // Act: Calculate second group (5 damage) with previous group
    var result = _sut.CalculateStructureDamage(
        unit, 
        PartLocation.CenterTorso, 
        5, 
        HitDirection.Front,
        [group1]);
    
    // Assert: Should see only 3 armor remaining (10 - 7)
    result.ShouldHaveSingleItem();
    result[0].Location.ShouldBe(PartLocation.CenterTorso);
    result[0].ArmorDamage.ShouldBe(3);  // Not 5! Only 3 armor left
    result[0].StructureDamage.ShouldBe(2);  // Overflow to structure
    result[0].IsLocationDestroyed.ShouldBeFalse();
}
```

### Test 2: Multiple Groups Same Location - Structure Damage

```csharp
[Fact]
public void CalculateStructureDamage_WithPreviousDamageGroups_AccountsForAccumulatedStructureDamage()
{
    // Arrange
    var unit = CreateTestMech();
    var centerTorso = unit.Parts[PartLocation.CenterTorso];
    
    // First group: Depletes all armor and does 3 structure damage
    var armorPlusThree = centerTorso.CurrentArmor + 3;
    var group1Damage = _sut.CalculateStructureDamage(
        unit, 
        PartLocation.CenterTorso, 
        armorPlusThree, 
        HitDirection.Front);
    var group1 = new LocationHitData(group1Damage, [], [], PartLocation.CenterTorso);
    
    // Act: Calculate second group (4 damage) with previous group
    var result = _sut.CalculateStructureDamage(
        unit, 
        PartLocation.CenterTorso, 
        4, 
        HitDirection.Front,
        [group1]);
    
    // Assert: Should see no armor, and structure reduced by 3
    result.ShouldHaveSingleItem();
    result[0].Location.ShouldBe(PartLocation.CenterTorso);
    result[0].ArmorDamage.ShouldBe(0);  // No armor left
    result[0].StructureDamage.ShouldBe(4);  // All damage to structure
    result[0].IsLocationDestroyed.ShouldBeFalse();
}
```

### Test 3: Multiple Groups Same Location - Location Destroyed

```csharp
[Fact]
public void CalculateStructureDamage_WithPreviousDamageGroups_DetectsLocationDestruction()
{
    // Arrange
    var unit = CreateTestMech();
    var rightArm = unit.Parts[PartLocation.RightArm];
    
    // First group: Depletes armor and does half structure damage
    var firstGroupDamage = rightArm.CurrentArmor + (rightArm.CurrentStructure / 2);
    var group1Damage = _sut.CalculateStructureDamage(
        unit, 
        PartLocation.RightArm, 
        firstGroupDamage, 
        HitDirection.Front);
    var group1 = new LocationHitData(group1Damage, [], [], PartLocation.RightArm);
    
    // Act: Calculate second group that destroys the location
    var secondGroupDamage = rightArm.CurrentStructure;  // Enough to destroy
    var result = _sut.CalculateStructureDamage(
        unit, 
        PartLocation.RightArm, 
        secondGroupDamage, 
        HitDirection.Front,
        [group1]);
    
    // Assert: Location should be marked as destroyed
    result.ShouldHaveSingleItem();
    result[0].Location.ShouldBe(PartLocation.RightArm);
    result[0].IsLocationDestroyed.ShouldBeTrue();
}
```

### Test 4: Multiple Groups Different Locations

```csharp
[Fact]
public void CalculateStructureDamage_WithPreviousDamageGroups_OnlyAccountsForSameLocation()
{
    // Arrange
    var unit = CreateTestMech();
    var centerTorso = unit.Parts[PartLocation.CenterTorso];
    var rightArm = unit.Parts[PartLocation.RightArm];
    
    // First group: 7 damage to Right Arm
    var group1Damage = _sut.CalculateStructureDamage(
        unit, 
        PartLocation.RightArm, 
        7, 
        HitDirection.Front);
    var group1 = new LocationHitData(group1Damage, [], [], PartLocation.RightArm);
    
    // Act: Calculate second group (5 damage) to Center Torso with previous group
    var result = _sut.CalculateStructureDamage(
        unit, 
        PartLocation.CenterTorso, 
        5, 
        HitDirection.Front,
        [group1]);
    
    // Assert: CT should see full armor (not affected by RA damage)
    result.ShouldHaveSingleItem();
    result[0].Location.ShouldBe(PartLocation.CenterTorso);
    result[0].ArmorDamage.ShouldBe(5);  // Full damage to armor
    result[0].StructureDamage.ShouldBe(0);  // No structure damage
}
```

### Test 5: Rear Armor Handling

```csharp
[Fact]
public void CalculateStructureDamage_WithPreviousDamageGroups_TracksRearArmorSeparately()
{
    // Arrange
    var unit = CreateTestMech();
    var centerTorso = (Torso)unit.Parts[PartLocation.CenterTorso];
    
    // First group: 5 damage to front armor
    var group1Damage = _sut.CalculateStructureDamage(
        unit, 
        PartLocation.CenterTorso, 
        5, 
        HitDirection.Front);
    var group1 = new LocationHitData(group1Damage, [], [], PartLocation.CenterTorso);
    
    // Act: Calculate second group (3 damage) to rear armor with previous group
    var result = _sut.CalculateStructureDamage(
        unit, 
        PartLocation.CenterTorso, 
        3, 
        HitDirection.Rear,
        [group1]);
    
    // Assert: Rear armor should be unaffected by front armor damage
    result.ShouldHaveSingleItem();
    result[0].Location.ShouldBe(PartLocation.CenterTorso);
    result[0].ArmorDamage.ShouldBe(3);  // Full damage to rear armor
    result[0].StructureDamage.ShouldBe(0);
    result[0].IsRearArmor.ShouldBeTrue();
}
```

### Test 6: Damage Transfer Between Groups

```csharp
[Fact]
public void CalculateStructureDamage_WithPreviousDamageGroups_HandlesLocationDestructionAndTransfer()
{
    // Arrange
    var unit = CreateTestMech();
    var rightArm = unit.Parts[PartLocation.RightArm];
    
    // First group: Destroys right arm completely
    var destroyArmDamage = rightArm.CurrentArmor + rightArm.CurrentStructure;
    var group1Damage = _sut.CalculateStructureDamage(
        unit, 
        PartLocation.RightArm, 
        destroyArmDamage, 
        HitDirection.Front);
    var group1 = new LocationHitData(group1Damage, [], [], PartLocation.RightArm);
    
    // Act: Calculate second group (5 damage) to right arm with previous group
    // Should transfer to right torso
    var result = _sut.CalculateStructureDamage(
        unit, 
        PartLocation.RightArm, 
        5, 
        HitDirection.Front,
        [group1]);
    
    // Assert: Damage should transfer to right torso
    result.ShouldHaveSingleItem();
    result[0].Location.ShouldBe(PartLocation.RightTorso);  // Transferred!
    result[0].ArmorDamage.ShouldBe(5);
}
```

### Test 7: No Previous Groups (Backward Compatibility)

```csharp
[Fact]
public void CalculateStructureDamage_WithoutPreviousDamageGroups_WorksAsBeforeRefactoring()
{
    // Arrange
    var unit = CreateTestMech();
    var damage = 5;
    
    // Act: Call without previous groups (existing behavior)
    var result = _sut.CalculateStructureDamage(
        unit, 
        PartLocation.CenterTorso, 
        damage, 
        HitDirection.Front);
    
    // Assert: Should work exactly as before
    result.ShouldHaveSingleItem();
    result[0].Location.ShouldBe(PartLocation.CenterTorso);
    result[0].ArmorDamage.ShouldBe(damage);
    result[0].StructureDamage.ShouldBe(0);
}
```

### Test 8: Empty Previous Groups List

```csharp
[Fact]
public void CalculateStructureDamage_WithEmptyPreviousDamageGroups_WorksAsBeforeRefactoring()
{
    // Arrange
    var unit = CreateTestMech();
    var damage = 5;
    
    // Act: Call with empty list
    var result = _sut.CalculateStructureDamage(
        unit, 
        PartLocation.CenterTorso, 
        damage, 
        HitDirection.Front,
        new List<LocationHitData>());
    
    // Assert: Should work exactly as before
    result.ShouldHaveSingleItem();
    result[0].Location.ShouldBe(PartLocation.CenterTorso);
    result[0].ArmorDamage.ShouldBe(damage);
    result[0].StructureDamage.ShouldBe(0);
}
```

---

## Integration Tests for WeaponAttackResolutionPhase

**File:** `tests/MakaMek.Core.Tests/Models/Game/Phases/WeaponAttackResolutionPhaseTests.cs`

### Test 9: LRM20 Cluster Hit - Multiple Groups Same Location

```csharp
[Fact]
public void Enter_ClusterWeapon_MultipleGroupsSameLocation_CalculatesDamageSequentially()
{
    // Arrange
    SetMap();
    
    // Create LRM20 weapon (20 missiles, 1 damage each, cluster size 5)
    var lrm20 = new TestClusterWeapon(
        damage: 20,
        clusterSize: 5,
        clusters: 4,
        type: WeaponType.Missile);
    
    var part1 = _player1Unit1.Parts[PartLocation.LeftArm];
    part1.TryAddComponent(lrm20).ShouldBeTrue();
    
    // Set target with limited armor on CT
    var targetCT = _player2Unit1.Parts[PartLocation.CenterTorso];
    // Reduce armor to 2 points for testing
    targetCT.ApplyDamage(targetCT.CurrentArmor - 2, HitDirection.Front);
    
    // Set weapon target
    var weaponTargets = new List<WeaponTargetData>
    {
        new()
        {
            Weapon = lrm20.ToData(),
            TargetId = _player2Unit1.Id,
            IsPrimaryTarget = true
        }
    };
    _player1Unit1.DeclareWeaponAttack(weaponTargets);
    
    // Setup ToHitCalculator
    Game.ToHitCalculator.GetToHitNumber(
            Arg.Any<Unit>(),
            Arg.Any<Unit>(),
            Arg.Any<Weapon>(),
            Arg.Any<BattleMap>(),
            Arg.Any<bool>(),
            Arg.Any<PartLocation?>())
        .Returns(7);
    
    // Setup dice rolls:
    // - Attack roll: 8 (hit)
    // - Cluster roll: 4 (9 missiles hit for LRM20)
    // - Location rolls: 7, 7 (both groups hit CT)
    SetupDiceRolls(8, 4, 7, 7);
    
    // Act
    _sut.Enter();
    
    // Assert
    // 9 missiles = 2 groups (5 + 4 damage)
    // CT has 2 armor, 8 structure
    // Group 1 (5 damage): 2 armor + 3 structure
    // Group 2 (4 damage): 0 armor + 4 structure (armor already depleted)
    // Total: 2 armor + 7 structure
    
    targetCT.CurrentArmor.ShouldBe(0);  // All armor depleted
    targetCT.CurrentStructure.ShouldBe(1);  // 8 - 7 = 1
    
    // Verify command was published with correct damage
    var command = GetPublishedCommand<WeaponAttackResolutionCommand>();
    command.ShouldNotBeNull();
    
    var totalArmorDamage = command.ResolutionData.HitLocationsData!.HitLocations
        .SelectMany(h => h.Damage)
        .Sum(d => d.ArmorDamage);
    var totalStructureDamage = command.ResolutionData.HitLocationsData!.HitLocations
        .SelectMany(h => h.Damage)
        .Sum(d => d.StructureDamage);
    
    totalArmorDamage.ShouldBe(2);  // Not 4!
    totalStructureDamage.ShouldBe(7);  // Not 5!
}
```

### Test 10: SRM6 Cluster Hit - Location Destroyed Mid-Attack

```csharp
[Fact]
public void Enter_ClusterWeapon_LocationDestroyedMidAttack_TransfersDamageCorrectly()
{
    // Arrange
    SetMap();
    
    // Create SRM6 weapon
    var srm6 = new TestClusterWeapon(
        damage: 12,
        clusterSize: 2,
        clusters: 6,
        type: WeaponType.Missile);
    
    var part1 = _player1Unit1.Parts[PartLocation.LeftArm];
    part1.TryAddComponent(srm6).ShouldBeTrue();
    
    // Set target with very low armor/structure on RA
    var targetRA = _player2Unit1.Parts[PartLocation.RightArm];
    // Reduce to 3 armor, 2 structure
    targetRA.ApplyDamage(targetRA.CurrentArmor - 3, HitDirection.Front);
    targetRA.ApplyDamage(targetRA.CurrentStructure - 2, HitDirection.Front, true);
    
    // Set weapon target
    var weaponTargets = new List<WeaponTargetData>
    {
        new()
        {
            Weapon = srm6.ToData(),
            TargetId = _player2Unit1.Id,
            IsPrimaryTarget = true
        }
    };
    _player1Unit1.DeclareWeaponAttack(weaponTargets);
    
    // Setup ToHitCalculator
    Game.ToHitCalculator.GetToHitNumber(
            Arg.Any<Unit>(),
            Arg.Any<Unit>(),
            Arg.Any<Weapon>(),
            Arg.Any<BattleMap>(),
            Arg.Any<bool>(),
            Arg.Any<PartLocation?>())
        .Returns(7);
    
    // Setup dice rolls:
    // - Attack roll: 8 (hit)
    // - Cluster roll: 8 (4 missiles hit for SRM6)
    // - Location rolls: 10, 10 (both groups hit RA)
    SetupDiceRolls(8, 8, 10, 10);
    
    // Act
    _sut.Enter();
    
    // Assert
    // 4 missiles = 2 groups (2 + 2 damage)
    // RA has 3 armor, 2 structure (total 5 HP)
    // Group 1 (2 damage): 2 armor + 0 structure
    // Group 2 (2 damage): 1 armor + 1 structure (RA destroyed, transfers to RT)
    
    targetRA.IsDestroyed.ShouldBeTrue();
    
    // Verify damage transferred to RT
    var targetRT = _player2Unit1.Parts[PartLocation.RightTorso];
    targetRT.CurrentArmor.ShouldBeLessThan(targetRT.MaxArmor);
}
```

---

## Regression Tests

### Test 11: Non-Cluster Weapon Unchanged

```csharp
[Fact]
public void Enter_NonClusterWeapon_BehaviorUnchanged()
{
    // Arrange
    SetMap();
    SetupPlayer1WeaponTargets();  // Uses non-cluster weapon
    
    var targetCT = _player2Unit1.Parts[PartLocation.CenterTorso];
    var initialArmor = targetCT.CurrentArmor;
    
    SetupDiceRolls(8, 9);  // Hit, location roll
    
    // Act
    _sut.Enter();
    
    // Assert: Behavior should be exactly as before refactoring
    targetCT.CurrentArmor.ShouldBeLessThan(initialArmor);
    
    // Verify command structure unchanged
    var command = GetPublishedCommand<WeaponAttackResolutionCommand>();
    command.ShouldNotBeNull();
    command.ResolutionData.HitLocationsData.ShouldNotBeNull();
}
```

### Test 12: Falling Damage Unchanged

```csharp
[Fact]
public void FallingDamageCalculator_BehaviorUnchanged()
{
    // This test verifies that FallingDamageCalculator still works
    // even though it doesn't pass previousDamageGroups parameter
    
    // Arrange
    var mech = CreateTestMech();
    var calculator = new FallingDamageCalculator(
        Game.DiceRoller,
        Game.RulesProvider,
        Game.DamageTransferCalculator);
    
    // Act
    var result = calculator.CalculateFallingDamage(mech, 2);
    
    // Assert: Should work exactly as before
    result.ShouldNotBeNull();
    result.HitLocations.ShouldNotBeNull();
    result.HitLocations.HitLocations.ShouldNotBeEmpty();
}
```

---

## Test Helpers

### Helper: Get Published Command

```csharp
private T? GetPublishedCommand<T>() where T : IGameCommand
{
    return CommandPublisher.ReceivedCalls()
        .Where(call => call.GetMethodInfo().Name == "PublishCommand")
        .Select(call => call.GetArguments()[0])
        .OfType<T>()
        .FirstOrDefault();
}
```

---

## Test Coverage Goals

- ✅ Multiple groups hitting same location with armor depletion
- ✅ Multiple groups hitting same location with structure damage
- ✅ Multiple groups causing location destruction
- ✅ Multiple groups hitting different locations
- ✅ Rear armor handling
- ✅ Damage transfer between groups
- ✅ Backward compatibility (no previous groups)
- ✅ Empty previous groups list
- ✅ Full integration with weapon attack resolution
- ✅ Location destroyed mid-attack with transfer
- ✅ Non-cluster weapon regression
- ✅ Falling damage regression

---

## Running Tests

```bash
# Run all DamageTransferCalculator tests
dotnet test --filter "FullyQualifiedName~DamageTransferCalculatorTests"

# Run all WeaponAttackResolutionPhase tests
dotnet test --filter "FullyQualifiedName~WeaponAttackResolutionPhaseTests"

# Run all tests
dotnet test
```

---

## Success Criteria

- ✅ All new tests pass
- ✅ All existing tests pass (no regressions)
- ✅ Code coverage > 90% for modified code
- ✅ No performance degradation


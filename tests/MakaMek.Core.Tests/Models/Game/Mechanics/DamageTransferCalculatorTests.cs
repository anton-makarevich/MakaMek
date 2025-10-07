using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics;

public class DamageTransferCalculatorTests
{
    private readonly DamageTransferCalculator _sut;
    private readonly MechFactory _mechFactory;

    public DamageTransferCalculatorTests()
    {
        // Setup rules provider for unit creation
        IRulesProvider rules = new ClassicBattletechRulesProvider();

        // Setup localization service for unit creation
        var localizationService = Substitute.For<ILocalizationService>();

        // Setup mech factory
        _mechFactory = new MechFactory(
            rules,
            new ClassicBattletechComponentProvider(),
            localizationService);
        _sut = new DamageTransferCalculator(_mechFactory);
    }

    private Unit CreateTestMech()
    {
        var mechData = MechFactoryTests.CreateDummyMechData();
        return _mechFactory.Create(mechData);
    }

    [Fact]
    public void CalculateStructureDamage_WithZeroDamage_ReturnsEmptyList()
    {
        // Arrange
        var unit = CreateTestMech();
        
        // Act
        var result = _sut.CalculateStructureDamage(unit, PartLocation.CenterTorso, 0, HitDirection.Front);
        
        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void CalculateStructureDamage_WithNegativeDamage_ReturnsEmptyList()
    {
        // Arrange
        var unit = CreateTestMech();
        
        // Act
        var result = _sut.CalculateStructureDamage(unit, PartLocation.CenterTorso, -5, HitDirection.Front);
        
        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void CalculateStructureDamage_WithInvalidLocation_ReturnsEmptyList()
    {
        // Arrange
        var unit = CreateTestMech();
        
        // Act
        var result = _sut.CalculateStructureDamage(unit, (PartLocation)999, 10, HitDirection.Front);
        
        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void CalculateStructureDamage_DamageOnlyArmor_ReturnsCorrectData()
    {
        // Arrange
        var unit = CreateTestMech();
        var armorDamage = 5; // Less than available armor
        
        // Act
        var result = _sut.CalculateStructureDamage(unit, PartLocation.CenterTorso, armorDamage, HitDirection.Front);
        
        // Assert
        result.ShouldHaveSingleItem();
        var damageData = result[0];
        damageData.Location.ShouldBe(PartLocation.CenterTorso);
        damageData.ArmorDamage.ShouldBe(armorDamage);
        damageData.StructureDamage.ShouldBe(0);
        damageData.IsLocationDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void CalculateStructureDamage_DamageArmorAndStructure_ReturnsCorrectData()
    {
        // Arrange
        var unit = CreateTestMech();
        var centerTorso = unit.Parts[PartLocation.CenterTorso];
        var totalDamage = centerTorso.CurrentArmor + 3; // Exceed armor by 3
        
        // Act
        var result = _sut.CalculateStructureDamage(unit, PartLocation.CenterTorso, totalDamage, HitDirection.Front);
        
        // Assert
        result.ShouldHaveSingleItem();
        var damageData = result[0];
        damageData.Location.ShouldBe(PartLocation.CenterTorso);
        damageData.ArmorDamage.ShouldBe(centerTorso.CurrentArmor);
        damageData.StructureDamage.ShouldBe(3);
        damageData.IsLocationDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void CalculateStructureDamage_DestroyLocation_ReturnsCorrectData()
    {
        // Arrange
        var unit = CreateTestMech();
        var centerTorso = unit.Parts[PartLocation.CenterTorso];
        var totalDamage = centerTorso.CurrentArmor + centerTorso.CurrentStructure; // Destroy location
        
        // Act
        var result = _sut.CalculateStructureDamage(unit, PartLocation.CenterTorso, totalDamage, HitDirection.Front);
        
        // Assert
        result.ShouldHaveSingleItem();
        var damageData = result[0];
        damageData.Location.ShouldBe(PartLocation.CenterTorso);
        damageData.ArmorDamage.ShouldBe(centerTorso.CurrentArmor);
        damageData.StructureDamage.ShouldBe(centerTorso.CurrentStructure);
        damageData.IsLocationDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void CalculateStructureDamage_ExcessDamageWithTransfer_ReturnsMultipleLocations()
    {
        // Arrange
        var unit = CreateTestMech();
        var leftArm = unit.Parts[PartLocation.LeftArm];

        // Calculate damage to destroy left arm and damage left torso
        var totalDamage = leftArm.CurrentArmor + leftArm.CurrentStructure + 5;
        
        // Act
        var result = _sut.CalculateStructureDamage(unit, PartLocation.LeftArm, totalDamage, HitDirection.Front);
        
        // Assert
        result.Count.ShouldBe(2);
        
        // First location (LeftArm) should be destroyed
        var armDamage = result[0];
        armDamage.Location.ShouldBe(PartLocation.LeftArm);
        armDamage.ArmorDamage.ShouldBe(leftArm.CurrentArmor);
        armDamage.StructureDamage.ShouldBe(leftArm.CurrentStructure);
        armDamage.IsLocationDestroyed.ShouldBeTrue();
        
        // Second location (LeftTorso) should receive excess damage
        var torsoDamage = result[1];
        torsoDamage.Location.ShouldBe(PartLocation.LeftTorso);
        torsoDamage.ArmorDamage.ShouldBe(5);
        torsoDamage.StructureDamage.ShouldBe(0);
        torsoDamage.IsLocationDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void CalculateStructureDamage_RearHitExceedsRearArmor_DamagesStructure()
    {
        // Arrange
        var unit = CreateTestMech();
        var centerTorso = unit.Parts[PartLocation.CenterTorso] as Torso;
        centerTorso.ShouldNotBeNull();
        
        var totalDamage = centerTorso.CurrentRearArmor + 2; // Exceed rear armor by 2
        
        // Act
        var result = _sut.CalculateStructureDamage(unit, PartLocation.CenterTorso, totalDamage, HitDirection.Rear);
        
        // Assert
        result.ShouldHaveSingleItem();
        var damageData = result[0];
        damageData.Location.ShouldBe(PartLocation.CenterTorso);
        damageData.ArmorDamage.ShouldBe(centerTorso.CurrentRearArmor);
        damageData.IsRearArmor.ShouldBeTrue();
        damageData.StructureDamage.ShouldBe(2);
        damageData.IsLocationDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void CalculateStructureDamage_MaximumDamage_HandlesCorrectly()
    {
        // Arrange
        var unit = CreateTestMech();
        var centerTorso = unit.Parts[PartLocation.CenterTorso];
        var maximumDamage = int.MaxValue;
        
        // Act
        var result = _sut.CalculateStructureDamage(unit, PartLocation.CenterTorso, maximumDamage, HitDirection.Front);
        
        // Assert
        result.ShouldNotBeEmpty();
        var damageData = result[0];
        damageData.Location.ShouldBe(PartLocation.CenterTorso);
        damageData.ArmorDamage.ShouldBe(centerTorso.CurrentArmor);
        damageData.StructureDamage.ShouldBe(centerTorso.CurrentStructure);
        damageData.IsLocationDestroyed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(HitDirection.Front)]
    [InlineData(HitDirection.Rear)]
    [InlineData(HitDirection.Left)]
    [InlineData(HitDirection.Right)]
    public void CalculateStructureDamage_AllHitDirections_WorksCorrectly(HitDirection direction)
    {
        // Arrange
        var unit = CreateTestMech();
        var damage = 5;
        
        // Act
        var result = _sut.CalculateStructureDamage(unit, PartLocation.CenterTorso, damage, direction);
        
        // Assert
        result.ShouldHaveSingleItem();
        var damageData = result[0];
        damageData.Location.ShouldBe(PartLocation.CenterTorso);
        damageData.ArmorDamage.ShouldBe(damage);
        damageData.StructureDamage.ShouldBe(0);
        damageData.IsRearArmor.ShouldBe(direction == HitDirection.Rear);
        damageData.IsLocationDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void CalculateStructureDamage_ChainedTransfer_HandlesMultipleTransfers()
    {
        // Arrange
        var unit = CreateTestMech();
        var leftArm = unit.Parts[PartLocation.LeftArm];
        var leftTorso = unit.Parts[PartLocation.LeftTorso];

        // Calculate damage to destroy left arm, left torso, and damage center torso
        var totalDamage = leftArm.CurrentArmor + leftArm.CurrentStructure +
                         leftTorso.CurrentArmor + leftTorso.CurrentStructure + 3;
        
        // Act
        var result = _sut.CalculateStructureDamage(unit, PartLocation.LeftArm, totalDamage, HitDirection.Front);
        
        // Assert
        result.Count.ShouldBe(3);
        
        // LeftArm should be destroyed
        result[0].Location.ShouldBe(PartLocation.LeftArm);
        result[0].IsLocationDestroyed.ShouldBeTrue();
        
        // LeftTorso should be destroyed
        result[1].Location.ShouldBe(PartLocation.LeftTorso);
        result[1].IsLocationDestroyed.ShouldBeTrue();
        
        // CenterTorso should receive excess damage
        result[2].Location.ShouldBe(PartLocation.CenterTorso);
        result[2].ArmorDamage.ShouldBe(3);
        result[2].StructureDamage.ShouldBe(0);
        result[2].IsLocationDestroyed.ShouldBeFalse();
    }
    
    [Fact]
    public void CalculateExplosionDamage_WithZeroDamage_ReturnsEmptyList()
    {
        // Arrange
        var unit = CreateTestMech();

        // Act
        var result = _sut.CalculateExplosionDamage(unit, PartLocation.CenterTorso, 0);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void CalculateExplosionDamage_WithNegativeDamage_ReturnsEmptyList()
    {
        // Arrange
        var unit = CreateTestMech();

        // Act
        var result = _sut.CalculateExplosionDamage(unit, PartLocation.CenterTorso, -5);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void CalculateExplosionDamage_WithInvalidLocation_ReturnsEmptyList()
    {
        // Arrange
        var unit = CreateTestMech();

        // Act
        var result = _sut.CalculateExplosionDamage(unit, (PartLocation)999, 10);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void CalculateExplosionDamage_BypassesArmorCompletely_DamagesStructureDirectly()
    {
        // Arrange
        var unit = CreateTestMech();
        const int explosionDamage = 5; // Less than available structure

        // Act
        var result = _sut.CalculateExplosionDamage(unit, PartLocation.CenterTorso, explosionDamage);

        // Assert
        result.ShouldHaveSingleItem();
        var damageData = result[0];
        damageData.Location.ShouldBe(PartLocation.CenterTorso);
        damageData.ArmorDamage.ShouldBe(0); // Armor should not be damaged
        damageData.StructureDamage.ShouldBe(explosionDamage);
        damageData.IsLocationDestroyed.ShouldBeFalse();
        damageData.IsRearArmor.ShouldBeFalse(); // Explosion doesn't affect rear armor flag
    }

    [Fact]
    public void CalculateExplosionDamage_DestroyLocation_ReturnsCorrectData()
    {
        // Arrange
        var unit = CreateTestMech();
        var centerTorso = unit.Parts[PartLocation.CenterTorso];
        var explosionDamage = centerTorso.CurrentStructure; // Exactly destroy structure

        // Act
        var result = _sut.CalculateExplosionDamage(unit, PartLocation.CenterTorso, explosionDamage);

        // Assert
        result.ShouldHaveSingleItem();
        var damageData = result[0];
        damageData.Location.ShouldBe(PartLocation.CenterTorso);
        damageData.ArmorDamage.ShouldBe(0); // No armor damage
        damageData.StructureDamage.ShouldBe(centerTorso.CurrentStructure);
        damageData.IsLocationDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void CalculateExplosionDamage_ExcessDamageWithTransfer_ReturnsMultipleLocations()
    {
        // Arrange
        var unit = CreateTestMech();
        var leftArm = unit.Parts[PartLocation.LeftArm];

        // Calculate damage to destroy left arm structure and damage left torso structure (but not destroy it)
        // Left arm has 4 structure, left torso has 8 structure, so 4 + 3 = 7 total damage
        var explosionDamage = leftArm.CurrentStructure + 3;

        // Act
        var result = _sut.CalculateExplosionDamage(unit, PartLocation.LeftArm, explosionDamage);

        // Assert
        result.Count.ShouldBe(2);

        // First location (LeftArm) should be destroyed with no armor damage
        var armDamage = result[0];
        armDamage.Location.ShouldBe(PartLocation.LeftArm);
        armDamage.ArmorDamage.ShouldBe(0); // No armor damage from the explosion
        armDamage.StructureDamage.ShouldBe(leftArm.CurrentStructure);
        armDamage.IsLocationDestroyed.ShouldBeTrue();

        // Second location (LeftTorso) should receive excess damage to structure only
        var torsoDamage = result[1];
        torsoDamage.Location.ShouldBe(PartLocation.LeftTorso);
        torsoDamage.ArmorDamage.ShouldBe(0); // No armor damage from the explosion
        torsoDamage.StructureDamage.ShouldBe(3);
        torsoDamage.IsLocationDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void CalculateExplosionDamage_ChainedTransfer_HandlesMultipleTransfers()
    {
        // Arrange
        var unit = CreateTestMech();
        var leftArm = unit.Parts[PartLocation.LeftArm];
        var leftTorso = unit.Parts[PartLocation.LeftTorso];

        // Calculate damage to destroy left arm, left torso, and damage center torso structure
        var explosionDamage = leftArm.CurrentStructure + leftTorso.CurrentStructure + 3;

        // Act
        var result = _sut.CalculateExplosionDamage(unit, PartLocation.LeftArm, explosionDamage);

        // Assert
        result.Count.ShouldBe(3);

        // LeftArm should be destroyed with no armor damage
        result[0].Location.ShouldBe(PartLocation.LeftArm);
        result[0].ArmorDamage.ShouldBe(0);
        result[0].IsLocationDestroyed.ShouldBeTrue();

        // LeftTorso should be destroyed with no armor damage
        result[1].Location.ShouldBe(PartLocation.LeftTorso);
        result[1].ArmorDamage.ShouldBe(0);
        result[1].IsLocationDestroyed.ShouldBeTrue();

        // CenterTorso should receive excess damage to structure only
        result[2].Location.ShouldBe(PartLocation.CenterTorso);
        result[2].ArmorDamage.ShouldBe(0);
        result[2].StructureDamage.ShouldBe(3);
        result[2].IsLocationDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void CalculateExplosionDamage_MaximumDamage_HandlesCorrectly()
    {
        // Arrange
        var unit = CreateTestMech();
        var centerTorso = unit.Parts[PartLocation.CenterTorso];
        var maximumDamage = int.MaxValue;

        // Act
        var result = _sut.CalculateExplosionDamage(unit, PartLocation.CenterTorso, maximumDamage);

        // Assert
        result.ShouldNotBeEmpty();
        var damageData = result[0];
        damageData.Location.ShouldBe(PartLocation.CenterTorso);
        damageData.ArmorDamage.ShouldBe(0); // No armor damage from the explosion
        damageData.StructureDamage.ShouldBe(centerTorso.CurrentStructure);
        damageData.IsLocationDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void CalculateExplosionDamage_ComparedToRegularDamage_BypassesArmor()
    {
        // Arrange
        var unit = CreateTestMech();
        var centerTorso = unit.Parts[PartLocation.CenterTorso];
        var damage = centerTorso.CurrentArmor + 3; // Damage that would normally hit armor first

        // Act
        var regularResult = _sut.CalculateStructureDamage(unit, PartLocation.CenterTorso, damage, HitDirection.Front);
        var explosionResult = _sut.CalculateExplosionDamage(unit, PartLocation.CenterTorso, damage);

        // Assert
        regularResult.ShouldHaveSingleItem();
        explosionResult.ShouldHaveSingleItem();

        // Regular damage should hit armor first, then structure
        var regularDamage = regularResult[0];
        regularDamage.ArmorDamage.ShouldBe(centerTorso.CurrentArmor);
        regularDamage.StructureDamage.ShouldBe(3);

        // Explosion damage should bypass armor completely
        var explosionDamage = explosionResult[0];
        explosionDamage.ArmorDamage.ShouldBe(0);
        explosionDamage.StructureDamage.ShouldBe(Math.Min(damage, centerTorso.CurrentStructure));
    }

    [Fact]
    public void CalculateStructureDamage_OnAlreadyDestroyedLocation_ShouldSkipToTransferLocation()
    {
        // Arrange
        var unit = CreateTestMech();
        var leftArm = unit.Parts[PartLocation.LeftArm];

        // Manually destroy the left arm by setting both armor and structure to 0
        leftArm.ApplyDamage(leftArm.CurrentArmor + leftArm.CurrentStructure, HitDirection.Front);

        // Verify the location is destroyed
        leftArm.CurrentArmor.ShouldBe(0);
        leftArm.CurrentStructure.ShouldBe(0);
        leftArm.IsDestroyed.ShouldBeTrue();

        const int testDamage = 5; 

        // Act - Apply damage to the already destroyed location
        var result = _sut.CalculateStructureDamage(unit, PartLocation.LeftArm, testDamage, HitDirection.Front);

        // Assert - Should skip the destroyed location and go directly to the transfer location (LeftTorso)
        result.ShouldHaveSingleItem();
        var damageData = result[0];
        damageData.Location.ShouldBe(PartLocation.LeftTorso); // Should transfer to LeftTorso
        damageData.ArmorDamage.ShouldBe(testDamage); // All damage should go to the transfer location
        damageData.StructureDamage.ShouldBe(0);
        damageData.IsLocationDestroyed.ShouldBeFalse();

        // Should NOT contain any entry for the destroyed LeftArm location
        result.ShouldNotContain(d => d.Location == PartLocation.LeftArm);
    }

    [Fact]
    public void CalculateExplosionDamage_OnAlreadyDestroyedLocation_ShouldSkipToTransferLocation()
    {
        // Arrange
        var unit = CreateTestMech();
        var leftArm = unit.Parts[PartLocation.LeftArm];
        var leftTorso = unit.Parts[PartLocation.LeftTorso];

        // Manually destroy the left arm by setting structure to 0 (explosion bypasses armor)
        leftArm.ApplyDamage(leftArm.CurrentArmor + leftArm.CurrentStructure, HitDirection.Front);

        // Verify the location is destroyed
        leftArm.CurrentStructure.ShouldBe(0);
        leftArm.IsDestroyed.ShouldBeTrue();

        // Use damage less than the LeftTorso structure to avoid destroying it and causing further transfer
        var testDamage = leftTorso.CurrentStructure - 1;

        // Act - Apply explosion damage to the already destroyed location
        var result = _sut.CalculateExplosionDamage(unit, PartLocation.LeftArm, testDamage);

        // Assert - Should skip the destroyed location and go directly to the transfer location (LeftTorso)
        result.ShouldHaveSingleItem();
        var damageData = result[0];
        damageData.Location.ShouldBe(PartLocation.LeftTorso); // Should transfer to LeftTorso
        damageData.ArmorDamage.ShouldBe(0); // Explosion damage doesn't affect armor
        damageData.StructureDamage.ShouldBe(testDamage); // All damage should go to the transfer location structure
        damageData.IsLocationDestroyed.ShouldBeFalse();

        // Should NOT contain any entry for the destroyed LeftArm location
        result.ShouldNotContain(d => d.Location == PartLocation.LeftArm);
    }
    
    [Fact]
    public void CalculateStructureDamage_WithAccumulatedDamage_ShouldConsiderPreviousClusterDamage()
    {
        // Arrange - Simulate the bug scenario
        // LRM20 (20 missiles, 1 damage each, ClusterSize = 5)
        // Cluster roll: 4 → 9 missiles hit
        // Damage groups: Group 1 (5 missiles = 5 damage), Group 2 (4 missiles = 4 damage)
        // Both groups hit Center Torso
        // Target's CT: 2 armor points, 8 internal structure points

        var unit = CreateTestMech();
        var centerTorso = unit.Parts[PartLocation.CenterTorso];

        // Set CT to have only 2 armor points and 8 structure
        centerTorso.RestoreState(2, 8, false);
        centerTorso.CurrentArmor.ShouldBe(2);
        centerTorso.CurrentStructure.ShouldBe(8);

        // Simulate Group 1: 5 damage
        const int group1Damage = 5;
        var group1Result = _sut.CalculateStructureDamage(unit, PartLocation.CenterTorso, group1Damage, HitDirection.Front);

        // Group 1 should deplete 2 armor and deal 3 structure damage
        group1Result.ShouldHaveSingleItem();
        group1Result[0].ArmorDamage.ShouldBe(2);
        group1Result[0].StructureDamage.ShouldBe(3);

        // Create accumulated hit locations from Group 1
        var accumulatedHitLocations = new List<LocationHitData>
        {
            new(group1Result, [], [], PartLocation.CenterTorso)
        };

        // Simulate Group 2: 4 damage
        // This should see the updated state (0 armor, 5 structure remaining)
        const int group2Damage = 4;
        var group2Result = _sut.CalculateStructureDamage(
            unit,
            PartLocation.CenterTorso,
            group2Damage,
            HitDirection.Front,
            accumulatedHitLocations);

        // Group 2 should deal 0 armor damage (already depleted) and 4 structure damage
        group2Result.ShouldHaveSingleItem();
        group2Result[0].ArmorDamage.ShouldBe(0); // Armor already depleted by Group 1
        group2Result[0].StructureDamage.ShouldBe(4);

        // Total damage should be: 2 armor + 7 structure (not 4 armor + 5 structure)
        var totalArmorDamage = group1Result[0].ArmorDamage + group2Result[0].ArmorDamage;
        var totalStructureDamage = group1Result[0].StructureDamage + group2Result[0].StructureDamage;

        totalArmorDamage.ShouldBe(2); // Should not exceed available armor
        totalStructureDamage.ShouldBe(7);
    }
}

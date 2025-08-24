using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics;

public class StructureDamageCalculatorTests
{
    private readonly StructureDamageCalculator _sut;
    private readonly MechFactory _mechFactory;

    public StructureDamageCalculatorTests()
    {
        _sut = new StructureDamageCalculator();
        
        // Setup rules provider for unit creation
        IRulesProvider rules = new ClassicBattletechRulesProvider();
        
        // Setup localization service for unit creation
        var localizationService = Substitute.For<ILocalizationService>();
        
        // Setup mech factory
        _mechFactory = new MechFactory(rules, localizationService);
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
        var centerTorso = unit.Parts.First(p => p.Location == PartLocation.CenterTorso);
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
        var centerTorso = unit.Parts.First(p => p.Location == PartLocation.CenterTorso);
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
        var leftArm = unit.Parts.First(p => p.Location == PartLocation.LeftArm);

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
        var centerTorso = unit.Parts.First(p => p.Location == PartLocation.CenterTorso) as Torso;
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
        var centerTorso = unit.Parts.First(p => p.Location == PartLocation.CenterTorso);
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
        var leftArm = unit.Parts.First(p => p.Location == PartLocation.LeftArm);
        var leftTorso = unit.Parts.First(p => p.Location == PartLocation.LeftTorso);

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
}

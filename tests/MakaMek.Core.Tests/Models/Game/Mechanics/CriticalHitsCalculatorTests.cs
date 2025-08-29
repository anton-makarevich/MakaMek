using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics;

public class CriticalHitsCalculatorTests
{
    private readonly IDiceRoller _mockDiceRoller;
    private readonly CriticalHitsCalculator _sut;
    private readonly MechFactory _mechFactory;

    public CriticalHitsCalculatorTests()
    {
        // Setup mock dice roller
        _mockDiceRoller = Substitute.For<IDiceRoller>();
        
        // Setup calculator with mock dice roller
        _sut = new CriticalHitsCalculator(_mockDiceRoller);

        // Setup rules provider
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
    public void CalculateCriticalHitsForStructureDamage_WithNoStructureDamage_ReturnsEmptyList()
    {
        // Arrange
        var testUnit = CreateTestMech();
        var structureDamageByLocation = new LocationDamageData(PartLocation.CenterTorso,
            0,
            0,
            false);

        // Act
        var result = _sut.CalculateCriticalHitsForStructureDamage(
            testUnit,
            structureDamageByLocation);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void CalculateCriticalHitsForStructureDamage_WithStructureDamage_ReturnsCorrectData()
    {
        // Arrange
        var testUnit = CreateTestMech();
        var structureDamageByLocation = new LocationDamageData(PartLocation.CenterTorso,
            5,
            5,
            false);

        // Setup dice roller for critical hit checks
        _mockDiceRoller.Roll2D6().Returns(
            [new DiceResult(4), new DiceResult(4)] // Roll of 8 for CenterTorso
        );

        // Setup dice roller for critical hit slots
        _mockDiceRoller.RollD6().Returns(
            new DiceResult(2), // Slot for CenterTorso
            new DiceResult(1), // First slot for LeftArm
            new DiceResult(3)  // Second slot for LeftArm
        );

        // Act
        var result = _sut.CalculateCriticalHitsForStructureDamage(testUnit, structureDamageByLocation);

        // Assert
        result.ShouldNotBeEmpty();

        var centerTorsoResult = result.First(r => r.Location == PartLocation.CenterTorso);
        centerTorsoResult.Roll.ShouldBe([4, 4]);
        centerTorsoResult.NumCriticalHits.ShouldBe(1);
    }

    [Fact]
    public void CalculateCriticalHitsForHeatExplosion_WithValidComponent_ReturnsCorrectData()
    {
        // Arrange
        var testUnit = CreateTestMech();
        var centerTorso = testUnit.Parts.First(p => p.Location == PartLocation.CenterTorso);

        // Add an explodable ammo component
        var ammo = new Ammo(Lrm5.Definition, 24);
        centerTorso.TryAddComponent(ammo, [10]);

        // Setup dice roller for cascading critical hits (if any)
        _mockDiceRoller.Roll2D6().Returns([new DiceResult(3), new DiceResult(3)]);
        _mockDiceRoller.RollD6().Returns(new DiceResult(2));

        // Act
        var result = _sut.CalculateCriticalHitsForHeatExplosion(testUnit, ammo);

        // Assert
        result.ShouldNotBeEmpty();
        result.Count.ShouldBeGreaterThanOrEqualTo(1);

        var explosionResult = result.First();
        explosionResult.Location.ShouldBe(PartLocation.CenterTorso);
        explosionResult.Roll.ShouldBe([]); // No roll for forced critical hit
        explosionResult.NumCriticalHits.ShouldBe(1); // One forced critical hit
        explosionResult.HitComponents.ShouldNotBeNull();
        explosionResult.HitComponents!.Length.ShouldBe(1);
        explosionResult.HitComponents[0].Slot.ShouldBe(10);
        explosionResult.Explosions.ShouldNotBeNull();
        explosionResult.Explosions.Count.ShouldBe(1);
    }

    [Fact]
    public void CalculateCriticalHitsForStructureDamage_WithExplosiveComponents_IncludesExplosionData()
    {
        // Arrange
        var testUnit = CreateTestMech();
        var centerTorso = testUnit.Parts.First(p => p.Location == PartLocation.CenterTorso);

        // Add an explodable ammo component
        var ammo = new Ammo(Lrm5.Definition, 24);
        centerTorso.TryAddComponent(ammo, [10]);

        var structureDamageByLocation = new LocationDamageData(PartLocation.CenterTorso,
            5,
            5,
            false);

        // Setup dice roller to hit the ammo component
        _mockDiceRoller.Roll2D6().Returns([new DiceResult(4), new DiceResult(4)]); // Roll of 8
        _mockDiceRoller.RollD6().Returns(
            new DiceResult(5), 
            new DiceResult(5)  
        );

        // Act
        var result = _sut.CalculateCriticalHitsForStructureDamage(testUnit, structureDamageByLocation);

        // Assert
        result.ShouldNotBeEmpty();
        var centerTorsoResult = result.First(r => r.Location == PartLocation.CenterTorso);
        centerTorsoResult.Explosions.ShouldNotBeNull();
        centerTorsoResult.Explosions.Count.ShouldBe(1);
        centerTorsoResult.Explosions[0].ComponentType.ShouldBe(ammo.ComponentType);
        centerTorsoResult.Explosions[0].Slot.ShouldBe(10);
        centerTorsoResult.Explosions[0].ExplosionDamage.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void CalculateCriticalHitsForStructureDamage_ShouldReturnEmptyList_WhenStructureDamageIsZero()
    {
        // Arrange
        var testUnit = CreateTestMech();
        var structureDamageByLocation = new LocationDamageData(PartLocation.CenterTorso,
            5, // Armor damage
            0, // No structure damage
            false);

        // Act
        var result = _sut.CalculateCriticalHitsForStructureDamage(
            testUnit,
            structureDamageByLocation);

        // Assert
        result.ShouldBeEmpty(); // Should return an empty list when structure damage is 0
        _mockDiceRoller.DidNotReceive().Roll2D6(); // Should not roll dice
    }

    [Fact]
    public void CalculateCriticalHitsForStructureDamage_ShouldReturnEmptyList_WhenStructureDamageIsNegative()
    {
        // Arrange
        var testUnit = CreateTestMech();
        var structureDamageByLocation = new LocationDamageData(PartLocation.CenterTorso,
            5, // Armor damage
            -3, // Negative structure damage
            false);

        // Act
        var result = _sut.CalculateCriticalHitsForStructureDamage(
            testUnit,
            structureDamageByLocation);

        // Assert
        result.ShouldBeEmpty(); // Should return an empty list when structure damage is negative
        _mockDiceRoller.DidNotReceive().Roll2D6(); // Should not roll dice
    }

    [Fact]
    public void CalculateCriticalHitsForHeatExplosion_ShouldReturnEmptyList_WhenComponentHasNoMountedSlots()
    {
        // Arrange
        var testUnit = CreateTestMech();

        // Create an ammo component not mounted to any part (no slots)
        var ammo = new Ammo(Lrm5.Definition, 24);
        // Don't mount it to any part, so MountedAtSlots will be empty

        // Act
        var result = _sut.CalculateCriticalHitsForHeatExplosion(testUnit, ammo);

        // Assert
        result.ShouldBeEmpty(); // Should return an empty list when the component has no mounted slots
        _mockDiceRoller.DidNotReceive().Roll2D6(); // Should not roll dice
    }

    [Fact]
    public void CalculateCriticalHitsForStructureDamage_ShouldReturnEmptyList_WhenPartNotFound()
    {
        // Arrange
        var testUnit = CreateTestMech();
        var structureDamageByLocation = new LocationDamageData((PartLocation)999, // Invalid location
            5, // Armor damage
            3, // Structure damage
            false);

        // Act
        var result = _sut.CalculateCriticalHitsForStructureDamage(
            testUnit,
            structureDamageByLocation);

        // Assert
        result.ShouldBeEmpty(); // Should return an empty list when part is not found
        _mockDiceRoller.DidNotReceive().Roll2D6(); // Should not roll dice
    }

    [Fact]
    public void CalculateCriticalHitsForStructureDamage_ShouldReturnValidData_WhenCalculateCriticalHitsDataReturnsZeroCriticalHits()
    {
        // Arrange 
        var testUnit = CreateTestMech();
        var structureDamageByLocation = new LocationDamageData(PartLocation.CenterTorso,
            5, // Armor damage
            3, // Structure damage
            false);

        // Setup dice roller to return a roll that results in 0 critical hits but valid data
        _mockDiceRoller.Roll2D6().Returns([new DiceResult(1), new DiceResult(1)]); // Roll of 2 (0 critical hits)

        // Act
        var result = _sut.CalculateCriticalHitsForStructureDamage(
            testUnit,
            structureDamageByLocation);

        // Assert
        result.ShouldNotBeEmpty(); // Should return valid data even with 0 critical hits
        result.Count.ShouldBe(1);
        result[0].NumCriticalHits.ShouldBe(0); // Should have 0 critical hits
        _mockDiceRoller.Received(1).Roll2D6(); // Should have rolled dice
    }

    [Fact]
    public void CalculateCriticalHitsForStructureDamage_ShouldReturnEmptyList_WhenPartHasZeroStructure()
    {
        // Arrange - This specifically tests the CurrentStructure > 0 condition in lines 104-105
        var testUnit = CreateTestMech();
        var centerTorso = testUnit.Parts.First(p => p.Location == PartLocation.CenterTorso);

        // Destroy the center torso by applying enough damage to reduce structure to 0
        centerTorso.ApplyDamage(centerTorso.CurrentArmor + centerTorso.CurrentStructure, HitDirection.Front);

        var structureDamageByLocation = new LocationDamageData(PartLocation.CenterTorso,
            5, // Armor damage
            3, // Structure damage
            false);

        // Act
        var result = _sut.CalculateCriticalHitsForStructureDamage(
            testUnit,
            structureDamageByLocation);

        // Assert
        result.ShouldBeEmpty(); // Should return an empty list when structure is exactly 0
        _mockDiceRoller.DidNotReceive().Roll2D6(); // Should not roll dice when structure is 0
    }
}
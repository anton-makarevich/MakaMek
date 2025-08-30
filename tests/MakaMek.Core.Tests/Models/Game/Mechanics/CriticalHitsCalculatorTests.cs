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
    private readonly IDiceRoller _mockDiceRoller = Substitute.For<IDiceRoller>();
    private readonly IDamageTransferCalculator _mockDamageTransferCalculator = Substitute.For<IDamageTransferCalculator>();
    private readonly CriticalHitsCalculator _sut;
    private readonly MechFactory _mechFactory;

    public CriticalHitsCalculatorTests()
    {
        // Setup calculator with mock dice roller
        _sut = new CriticalHitsCalculator(_mockDiceRoller, _mockDamageTransferCalculator);

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
        result.ShouldBeNull();
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
        result.ShouldNotBeNull();
        result.Roll.ShouldBe([4, 4]);
        result.NumCriticalHits.ShouldBe(1);
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
        
        // Setup structure damage calculator to return damage from explosion
        _mockDamageTransferCalculator.CalculateExplosionDamage(
                Arg.Any<Unit>(),
                Arg.Is<PartLocation>(l => l == PartLocation.CenterTorso),
                Arg.Is<int>(d => d > 0))
            .Returns([
                new LocationDamageData(PartLocation.CenterTorso, 0, 5, false)
            ]);

        // Act
        var result = _sut.CalculateCriticalHitsForHeatExplosion(testUnit, ammo);

        // Assert
        result.ShouldNotBeNull();
        result.Location.ShouldBe(PartLocation.CenterTorso);
        result.Roll.ShouldBe([]); // No roll for forced critical hit
        result.NumCriticalHits.ShouldBe(1); // One forced critical hit
        result.HitComponents.ShouldNotBeNull();
        result.HitComponents!.Length.ShouldBe(1);
        result.HitComponents[0].Slot.ShouldBe(10);
        result.Explosions.ShouldNotBeNull();
        result.Explosions.Count.ShouldBe(1);
        result.Explosions[0].Location.ShouldBe(PartLocation.CenterTorso);
        result.Explosions[0].StructureDamage.ShouldBe(5);
    }

    [Fact]
    public void CalculateCriticalHitsForStructureDamage_WithExplosiveComponent_IncludesExplosionData()
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
        
        // Setup structure damage calculator to return damage from explosion
        _mockDamageTransferCalculator.CalculateExplosionDamage(
                Arg.Any<Unit>(),
                Arg.Is<PartLocation>(l => l == PartLocation.CenterTorso),
                Arg.Is<int>(d => d > 0))
            .Returns([
                new LocationDamageData(PartLocation.CenterTorso, 0, 5, false)
            ]);

        // Act
        var result = _sut.CalculateCriticalHitsForStructureDamage(testUnit, structureDamageByLocation);

        // Assert
        result.ShouldNotBeNull();
        result.Explosions.ShouldNotBeNull();
        result.Explosions.Count.ShouldBe(1);
        result.Explosions[0].Location.ShouldBe(PartLocation.CenterTorso);
        result.Explosions[0].StructureDamage.ShouldBe(5);
    }
    
    [Fact]
    public void CalculateCriticalHitsForStructureDamage_WithExplosiveComponents_IncludesExplosionDataForAllOfThem()
    {
        // Arrange
        var testUnit = CreateTestMech();
        var leftArm = testUnit.Parts.First(p => p.Location == PartLocation.LeftArm);

        // Add an explodable ammo component
        var ammo = new Ammo(Lrm5.Definition, 24);
        leftArm.TryAddComponent(ammo).ShouldBeTrue();
        var ammo2 = new Ammo(Lrm5.Definition, 24);
        leftArm.TryAddComponent(ammo2).ShouldBeTrue();

        var structureDamageByLocation = new LocationDamageData(PartLocation.LeftArm,
            5,
            5,
            false);

        // Setup dice roller to hit the ammo component
        _mockDiceRoller.Roll2D6().Returns([new DiceResult(5), new DiceResult(5)]); // 2 crits
        _mockDiceRoller.RollD6().Returns(
            new DiceResult(4), 
            new DiceResult(3)
        );
        
        // Setup structure damage calculator to return damage from explosion
        _mockDamageTransferCalculator.CalculateExplosionDamage(
                Arg.Any<Unit>(),
                Arg.Is<PartLocation>(l => l == PartLocation.LeftArm),
                Arg.Is<int>(d => d > 0))
            .Returns([
                new LocationDamageData(PartLocation.LeftArm, 0, 5, false)
            ]);

        // Act
        var result = _sut.CalculateCriticalHitsForStructureDamage(testUnit, structureDamageByLocation);

        // Assert
        result.ShouldNotBeNull();
        result.Explosions.ShouldNotBeNull();
        result.Explosions.Count.ShouldBe(2);
        result.Explosions[0].Location.ShouldBe(PartLocation.LeftArm);
        result.Explosions[0].StructureDamage.ShouldBe(5);
        result.Explosions[1].Location.ShouldBe(PartLocation.LeftArm);
        result.Explosions[1].StructureDamage.ShouldBe(5);
    }
    
    [Fact]
    public void CalculateCriticalHitsForStructureDamage_WithExplosiveComponents_IncludesExplosionDataForAllAffectedLocations()
    {
        // Arrange
        var testUnit = CreateTestMech();
        var leftArm = testUnit.Parts.First(p => p.Location == PartLocation.LeftArm);

        // Add an explodable ammo component
        var ammo = new Ammo(Lrm5.Definition, 24);
        leftArm.TryAddComponent(ammo).ShouldBeTrue();
        var ammo2 = new Ammo(Lrm5.Definition, 24);
        leftArm.TryAddComponent(ammo2).ShouldBeTrue();

        var structureDamageByLocation = new LocationDamageData(PartLocation.LeftArm,
            5,
            5,
            false);

        // Setup dice roller to hit the ammo component
        _mockDiceRoller.Roll2D6().Returns([new DiceResult(5), new DiceResult(5)]); // 2 crits
        _mockDiceRoller.RollD6().Returns(
            new DiceResult(4), 
            new DiceResult(3)
        );
        
        // Setup structure damage calculator to return damage from explosion
        _mockDamageTransferCalculator.CalculateExplosionDamage(
                Arg.Any<Unit>(),
                Arg.Is<PartLocation>(l => l == PartLocation.LeftArm),
                Arg.Is<int>(d => d > 0))
            .Returns([
                new LocationDamageData(PartLocation.LeftArm, 0, 5, false),
                new LocationDamageData(PartLocation.LeftTorso, 0, 5, false)
            ]);

        // Act
        var result = _sut.CalculateCriticalHitsForStructureDamage(testUnit, structureDamageByLocation);

        // Assert
        result.ShouldNotBeNull();
        result.Explosions.ShouldNotBeNull();
        result.Explosions.Count.ShouldBe(4);
        result.Explosions[0].Location.ShouldBe(PartLocation.LeftArm);
        result.Explosions[0].StructureDamage.ShouldBe(5);
        result.Explosions[1].Location.ShouldBe(PartLocation.LeftTorso);
        result.Explosions[1].StructureDamage.ShouldBe(5);
        result.Explosions[2].Location.ShouldBe(PartLocation.LeftArm);
        result.Explosions[2].StructureDamage.ShouldBe(5);
        result.Explosions[3].Location.ShouldBe(PartLocation.LeftTorso);
        result.Explosions[3].StructureDamage.ShouldBe(5);
    }

    [Fact]
    public void CalculateCriticalHitsForStructureDamage_ShouldReturnNull_WhenStructureDamageIsZero()
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
        result.ShouldBeNull(); // Should return null when structure damage is 0
        _mockDiceRoller.DidNotReceive().Roll2D6(); // Should not roll dice
    }

    [Fact]
    public void CalculateCriticalHitsForStructureDamage_ShouldReturnNull_WhenStructureDamageIsNegative()
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
        result.ShouldBeNull(); // Should return null when structure damage is negative
        _mockDiceRoller.DidNotReceive().Roll2D6(); // Should not roll dice
    }

    [Fact]
    public void CalculateCriticalHitsForHeatExplosion_ShouldReturnNull_WhenComponentHasNoMountedSlots()
    {
        // Arrange
        var testUnit = CreateTestMech();

        // Create an ammo component not mounted to any part (no slots)
        var ammo = new Ammo(Lrm5.Definition, 24);
        // Don't mount it to any part, so MountedAtSlots will be empty

        // Act
        var result = _sut.CalculateCriticalHitsForHeatExplosion(testUnit, ammo);

        // Assert
        result.ShouldBeNull(); // Should return null when the component has no mounted slots
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
        result.ShouldNotBeNull(); // Should return valid data even with 0 critical hits
        result.NumCriticalHits.ShouldBe(0); // Should have 0 critical hits
        _mockDiceRoller.Received(1).Roll2D6(); // Should have rolled dice
    }

    [Fact]
    public void CalculateCriticalHitsForStructureDamage_ShouldReturnNull_WhenPartHasZeroStructure()
    {
        // Arrange - This specifically tests the CurrentStructure > 0 condition when the part is already destroyed
        // There exceptions to this rule that should be implemented later
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
        result.ShouldBeNull(); // Should return null when the structure is exactly 0
        _mockDiceRoller.DidNotReceive().Roll2D6(); // Should not roll dice when structure is 0
    }
}
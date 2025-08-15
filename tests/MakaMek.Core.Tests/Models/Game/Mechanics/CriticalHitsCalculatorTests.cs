using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Sanet.MakaMek.Core.Models.Units.Mechs;
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

    private static List<UnitPart> CreateCustomPartsData(int armorValue, int structureValue)
    {
        return
        [
            new Head("Head", armorValue, structureValue),
            new CenterTorso("CenterTorso", armorValue, armorValue, structureValue),
            new SideTorso("LeftTorso", PartLocation.LeftTorso, armorValue, armorValue, structureValue),
            new SideTorso("RightTorso", PartLocation.RightTorso, armorValue, armorValue, structureValue),
            new Arm("RightArm", PartLocation.RightArm, armorValue, structureValue),
            new Arm("LeftArm", PartLocation.LeftArm, armorValue, structureValue),
            new Leg("RightLeg", PartLocation.RightLeg, armorValue, structureValue),
            new Leg("LeftLeg", PartLocation.LeftLeg, armorValue, structureValue)
        ];
    }

    private Unit CreateCustomMech(int armorValue, int structureValue)
    {
        return new Mech("TestMech", "TST-1A", 50, 4, CreateCustomPartsData(armorValue, structureValue));
    }

    [Fact]
    public void CalculateCriticalHits_WhenNoStructuralDamage_ReturnsEmptyList()
    {
        // Arrange
        var testUnit = CreateTestMech();
        const PartLocation location = PartLocation.CenterTorso;
        var part = testUnit.Parts.First(p => p.Location == location);
        var damage = part.CurrentArmor - 1; // Damage less than armor

        // Act
        var result = _sut.CalculateCriticalHits(testUnit, location, damage);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void CalculateCriticalHits_WhenStructuralDamageOccurs_ReturnsCriticalHits()
    {
        // Arrange
        var testUnit = CreateTestMech();
        const PartLocation location = PartLocation.CenterTorso;
        var part = testUnit.Parts.First(p => p.Location == location);
        var damage = part.CurrentArmor + 1; // Just enough to cause 1 point of structural damage
        
        // Setup dice roller for critical hit check (2d6 roll of 12)
        _mockDiceRoller.Roll2D6().Returns([new DiceResult(6), new DiceResult(2)]);
        
        // Setup dice roller for critical hit slots
        _mockDiceRoller.RollD6().Returns(
            new DiceResult(3)
        );
    
        // Act
        var result = _sut.CalculateCriticalHits(testUnit, location, damage);
    
        // Assert
        result.ShouldNotBeEmpty();
        result.Count.ShouldBe(1);
        result[0].Location.ShouldBe(location);
        result[0].Roll.ShouldBe(8); // 6 + 2
        result[0].NumCriticalHits.ShouldBe(1); // Roll of 12 gives 3 critical hits
    }

    [Fact]
    public void CalculateCriticalHits_WhenLocationDestroyed_PropagatesDamageToNextLocation()
    {
        // Arrange
        var testUnit = CreateCustomMech(5, 5); // Low armor and structure to ensure transfer
        const PartLocation initialLocation = PartLocation.RightArm;
        const int totalDamage = 16; // Enough to destroy RightArm and damage RightTorso
        
        // Setup dice rolls for critical hit checks
        _mockDiceRoller.Roll2D6().Returns(
            // First call for RightArm (roll of 8)
            [new DiceResult(5), new DiceResult(3)],
            // Second call for RightTorso (roll of 10)
            [new DiceResult(6), new DiceResult(4)]
        );
        
        // Setup rolls for critical hit slots
        _mockDiceRoller.RollD6().Returns(
            new DiceResult(3), // Slot for RightArm
            new DiceResult(2), // First slot for RightTorso
            new DiceResult(4)  // Second slot for RightTorso
        );

        // Act
        var result = _sut.CalculateCriticalHits(testUnit, initialLocation, totalDamage);

        // Assert
        result.ShouldNotBeEmpty();
        result.Count.ShouldBe(2); // Should have critical hits for both locations
        
        result[0].Location.ShouldBe(initialLocation);
        result[0].Roll.ShouldBe(8); // 5 + 3
        result[0].NumCriticalHits.ShouldBe(1); // Roll of 8 gives 1 critical hit
        
        result[1].Location.ShouldBe(PartLocation.RightTorso); // Transfer location
        result[1].Roll.ShouldBe(10); // 6 + 4
        result[1].NumCriticalHits.ShouldBe(2); // Roll of 10 gives 2 critical hits
    }

    [Fact]
    public void CalculateCriticalHits_WithMultipleTransfers_CalculatesAllCriticalHits()
    {
        // Arrange
        var testUnit = CreateCustomMech(3, 3); // Very low armor and structure to ensure multiple transfers
        const PartLocation initialLocation = PartLocation.RightArm;
        const int totalDamage = 26; // Enough to destroy multiple locations
        
        var rightTorso = testUnit.Parts.First(p => p.Location == PartLocation.RightTorso);
        rightTorso.TryAddComponent(new MediumLaser(),[0]);
        
        // Setup dice rolls for critical hit checks
        _mockDiceRoller.Roll2D6().Returns(
            // RightArm (roll of 8)
            [new DiceResult(4), new DiceResult(4)],
            // RightTorso (roll of 8)
            [new DiceResult(5), new DiceResult(3)],
            // CenterTorso (roll of 8)
            [new DiceResult(6), new DiceResult(2)]
        );
        
        // Setup rolls for critical hit slots
        _mockDiceRoller.RollD6().Returns(
            // For CenterTorso 
            new DiceResult(4)
        );
    
        // Act
        var result = _sut.CalculateCriticalHits(testUnit, initialLocation, totalDamage);
    
        // Assert
        result.ShouldNotBeEmpty();
        result.Count.ShouldBe(3); // Should have critical hits for all three locations
        
        result[0].Location.ShouldBe(initialLocation);
        result[0].Roll.ShouldBe(8); // 4 + 4
        result[0].NumCriticalHits.ShouldBe(1); // Roll of 8 gives 1 critical hit
        
        result[1].Location.ShouldBe(PartLocation.RightTorso); // First transfer
        result[1].Roll.ShouldBe(8); // 5 + 3
        result[1].NumCriticalHits.ShouldBe(1); // Roll of 10 gives 2 critical hits
        
        result[2].Location.ShouldBe(PartLocation.CenterTorso); // Second transfer
        result[2].Roll.ShouldBe(8); // 6 + 2
        result[2].NumCriticalHits.ShouldBe(1); // Roll of 12 gives 3 critical hits
    }

    [Fact]
    public void CalculateCriticalHits_WhenPartNotFound_ReturnsEmptyList()
    {
        // Arrange
        var testUnit = CreateTestMech();
        const PartLocation nonExistentLocation = (PartLocation)999; // Invalid location
        const int damage = 10;

        // Act
        var result = _sut.CalculateCriticalHits(testUnit, nonExistentLocation, damage);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void CalculateCriticalHits_WithLowDiceRoll_DoesNotGenerateCriticalHits()
    {
        // Arrange
        var testUnit = CreateTestMech();
        const PartLocation location = PartLocation.CenterTorso;
        var part = testUnit.Parts.First(p => p.Location == location);
        var damage = part.CurrentArmor + 1; // Just enough to cause 1 point of structural damage
        
        // Set up the dice roller to return a low value (roll of 2 - no critical hit)
        _mockDiceRoller.Roll2D6().Returns([new DiceResult(1), new DiceResult(1)]);

        // Act
        var result = _sut.CalculateCriticalHits(testUnit, location, damage);

        // Assert
        result.Count.ShouldBe(1);
        result[0].NumCriticalHits.ShouldBe(0); // No criticals with a roll of 2
    }

    [Fact]
    public void CalculateCriticalHits_WithBlownOffLocation_SetsIsBlownOffFlag()
    {
        // Arrange
        var testUnit = CreateCustomMech(3, 3);
        const PartLocation location = PartLocation.Head; // Head is easier to blow off
        const int damage = 10; // Enough to destroy the head
        
        // Setup dice rolls for critical hit check - high enough to blow off the location
        _mockDiceRoller.Roll2D6().Returns([new DiceResult(6), new DiceResult(6)]);
        
        // Act
        var result = _sut.CalculateCriticalHits(testUnit, location, damage);

        // Assert
        result.ShouldNotBeEmpty();
        result.Count.ShouldBe(1);
        result[0].Location.ShouldBe(location);
        result[0].IsBlownOff.ShouldBeTrue(); // Location should be blown off with a roll of 12
    }

    [Fact]
    public void CalculateCriticalHits_WithExplodingAmmo_AddsDamageAndCausesAdditionalCriticalHits()
    {
        // Arrange
        var testUnit = CreateCustomMech(5, 10); // Higher structure to ensure it doesn't transfer
        const PartLocation location = PartLocation.RightTorso;
        var part = testUnit.Parts.First(p => p.Location == location);
        
        // Add ammo to the location
        var ammo = new Ammo(Ac5.Definition, 1);
        part.TryAddComponent(ammo, [0]);
        
        const int damage = 6; // Enough to cause structural damage but not destroy the location
        
        // Setup dice rolls for initial critical hit check
        _mockDiceRoller.Roll2D6().Returns(
            // First roll for initial damage (8 = 1 crit)
            [new DiceResult(4), new DiceResult(4)],
            // Second roll for explosion damage (8 = 1 crit)
            [new DiceResult(5), new DiceResult(3)]
        );
        
        // Act
        var result = _sut.CalculateCriticalHits(testUnit, location, damage);
        
        // Assert
        result.ShouldNotBeEmpty();
        result.Count.ShouldBe(2); // Should have two sets of critical hits (initial and explosion)
        
        // First critical hit set (from initial damage)
        result[0].Location.ShouldBe(location);
        result[0].Roll.ShouldBe(8); // 4 + 4
        result[0].NumCriticalHits.ShouldBe(1); // Roll of 8 gives 1 critical hit
        result[0].HitComponents!.FirstOrDefault(c=>c.Slot == 0).ShouldNotBeNull(); // Should hit slot 0 where ammo is
        
        // Second critical hit set (from explosion)
        result[1].Location.ShouldBe(location);
        result[1].Roll.ShouldBe(8); // 5 + 3
        result[1].NumCriticalHits.ShouldBe(1); // Roll of 8 gives 1 critical hit
        result[1].HitComponents!.FirstOrDefault(c=>c.Slot == 0).ShouldNotBeNull(); // Should hit slot 0 where ammo is
    }
    
    [Fact]
    public void CalculateCriticalHits_WithCascadingExplosions_HandlesMultipleExplosions()
    {
        // Arrange
        var testUnit = CreateCustomMech(5, 15); // Higher structure to ensure it doesn't transfer
        const PartLocation location = PartLocation.RightTorso;
        var part = testUnit.Parts.First(p => p.Location == location);
        
        // Add multiple ammo boxes to the location
        var ammo1 = new Ammo(Srm2.Definition, 1);
        var ammo2 = new Ammo(Lrm10.Definition, 1);
        
        part.TryAddComponent(ammo1, [0]);
        part.TryAddComponent(ammo2, [1]);
        
        const int damage = 6; // Enough to cause structural damage but not destroy the location by itself
        
        // Setup dice rolls for critical hit checks
        _mockDiceRoller.Roll2D6().Returns(
            // First roll for initial damage (8 = 1 crit)
            [new DiceResult(4), new DiceResult(4)],
            // Second roll for first explosion (8 = 1 crits)
            [new DiceResult(5), new DiceResult(3)],
            // Third roll for second explosion (8 = 1 crit)
            [new DiceResult(4), new DiceResult(4)]
        );
        
        // Setup rolls for critical hit slots
        _mockDiceRoller.RollD6().Returns(
            // Initial critical hit - hits first ammo
            new DiceResult(1), // Upper group
            new DiceResult(1), // Slot 0 where we placed ammo1
            
            // First explosion critical hits - one hits second ammo
            new DiceResult(1), // Upper group
            new DiceResult(2), // Slot 1 where we placed ammo2
            
            // Second explosion critical hit - doesn't hit third ammo
            new DiceResult(1), // Upper group
            new DiceResult(1)  // Slot 0 again, already exploded
        );
        
        // Act
        var result = _sut.CalculateCriticalHits(testUnit, location, damage);
        
        // Assert
        result.ShouldNotBeEmpty();
        result.Count.ShouldBe(3); // Should have three sets of critical hits (initial + 2 explosions)
        
        // First critical hit set (from initial damage)
        result[0].Location.ShouldBe(location);
        result[0].NumCriticalHits.ShouldBe(1);
        result[0].HitComponents!.FirstOrDefault(c=>c.Slot == 0).ShouldNotBeNull(); // Should hit slot 0 where ammo1 is
        
        // Second critical hit set (from first explosion)
        result[1].Location.ShouldBe(location);
        result[1].NumCriticalHits.ShouldBe(1);
        result[1].HitComponents!.FirstOrDefault(c=>c.Slot == 1).ShouldNotBeNull(); // Should hit slot 1 where ammo2 is
        
        // Third critical hit set (from the second explosion)
        result[2].Location.ShouldBe(location);
        result[2].NumCriticalHits.ShouldBe(1);
    }
    
    [Fact]
    public void CalculateCriticalHits_WithExplosionAndTransfer_PropagatesToNextLocation()
    {
        // Arrange
        var testUnit = CreateCustomMech(5, 5); // Low structure to ensure transfer
        const PartLocation initialLocation = PartLocation.RightTorso;
        var armPart = testUnit.Parts.First(p => p.Location == initialLocation);
        
        // Add ammo to the arm
        var ammo = new Ammo(Ac5.Definition, 11);
        armPart.TryAddComponent(ammo, [0]);
        
        const int damage = 6; // Enough to cause structural damage but not destroy the location by itself
        
        // Setup dice rolls for critical hit checks
        _mockDiceRoller.Roll2D6().Returns(
            // First roll for initial damage (8 = 1 crit)
            [new DiceResult(4), new DiceResult(4)],
            // Second roll for explosion in RightArm
            [new DiceResult(5), new DiceResult(5)],
            // Third roll for transfer to RightTorso
            [new DiceResult(5), new DiceResult(3)]
        );
        
        // Setup rolls for critical hit slots
        _mockDiceRoller.RollD6().Returns(
            // Initial critical hit - hits first ammo
            new DiceResult(1), // Upper group
            new DiceResult(5)
        );
        
        // Act
        var result = _sut.CalculateCriticalHits(testUnit, initialLocation, damage);
        
        // Assert
        result.ShouldNotBeEmpty();
        result.Count.ShouldBe(3); // Should have multiple critical hit sets: 2 in initial location (damage and explosion) and one in transfer
        
        // First critical hit set (from initial damage)
        result[0].Location.ShouldBe(initialLocation);
        result[0].NumCriticalHits.ShouldBe(1);
        result[0].HitComponents!.FirstOrDefault(c=>c.Slot == 0).ShouldNotBeNull(); // Should hit slot 0 where ammo is
        
        // The last critical hit set should be in the transfer location
        var lastResult = result[^1];
        lastResult.Location.ShouldBe(PartLocation.CenterTorso);
    }
}
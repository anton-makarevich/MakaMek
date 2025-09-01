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
        // Setup calculator with mock dice roller and damage transfer calculator
        _sut = new CriticalHitsCalculator(_mockDiceRoller, _mockDamageTransferCalculator);

        // Setup rules provider
        IRulesProvider rules = new ClassicBattletechRulesProvider();

        // Setup localization service for unit creation
        var localizationService = Substitute.For<ILocalizationService>();

        // Setup mech factory
        _mechFactory = new MechFactory(rules, localizationService);
    }
    
    // Helper methods for creating test data
    private Unit CreateTestMech()
    {
        var mechData = MechFactoryTests.CreateDummyMechData();
        return _mechFactory.Create(mechData);
    }
    
    private static LocationDamageData CreateLocationDamageData(PartLocation location, int armorDamage,
        int structureDamage)
    {
        return new LocationDamageData(location, armorDamage, structureDamage, false);
    }

    [Fact]
    public void CalculateCriticalHitsForHeatExplosion_WithValidComponent_ReturnsCorrectData()
    {
        // Arrange
        var testUnit = CreateTestMech();
        var centerTorso = testUnit.Parts.First(p => p.Location == PartLocation.CenterTorso);

        // Add an explodable ammo component
        var ammo = new Ammo(Lrm5.Definition, 24);
        centerTorso.TryAddComponent(ammo, [10]).ShouldBeTrue();

        // Setup structure damage calculator to return damage from explosion
        _mockDamageTransferCalculator.CalculateExplosionDamage(
                Arg.Any<Unit>(),
                Arg.Is<PartLocation>(l => l == PartLocation.CenterTorso),
                Arg.Is<int>(d => d > 0))
            .Returns([
                new LocationDamageData(PartLocation.CenterTorso, 0, 0, false) // no damage to not chain calculations
            ]);

        // Act
        var result = _sut.CalculateCriticalHitsForHeatExplosion(testUnit, ammo);

        // Assert
        result.ShouldNotBeEmpty();
        var locationData = result[0];
        locationData.Location.ShouldBe(PartLocation.CenterTorso);
        locationData.Roll.ShouldBeEmpty(); // No roll for forced critical hit
        locationData.NumCriticalHits.ShouldBe(1); // One forced critical hit
        locationData.HitComponents.ShouldNotBeNull();
        locationData.HitComponents!.Length.ShouldBe(1);
        locationData.HitComponents[0].Slot.ShouldBe(10);
        locationData.ExplosionsDamage.ShouldNotBeNull();
        locationData.ExplosionsDamage.Count.ShouldBe(1);
        locationData.ExplosionsDamage[0].Location.ShouldBe(PartLocation.CenterTorso);
        locationData.ExplosionsDamage[0].StructureDamage.ShouldBe(0);
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
        result.ShouldBeEmpty(); // Should return null when the component has no mounted slots
        _mockDiceRoller.DidNotReceive().Roll2D6(); // Should not roll dice
    }


    
    [Fact]
    public void CalculateCriticalHitsForHeatExplosion_ShouldReturnEmptyList_WhenExplosionDamageIsZero()
    {
        // Arrange
        var testUnit = CreateTestMech();
        var centerTorso = testUnit.Parts.First(p => p.Location == PartLocation.CenterTorso);

        // Ammo with 0 rounds -> zero explosion damage
        var ammo = new Ammo(Lrm5.Definition, 0);
        centerTorso.TryAddComponent(ammo).ShouldBeTrue();

        // Act
        var result = _sut.CalculateCriticalHitsForHeatExplosion(testUnit, ammo);

        // Assert
        result.ShouldBeEmpty();
        _mockDamageTransferCalculator.DidNotReceive().CalculateExplosionDamage(Arg.Any<Unit>(), Arg.Any<PartLocation>(), Arg.Any<int>());
        _mockDiceRoller.DidNotReceive().Roll2D6();
    }

    // Tests moved from WeaponAttackResolutionPhaseTests - testing critical hits logic directly

    [Fact]
    public void CalculateCriticalHits_ShouldReturnNull_WhenOnlyArmorDamage()
    {
        // Arrange
        var testUnit = CreateTestMech();
        List<LocationDamageData> hitLocationsData = [
            CreateLocationDamageData(PartLocation.CenterTorso, 5, 0) // Only armor damage
        ];

        // Act
        var result = _sut.CalculateAndApplyCriticalHits(testUnit, hitLocationsData);

        // Assert
        result.ShouldBeNull(); // No critical hits when no structure damage
        _mockDiceRoller.DidNotReceive().Roll2D6(); // Should not roll dice
    }

    [Fact]
    public void CalculateCriticalHits_ShouldReturnCommand_WhenStructureDamageOccurs()
    {
        // Arrange
        var testUnit = CreateTestMech();
        List<LocationDamageData> hitLocationsData = [
            CreateLocationDamageData(PartLocation.CenterTorso, 3, 2) // Structure damage
        ];

        // Setup dice roller for critical hit checks
        _mockDiceRoller.Roll2D6().Returns([new DiceResult(4), new DiceResult(4)]); // Roll of 8
        _mockDiceRoller.RollD6().Returns(new DiceResult(2)); // Slot

        // Act
        var result = _sut.CalculateAndApplyCriticalHits(testUnit, hitLocationsData);

        // Assert
        result.ShouldNotBeNull();
        result.TargetId.ShouldBe(testUnit.Id);
        result.CriticalHits.Count.ShouldBe(1);
        result.CriticalHits[0].Location.ShouldBe(PartLocation.CenterTorso);
        result.CriticalHits[0].NumCriticalHits.ShouldBe(1);
        _mockDiceRoller.Received(1).Roll2D6();
    }

    [Fact]
    public void CalculateCriticalHits_ShouldCalculateForMultipleLocations_WhenMultipleStructureDamage()
    {
        // Arrange
        var testUnit = CreateTestMech();
        List<LocationDamageData> hitLocationsData = [
            CreateLocationDamageData(PartLocation.CenterTorso, 3, 2), // Structure damage
            CreateLocationDamageData(PartLocation.LeftArm, 2, 1)     // Structure damage
        ];

        // Setup dice roller for critical hit checks
        _mockDiceRoller.Roll2D6().Returns(
            [new DiceResult(4), new DiceResult(4)], // First location
            [new DiceResult(3), new DiceResult(3)]  // Second location
        );
        _mockDiceRoller.RollD6().Returns(
            new DiceResult(2), // First slot
            new DiceResult(1)  // Second slot
        );

        // Act
        var result = _sut.CalculateAndApplyCriticalHits(testUnit, hitLocationsData);

        // Assert
        result.ShouldNotBeNull();
        result.CriticalHits.Count.ShouldBe(2);
        result.CriticalHits.ShouldContain(ch => ch.Location == PartLocation.CenterTorso);
        result.CriticalHits.ShouldContain(ch => ch.Location == PartLocation.LeftArm);
        _mockDiceRoller.Received(2).Roll2D6();
    }

    [Fact]
    public void CalculateCriticalHits_ShouldNotProcessExplosionDamage_WhenStructureDamageIsZero()
    {
        // Arrange
        var testUnit = CreateTestMech();
        List<LocationDamageData> hitLocationsData = [
            CreateLocationDamageData(PartLocation.CenterTorso, 3, 2) // Structure damage
        ];

        // Setup dice roller and explosion damage with zero structure damage
        _mockDiceRoller.Roll2D6().Returns([new DiceResult(4), new DiceResult(4)]);
        _mockDiceRoller.RollD6().Returns(new DiceResult(2));

        // Setup explosion damage with zero structure damage
        var explosionDamage = new LocationDamageData(PartLocation.LeftTorso, 0, 0, false);
        _mockDamageTransferCalculator.CalculateExplosionDamage(
                Arg.Any<Unit>(),
                Arg.Any<PartLocation>(),
                Arg.Any<int>())
            .Returns([explosionDamage]);

        // Act
        var result = _sut.CalculateAndApplyCriticalHits(testUnit, hitLocationsData);

        // Assert
        result.ShouldNotBeNull();
        result.CriticalHits.Count.ShouldBe(1); // Only initial critical hit, no explosion processing
        result.CriticalHits[0].Location.ShouldBe(PartLocation.CenterTorso);
    }

    [Fact]
    public void CalculateCriticalHits_ShouldProcessExplosionDamage_WhenCriticalHitCausesExplosion()
    {
        // Arrange
        var testUnit = CreateTestMech();
        var centerTorso = testUnit.Parts.First(p => p.Location == PartLocation.CenterTorso);

        // Add an explodable ammo component
        var ammo = new Ammo(Lrm5.Definition, 24);
        centerTorso.TryAddComponent(ammo, [10]).ShouldBeTrue();

        List<LocationDamageData> hitLocationsData = [
            CreateLocationDamageData(PartLocation.CenterTorso, 3, 2) // Structure damage
        ];

        // Setup dice roller to hit the ammo component
        _mockDiceRoller.Roll2D6().Returns(
            [new DiceResult(4), new DiceResult(4)], // Initial critical hit
            [new DiceResult(4), new DiceResult(4)]  // Explosion critical hit
        );
        _mockDiceRoller.RollD6().Returns(
            new DiceResult(5), // Second group
            new DiceResult(5), // Hit ammo slot (10 -> slot 5 in 6-sided die)
            new DiceResult(2)  // Explosion critical hit slot
        );

        // Setup explosion damage
        var explosionDamage = new LocationDamageData(PartLocation.LeftTorso, 0, 2, false);
        _mockDamageTransferCalculator.CalculateExplosionDamage(
                Arg.Any<Unit>(),
                Arg.Is<PartLocation>(l => l == PartLocation.CenterTorso),
                Arg.Is<int>(d => d > 0))
            .Returns([explosionDamage]);

        // Act
        var result = _sut.CalculateAndApplyCriticalHits(testUnit, hitLocationsData);

        // Assert
        result.ShouldNotBeNull();
        result.CriticalHits.Count.ShouldBe(2); 
        result.CriticalHits.ShouldContain(ch => ch.Location == PartLocation.CenterTorso);
        result.CriticalHits.ShouldContain(ch => ch.Location == PartLocation.LeftTorso);
        _mockDiceRoller.Received(2).Roll2D6(); 
    }

    [Fact]
    public void CalculateCriticalHits_ShouldProcessChainedExplosions_WhenExplosionCausesAnotherExplosion()
    {
        // Arrange
        var testUnit = CreateTestMech();
        var centerTorso = testUnit.Parts.First(p => p.Location == PartLocation.CenterTorso);
        var leftTorso = testUnit.Parts.First(p => p.Location == PartLocation.LeftTorso);

        // Add explodable ammo components
        var ammo1 = new Ammo(Lrm5.Definition, 24);
        centerTorso.TryAddComponent(ammo1, [10]).ShouldBeTrue();
        var ammo2 = new Ammo(Lrm5.Definition, 24);
        leftTorso.TryAddComponent(ammo2, [8]).ShouldBeTrue();

        List<LocationDamageData> hitLocationsData = [
            CreateLocationDamageData(PartLocation.CenterTorso, 3, 2) // Initial structure damage
        ];

        // Setup dice roller for chained explosions
        _mockDiceRoller.Roll2D6().Returns(
            [new DiceResult(4), new DiceResult(4)], // Initial critical hit
            [new DiceResult(4), new DiceResult(4)], // First explosion critical hit
            [new DiceResult(4), new DiceResult(4)]  // Second explosion critical hit
        );
        _mockDiceRoller.RollD6().Returns(
            new DiceResult(5), // second group
            new DiceResult(5), // Hit first ammo
            new DiceResult(5), // Second group
            new DiceResult(3), // Hit second ammo
            new DiceResult(1)  // Final critical hit slot
        );

        // Setup explosion damages
        var firstExplosionDamage = new LocationDamageData(PartLocation.LeftTorso, 0, 2, false);
        var secondExplosionDamage = new LocationDamageData(PartLocation.RightTorso, 0, 2, false);

        _mockDamageTransferCalculator.CalculateExplosionDamage(
                Arg.Any<Unit>(),
                Arg.Is<PartLocation>(l => l == PartLocation.CenterTorso),
                Arg.Any<int>())
            .Returns([firstExplosionDamage]);

        _mockDamageTransferCalculator.CalculateExplosionDamage(
                Arg.Any<Unit>(),
                Arg.Is<PartLocation>(l => l == PartLocation.LeftTorso),
                Arg.Any<int>())
            .Returns([secondExplosionDamage]);

        // Act
        var result = _sut.CalculateAndApplyCriticalHits(testUnit, hitLocationsData);

        // Assert
        result.ShouldNotBeNull();
        result.CriticalHits.Count.ShouldBe(3); 
        result.CriticalHits.ShouldContain(ch => ch.Location == PartLocation.CenterTorso);
        result.CriticalHits.ShouldContain(ch => ch.Location == PartLocation.LeftTorso);
        result.CriticalHits.ShouldContain(ch => ch.Location == PartLocation.RightTorso);
        _mockDiceRoller.Received(3).Roll2D6(); 
    }

    [Fact]
    public void CalculateCriticalHits_ShouldProcessMultipleExplosionsFromSameLocation_WhenMultipleComponentsExplode()
    {
        // Arrange
        var testUnit = CreateTestMech();
        var centerTorso = testUnit.Parts.First(p => p.Location == PartLocation.CenterTorso);

        // Add multiple explodable ammo components
        var ammo1 = new Ammo(Lrm5.Definition, 24);
        centerTorso.TryAddComponent(ammo1, [10]).ShouldBeTrue();
        var ammo2 = new Ammo(Lrm5.Definition, 24);
        centerTorso.TryAddComponent(ammo2, [11]).ShouldBeTrue();

        List<LocationDamageData> hitLocationsData = [
            CreateLocationDamageData(PartLocation.CenterTorso, 3, 2) // Structure damage
        ];

        // Setup dice roller for multiple explosions
        _mockDiceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(5)], // Initial critical hit (2 crits)
            [new DiceResult(3), new DiceResult(3)], // First explosion critical hit
            [new DiceResult(2), new DiceResult(3)]  // Second explosion critical hit
        );
        _mockDiceRoller.RollD6().Returns(
            new DiceResult(5), // Second group
            new DiceResult(5), // Hit first ammo
            new DiceResult(5), // Second group
            new DiceResult(6), // Hit second ammo
            new DiceResult(1), // First explosion slot
            new DiceResult(2)  // Second explosion slot
        );

        // Setup explosion damages
        var firstExplosionDamage = new LocationDamageData(PartLocation.LeftTorso, 0, 2, false);
        var secondExplosionDamage = new LocationDamageData(PartLocation.RightTorso, 0, 2, false);

        _mockDamageTransferCalculator.CalculateExplosionDamage(
                Arg.Any<Unit>(),
                Arg.Is<PartLocation>(l => l == PartLocation.CenterTorso),
                Arg.Any<int>())
            .Returns([firstExplosionDamage], [secondExplosionDamage]);

        // Act
        var result = _sut.CalculateAndApplyCriticalHits(testUnit, hitLocationsData);

        // Assert
        result.ShouldNotBeNull();
        result.CriticalHits.Count.ShouldBe(3); // Initial + 2 explosion
        result.CriticalHits.ShouldContain(ch => ch.Location == PartLocation.CenterTorso);
        result.CriticalHits.ShouldContain(ch => ch.Location == PartLocation.LeftTorso);
        result.CriticalHits.ShouldContain(ch => ch.Location == PartLocation.RightTorso);
        _mockDiceRoller.Received(3).Roll2D6(); 
    }

    [Fact]
    public void CalculateCriticalHits_ShouldHandleMixedDamageScenarios_WhenSomeLocationsHaveStructureDamage()
    {
        // Arrange
        var testUnit = CreateTestMech();
        List<LocationDamageData> hitLocationsData = [
            CreateLocationDamageData(PartLocation.CenterTorso, 5, 0), // Only armor damage
            CreateLocationDamageData(PartLocation.LeftArm, 3, 2),     // Structure damage
            CreateLocationDamageData(PartLocation.RightArm, 4, 0),    // Only armor damage
            CreateLocationDamageData(PartLocation.LeftLeg, 4, 3)      // Structure damage
        ];

        // Setup dice roller for locations with structure damage
        _mockDiceRoller.Roll2D6().Returns(
            [new DiceResult(4), new DiceResult(4)], // LeftArm critical hit
            [new DiceResult(4), new DiceResult(4)]  // LeftLeg critical hit
        );
        _mockDiceRoller.RollD6().Returns(
            new DiceResult(2), // LeftArm slot
            new DiceResult(3)  // LeftLeg slot
        );

        // Act
        var result = _sut.CalculateAndApplyCriticalHits(testUnit, hitLocationsData);

        // Assert
        result.ShouldNotBeNull();
        result.CriticalHits.Count.ShouldBe(2); // Only locations with structure damage
        result.CriticalHits.ShouldContain(ch => ch.Location == PartLocation.LeftArm);
        result.CriticalHits.ShouldContain(ch => ch.Location == PartLocation.LeftLeg);
        result.CriticalHits.ShouldNotContain(ch => ch.Location == PartLocation.CenterTorso);
        result.CriticalHits.ShouldNotContain(ch => ch.Location == PartLocation.RightArm);
        _mockDiceRoller.Received(2).Roll2D6(); // Only for locations with structure damage
    }

    [Fact]
    public void CalculateCriticalHits_ShouldReturnCommandWithZeroCritEntry_WhenNoCriticalHitsOccur()
    {
        // Arrange
        var testUnit = CreateTestMech();
        List<LocationDamageData> hitLocationsData = [
            CreateLocationDamageData(PartLocation.CenterTorso, 3, 2) // Structure damage
        ];

        // Setup dice roller to return no critical hits
        _mockDiceRoller.Roll2D6().Returns([new DiceResult(1), new DiceResult(1)]); // Roll of 2 (no crits)

        // Act
        var result = _sut.CalculateAndApplyCriticalHits(testUnit, hitLocationsData);

        // Assert
        result.ShouldNotBeNull(); // Public method returns command even with no crits
        result.CriticalHits.Count.ShouldBe(1); // Public API still creates location data even with no crits
        result.CriticalHits[0].NumCriticalHits.ShouldBe(0); // But the location has 0 critical hits
        _mockDiceRoller.Received(1).Roll2D6(); // Should have rolled dice
    }

    [Fact]
    public void CalculateCriticalHits_ShouldOnlyProcessStructureDamageLocations_WhenMultipleHitLocations()
    {
        // Arrange
        var testUnit = CreateTestMech();
        List<LocationDamageData> hitLocationsData = [
            CreateLocationDamageData(PartLocation.CenterTorso, 5, 0), // No structure damage
            CreateLocationDamageData(PartLocation.LeftArm, 3, 1),     // Structure damage
            CreateLocationDamageData(PartLocation.RightArm, 2, 0),    // No structure damage
            CreateLocationDamageData(PartLocation.LeftLeg, 4, 3)      // Structure damage
        ];

        // Setup dice roller for locations with structure damage
        _mockDiceRoller.Roll2D6().Returns(
            [new DiceResult(3), new DiceResult(4)], // LeftArm critical hit
            [new DiceResult(5), new DiceResult(6)]  // LeftLeg critical hit
        );
        _mockDiceRoller.RollD6().Returns(
            new DiceResult(2), // LeftArm slot
            new DiceResult(3)  // LeftLeg slot
        );

        // Act
        var result = _sut.CalculateAndApplyCriticalHits(testUnit, hitLocationsData);

        // Assert
        result.ShouldNotBeNull();
        result.CriticalHits.Count.ShouldBe(2); // Only locations with structure damage
        result.CriticalHits.ShouldContain(ch => ch.Location == PartLocation.LeftArm);
        result.CriticalHits.ShouldContain(ch => ch.Location == PartLocation.LeftLeg);
        result.CriticalHits.ShouldNotContain(ch => ch.Location == PartLocation.CenterTorso);
        result.CriticalHits.ShouldNotContain(ch => ch.Location == PartLocation.RightArm);
        _mockDiceRoller.Received(2).Roll2D6(); // Only for locations with structure damage
    }
}
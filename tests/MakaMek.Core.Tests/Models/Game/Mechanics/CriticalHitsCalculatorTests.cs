using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
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
        var structureDamageByLocation = new Dictionary<PartLocation, int>
        {
            { PartLocation.CenterTorso, 0 },
            { PartLocation.LeftArm, 0 }
        };

        // Act
        var result = _sut.CalculateCriticalHitsForStructureDamage(testUnit, structureDamageByLocation);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void CalculateCriticalHitsForStructureDamage_WithStructureDamage_ReturnsCorrectData()
    {
        // Arrange
        var testUnit = CreateTestMech();
        var structureDamageByLocation = new Dictionary<PartLocation, int>
        {
            { PartLocation.CenterTorso, 5 },
            { PartLocation.LeftArm, 3 }
        };

        // Setup dice roller for critical hit checks
        _mockDiceRoller.Roll2D6().Returns(
            [new DiceResult(4), new DiceResult(4)], // Roll of 8 for CenterTorso
            [new DiceResult(5), new DiceResult(5)]  // Roll of 10 for LeftArm
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
        result.Count.ShouldBe(2);

        var centerTorsoResult = result.First(r => r.Location == PartLocation.CenterTorso);
        centerTorsoResult.StructureDamageReceived.ShouldBe(5);
        centerTorsoResult.CriticalHitRoll.ShouldBe(8);
        centerTorsoResult.NumCriticalHits.ShouldBe(1);

        var leftArmResult = result.First(r => r.Location == PartLocation.LeftArm);
        leftArmResult.StructureDamageReceived.ShouldBe(3);
        leftArmResult.CriticalHitRoll.ShouldBe(10);
        leftArmResult.NumCriticalHits.ShouldBe(2);
    }

    [Fact]
    public void CalculateCriticalHitsForHeatExplosion_WithValidComponent_ReturnsCorrectData()
    {
        // Arrange
        var testUnit = CreateTestMech();
        var centerTorso = testUnit.Parts.First(p => p.Location == PartLocation.CenterTorso);

        // Add an explodable ammo component
        var ammo = new Ammo(Lrm5.Definition, 24);
        centerTorso.TryAddComponent(ammo, [5]);

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
        explosionResult.StructureDamageReceived.ShouldBe(0); // Heat explosion doesn't receive structure damage
        explosionResult.CriticalHitRoll.ShouldBe(0); // No roll for forced critical hit
        explosionResult.NumCriticalHits.ShouldBe(1); // One forced critical hit
        explosionResult.HitComponents.ShouldNotBeNull();
        explosionResult.HitComponents!.Length.ShouldBe(1);
        explosionResult.HitComponents[0].Slot.ShouldBe(5);
        explosionResult.Explosions.ShouldNotBeNull();
        explosionResult.Explosions!.Count.ShouldBe(1);
    }

    [Fact]
    public void CalculateCriticalHitsForHeatExplosion_WithNonExplodableComponent_ReturnsBasicCriticalHit()
    {
        // Arrange
        var testUnit = CreateTestMech();
        var centerTorso = testUnit.Parts.First(p => p.Location == PartLocation.CenterTorso);

        // Add a non-explodable component
        var heatSink = new HeatSink();
        centerTorso.TryAddComponent(heatSink, [7]);

        // Act
        var result = _sut.CalculateCriticalHitsForHeatExplosion(testUnit, heatSink);

        // Assert
        result.ShouldNotBeEmpty();
        result.Count.ShouldBe(1);

        var explosionResult = result.First();
        explosionResult.Location.ShouldBe(PartLocation.CenterTorso);
        explosionResult.NumCriticalHits.ShouldBe(1);
        explosionResult.HitComponents.ShouldNotBeNull();
        explosionResult.HitComponents!.Length.ShouldBe(1);
        explosionResult.HitComponents[0].Slot.ShouldBe(7);
        explosionResult.Explosions.ShouldBeNull(); // No explosions for non-explodable components
    }

    [Fact]
    public void CalculateCriticalHitsForStructureDamage_WithExplosiveComponents_IncludesExplosionData()
    {
        // Arrange
        var testUnit = CreateTestMech();
        var centerTorso = testUnit.Parts.First(p => p.Location == PartLocation.CenterTorso);

        // Add an explodable ammo component
        var ammo = new Ammo(Lrm5.Definition, 24);
        centerTorso.TryAddComponent(ammo, [8]);

        var structureDamageByLocation = new Dictionary<PartLocation, int>
        {
            { PartLocation.CenterTorso, 5 }
        };

        // Setup dice roller to hit the ammo component
        _mockDiceRoller.Roll2D6().Returns([new DiceResult(4), new DiceResult(4)]); // Roll of 8
        _mockDiceRoller.RollD6().Returns(new DiceResult(8)); // Hit slot 8 (ammo)

        // Act
        var result = _sut.CalculateCriticalHitsForStructureDamage(testUnit, structureDamageByLocation);

        // Assert
        result.ShouldNotBeEmpty();
        var centerTorsoResult = result.First(r => r.Location == PartLocation.CenterTorso);
        centerTorsoResult.Explosions.ShouldNotBeNull();
        centerTorsoResult.Explosions!.Count.ShouldBe(1);
        centerTorsoResult.Explosions[0].ComponentType.ShouldBe(ammo.ComponentType);
        centerTorsoResult.Explosions[0].Slot.ShouldBe(8);
        centerTorsoResult.Explosions[0].ExplosionDamage.ShouldBeGreaterThan(0);
    }
}
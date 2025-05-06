using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Combat;

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
}
using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Models.Units;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics;

public class FallingDamageCalculatorTests
{
    private readonly IDiceRoller _mockDiceRoller;
    private readonly FallingDamageCalculator _sut;

    public FallingDamageCalculatorTests()
    {
        // Setup mock dice roller
        _mockDiceRoller = Substitute.For<IDiceRoller>();

        // Setup mock rules provider
        IRulesProvider rules = new ClassicBattletechRulesProvider();

        // Setup calculator with mock dice roller and rules provider
        _sut = new FallingDamageCalculator(_mockDiceRoller, rules);
    }

    private static List<UnitPart> CreateBasicPartsData()
    {
        var centerTorso = new CenterTorso("CenterTorso", 31, 10, 6);
        centerTorso.TryAddComponent(new Engine(250));
        return
        [
            new Head("Head", 9, 3),
            centerTorso,
            new SideTorso("LeftTorso", PartLocation.LeftTorso, 25, 8, 6),
            new SideTorso("RightTorso", PartLocation.RightTorso, 25, 8, 6),
            new Arm("RightArm", PartLocation.RightArm, 17, 6),
            new Arm("LeftArm", PartLocation.LeftArm, 17, 6),
            new Leg("RightLeg", PartLocation.RightLeg, 25, 8),
            new Leg("LeftLeg", PartLocation.LeftLeg, 25, 8)
        ];
    }

    private Unit CreateTestMech(int tonnage)
    {
       return new Mech("Test", "TST-1A", tonnage, 4, CreateBasicPartsData());
    }

    // Test modifier implementation for RollModifier
    private record TestModifier : RollModifier
    {
        public required string Name { get; init; }
        
        public override string Render(ILocalizationService localizationService)
        {
            return Name;
        }
    }

    private PsrBreakdown CreatePsrBreakdown(int basePilotingSkill, int modifierTotal = 0, bool impossible = false)
    {
        var modifiers = new List<RollModifier>();
        
        if (modifierTotal != 0)
        {
            modifiers.Add(new TestModifier { Name = "Test Modifier", Value = modifierTotal });
        }
        
        // If we want an impossible roll, make sure the total is at least 13
        if (impossible && basePilotingSkill + modifierTotal < PsrBreakdown.ImpossibleRoll)
        {
            int additionalModifier = PsrBreakdown.ImpossibleRoll - (basePilotingSkill + modifierTotal);
            modifiers.Add(new TestModifier { Name = "Impossible Modifier", Value = additionalModifier });
        }
        
        return new PsrBreakdown
        {
            BasePilotingSkill = basePilotingSkill,
            Modifiers = modifiers
        };
    }

    [Fact]
    public void CalculateFallingDamage_WhenUnitIsNotMech_ThrowsArgumentException()
    {
        // Arrange
        var unit = new UnitTests.TestUnit("test", "unit", 20, 4, []);
        var psrBreakdown = CreatePsrBreakdown(4);

        // Act & Assert
        Should.Throw<ArgumentException>(() => 
            _sut.CalculateFallingDamage(unit, 1, false, psrBreakdown))
            .Message.ShouldContain("Only mechs can take falling damage");
    }

    [Fact]
    public void CalculateFallingDamage_WhenMechNotDeployed_ThrowsArgumentException()
    {
        // Arrange
        var mech = CreateTestMech(20);
        // Don't deploy the mech
        var psrBreakdown = CreatePsrBreakdown(4);

        // Act & Assert
        Should.Throw<ArgumentException>(() => 
            _sut.CalculateFallingDamage(mech, 1, false, psrBreakdown))
            .Message.ShouldContain("Mech must be deployed");
    }

    [Theory]
    [InlineData(0, false, 5)] // 0 levels, not jumping, 50 ton mech = 5 damage
    [InlineData(1, false, 10)] // 1 level, not jumping, 50 ton mech = 10 damage
    [InlineData(2, false, 15)] // 2 levels, not jumping, 50 ton mech = 15 damage
    [InlineData(0, true, 5)] // 0 levels, jumping, 50 ton mech = 5 damage
    [InlineData(1, true, 5)] // 1 level, jumping, 50 ton mech = 5 damage (jumping ignores levels)
    [InlineData(2, true, 5)] // 2 levels, jumping, 50 ton mech = 5 damage (jumping ignores levels)
    public void CalculateFallingDamage_CalculatesTotalDamageCorrectly(int levelsFallen, bool wasJumping, int expectedDamage)
    {
        // Arrange
        var mech = CreateTestMech(50);
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.Top));
        
        var psrBreakdown = CreatePsrBreakdown(4);
        
        // Setup dice roller for facing roll
        _mockDiceRoller.RollD6().Returns(new DiceResult(1));
        
        // Setup dice roller for hit locations
        _mockDiceRoller.Roll2D6().Returns(
            [new DiceResult(3), new DiceResult(3)], // First hit location roll
            [new DiceResult(4), new DiceResult(4)]  // PSR roll
        );
        
        // Act
        var result = _sut.CalculateFallingDamage(mech, levelsFallen, wasJumping, psrBreakdown);
        
        // Assert
        result.HitLocations.TotalDamage.ShouldBe(expectedDamage);
    }

    [Fact]
    public void CalculateFallingDamage_DistributesDamageIntoCorrectGroups()
    {
        // Arrange
        var mech = CreateTestMech(50);
        mech.Deploy(new HexPosition(1,1, HexDirection.Top));
        var psrBreakdown = CreatePsrBreakdown(4);
        const int levelsFallen = 2; // 15 damage for a 50 ton mech (50/10 * (2+1)
        
        // Setup dice roller for facing roll
        _mockDiceRoller.RollD6().Returns(new DiceResult(1));
        
        // Setup dice roller for hit locations (3 groups of 5 damage)
        _mockDiceRoller.Roll2D6().Returns(
            [new DiceResult(3), new DiceResult(4)], // First hit location roll, 7 CT
            [new DiceResult(4), new DiceResult(4)], // Second hit location roll, 8 LT
            [new DiceResult(5), new DiceResult(5)], // Third hit location roll, 10 LA
            [new DiceResult(2), new DiceResult(3)]  // PSR roll
        );
        
        // Act
        var result = _sut.CalculateFallingDamage(mech, levelsFallen, false, psrBreakdown);
        
        // Assert
        result.HitLocations.TotalDamage.ShouldBe(15);
        result.HitLocations.HitLocations.Count.ShouldBe(3);
        
        // Check each hit location group
        result.HitLocations.HitLocations[0].Damage.ShouldBe(5);
        result.HitLocations.HitLocations[0].Location.ShouldBe(PartLocation.CenterTorso);
        
        result.HitLocations.HitLocations[1].Damage.ShouldBe(5);
        result.HitLocations.HitLocations[1].Location.ShouldBe(PartLocation.LeftTorso);
        
        result.HitLocations.HitLocations[2].Damage.ShouldBe(5);
        result.HitLocations.HitLocations[2].Location.ShouldBe(PartLocation.LeftArm);
    }

    [Fact]
    public void CalculateFallingDamage_WithRemainder_DistributesDamageCorrectly()
    {
        // Arrange
        var mech = CreateTestMech(40);
        mech.Deploy(new HexPosition(1,1, HexDirection.Top));
        var psrBreakdown = CreatePsrBreakdown(4);
        const int levelsFallen = 1; // 8 damage for a 40 ton mech
        
        // Setup dice roller for facing roll
        _mockDiceRoller.RollD6().Returns(new DiceResult(1));
        
        // Setup dice roller for hit locations (2 groups: 5 damage and 3 damage)
        _mockDiceRoller.Roll2D6().Returns(
            [new DiceResult(3), new DiceResult(4)], // First hit location roll
            [new DiceResult(4), new DiceResult(4)], // Second hit location roll
            [new DiceResult(2), new DiceResult(3)]  // PSR roll
        );
        
        // Act
        var result = _sut.CalculateFallingDamage(mech, levelsFallen, false, psrBreakdown);
        
        // Assert
        result.HitLocations.TotalDamage.ShouldBe(8);
        result.HitLocations.HitLocations.Count.ShouldBe(2);
        
        // Check each hit location group
        result.HitLocations.HitLocations[0].Damage.ShouldBe(5);
        result.HitLocations.HitLocations[0].Location.ShouldBe(PartLocation.CenterTorso);
        
        result.HitLocations.HitLocations[1].Damage.ShouldBe(3);
        result.HitLocations.HitLocations[1].Location.ShouldBe(PartLocation.LeftTorso);
    }

    [Fact]
    public void CalculateFallingDamage_WithOddDamage_DistributesDamageCorrectly()
    {
        // Arrange
        // Create a 35 ton
        var mech = CreateTestMech(35);
        mech.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));
        
        var psrBreakdown = CreatePsrBreakdown(4);
        const int levelsFallen = 1; // 8 damage for a 35 ton mech
        
        // Setup dice roller for facing roll
        _mockDiceRoller.RollD6().Returns(new DiceResult(1));
        
        // Setup dice roller for hit locations (1 group of 5 damage, 1 group of 3 damage)
        _mockDiceRoller.Roll2D6().Returns(
            [new DiceResult(3), new DiceResult(4)], // First hit location roll
            [new DiceResult(4), new DiceResult(4)], // Second hit location roll
            [new DiceResult(2), new DiceResult(3)]  // PSR roll
        );
        
        // Act
        var result = _sut.CalculateFallingDamage(mech, levelsFallen, false, psrBreakdown);
        
        // Assert
        result.HitLocations.TotalDamage.ShouldBe(8);
        result.HitLocations.HitLocations.Count.ShouldBe(2);
        
        // Check each hit location group
        result.HitLocations.HitLocations[0].Damage.ShouldBe(5);
        result.HitLocations.HitLocations[0].Location.ShouldBe(PartLocation.CenterTorso);
        
        result.HitLocations.HitLocations[1].Damage.ShouldBe(3);
        result.HitLocations.HitLocations[1].Location.ShouldBe(PartLocation.LeftTorso);
    }

    [Fact]
    public void CalculateFallingDamage_UpdatesFacingCorrectly()
    {
        // Arrange
        var mech = CreateTestMech(20);
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.Top));
        var psrBreakdown = CreatePsrBreakdown(4);
        
        // Setup dice roller for facing roll
        _mockDiceRoller.RollD6().Returns(new DiceResult(3));
        
        // Setup dice roller for hit locations
        _mockDiceRoller.Roll2D6().Returns(
            [new DiceResult(3), new DiceResult(3)], // Hit location roll
            [new DiceResult(2), new DiceResult(3)]  // PSR roll
        );
        
        // Act
        var result = _sut.CalculateFallingDamage(mech, 0, false, psrBreakdown);
        
        // Assert
        result.FacingAfterFall.ShouldBe(HexDirection.BottomRight);
        result.FacingDiceRoll.Result.ShouldBe(3);
    }

    [Theory]
    [InlineData(4, true)] // Roll of 5 > skill of 4, pilot takes damage
    [InlineData(6, false)] // Roll of 5 < skill of 6, pilot doesn't take damage
    [InlineData(5, false)] // Roll of 5 = skill of 5, pilot doesn't take damage
    public void CalculateFallingDamage_DeterminesPilotDamageCorrectly(int pilotingSkill, bool expectPilotDamage)
    {
        // Arrange
        const int rollResult = 5; 
        var mech = CreateTestMech(20);
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.Top));
        var psrBreakdown = CreatePsrBreakdown(pilotingSkill);
        
        // Setup dice roller for facing roll
        _mockDiceRoller.RollD6().Returns(new DiceResult(1));
        
        // Setup dice roller for hit locations and PSR
        _mockDiceRoller.Roll2D6().Returns(
            [new DiceResult(3), new DiceResult(3)], // Hit location roll
            [new DiceResult(2), new DiceResult(3)]  // PSR roll with sum of 5
        );
        
        // Act
        var result = _sut.CalculateFallingDamage(mech, 0, false, psrBreakdown);
        
        // Assert
        result.PilotTakesDamage.ShouldBe(expectPilotDamage);
        result.PilotDamageRoll.ShouldNotBeNull();
        result.PilotDamageRoll.Count.ShouldBe(2);
        result.PilotDamageRoll.Sum(r => r.Result).ShouldBe(rollResult);
    }

    [Fact]
    public void CalculateFallingDamage_WhenPsrIsImpossible_PilotAutomaticallyTakesDamage()
    {
        // Arrange
        var mech = CreateTestMech(20);
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.Top));
        var psrBreakdown = CreatePsrBreakdown(4, 0, true); // Make it impossible
        
        // Setup dice roller for facing roll
        _mockDiceRoller.RollD6().Returns(new DiceResult(1));
        
        // Setup dice roller for hit locations (no PSR roll needed)
        _mockDiceRoller.Roll2D6().Returns(
            [new DiceResult(3), new DiceResult(3)] // Hit location roll
        );
        
        // Act
        var result = _sut.CalculateFallingDamage(mech, 0, false, psrBreakdown);
        
        // Assert
        result.PilotTakesDamage.ShouldBe(true);
        result.PilotDamageRoll.ShouldBeNull(); // No roll was made
    }
}

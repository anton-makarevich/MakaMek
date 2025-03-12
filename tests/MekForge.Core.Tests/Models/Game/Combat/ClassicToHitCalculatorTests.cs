using NSubstitute;
using Sanet.MekForge.Core.Models.Game.Combat;
using Sanet.MekForge.Core.Models.Game.Combat.Modifiers;
using Sanet.MekForge.Core.Models.Map;
using Sanet.MekForge.Core.Models.Map.Terrains;
using Sanet.MekForge.Core.Models.Units;
using Sanet.MekForge.Core.Models.Units.Components.Weapons;
using Sanet.MekForge.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MekForge.Core.Tests.Data;
using Sanet.MekForge.Core.Tests.Data.Community;
using Sanet.MekForge.Core.Utils;
using Sanet.MekForge.Core.Utils.Generators;
using Sanet.MekForge.Core.Utils.TechRules;
using Shouldly;

namespace Sanet.MekForge.Core.Tests.Models.Game.Combat;

public class ClassicToHitCalculatorTests
{
    private readonly IRulesProvider _rules;
    private readonly ClassicToHitCalculator _calculator;
    private Unit _attacker;
    private Unit _target;
    private readonly Weapon _weapon;
    private readonly MechFactory _mechFactory;

    public ClassicToHitCalculatorTests()
    {
        _rules = Substitute.For<IRulesProvider>();
        _calculator = new ClassicToHitCalculator(_rules);

        // Setup rules for structure values (needed for MechFactory)
        _rules.GetStructureValues(20).Returns(new Dictionary<PartLocation, int>
        {
            { PartLocation.Head, 8 },
            { PartLocation.CenterTorso, 10 },
            { PartLocation.LeftTorso, 8 },
            { PartLocation.RightTorso, 8 },
            { PartLocation.LeftArm, 4 },
            { PartLocation.RightArm, 4 },
            { PartLocation.LeftLeg, 8 },
            { PartLocation.RightLeg, 8 }
        });

        _mechFactory = new MechFactory(_rules);

        // Setup weapon
        _weapon = new MediumLaser();

        // Default rules setup
        _rules.GetAttackerMovementModifier(MovementType.StandingStill).Returns(0);
        _rules.GetTargetMovementModifier(1).Returns(0);
        _rules.GetRangeModifier(WeaponRange.Short,Arg.Any<int>(), Arg.Any<int>()).Returns(0);
        _rules.GetHeatModifier(0).Returns(0);
    }

    private void SetupAttackerAndTarget(HexPosition attackerPosition, HexPosition targetEndPosition)
    {
        // Setup attacker
        var attackerData = MechFactoryTests.CreateDummyMechData();
        _attacker = _mechFactory.Create(attackerData);
        _attacker.Deploy(attackerPosition);
        _attacker.Move(MovementType.StandingStill, []);

        // Setup target
        var targetData = MechFactoryTests.CreateDummyMechData();
        _target = _mechFactory.Create(targetData);
        var targetStartPosition = new HexPosition(new HexCoordinates(targetEndPosition.Coordinates.Q-1, targetEndPosition.Coordinates.R), HexDirection.Bottom);
        _target.Deploy(targetStartPosition);
        _target.Move(MovementType.Walk, [new PathSegment(targetStartPosition, targetEndPosition, 1).ToData()]);
    }

    [Fact]
    public void GetToHitModifier_NoLineOfSight_ReturnsMaxValue()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(8, 2), HexDirection.Bottom));
        var map = BattleMap.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new HeavyWoodsTerrain()));

        // Act
        var result = _calculator.GetToHitNumber(_attacker, _target, _weapon, map);

        // Assert
        result.ShouldBe(ToHitBreakdown.ImpossibleRoll);
    }

    [Fact]
    public void GetToHitModifier_OutOfRange_ReturnsMaxValue()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(1,1), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(10, 10), HexDirection.Bottom));
        var map = BattleMap.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        _rules.GetRangeModifier(WeaponRange.OutOfRange,Arg.Any<int>(), Arg.Any<int>()).Returns(ToHitBreakdown.ImpossibleRoll);

        // Act
        var result = _calculator.GetToHitNumber(_attacker, _target, _weapon, map);

        // Assert
        result.ShouldBe(ToHitBreakdown.ImpossibleRoll+4);
    }

    [Fact]
    public void GetToHitModifier_ValidShot_ReturnsCorrectModifier()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(5, 2), HexDirection.Bottom));
        var map = BattleMap.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        _rules.GetTerrainToHitModifier("LightWoods").Returns(1);

        // Act
        var result = _calculator.GetToHitNumber(_attacker, _target, _weapon, map);

        // Assert
        // Base gunnery (4) + Attacker movement (0) + Target movement (0) + Terrain (0) = 4
        result.ShouldBe(4);
    }

    [Fact]
    public void GetModifierBreakdown_NoLineOfSight_ReturnsBreakdownWithNoLos()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(5, 2), HexDirection.Bottom));
        var map = BattleMap.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new HeavyWoodsTerrain()));

        // Act
        var result = _calculator.GetModifierBreakdown(_attacker, _target, _weapon, map);

        // Assert
        result.HasLineOfSight.ShouldBeFalse();
        result.Total.ShouldBe(ToHitBreakdown.ImpossibleRoll);
    }

    [Fact]
    public void GetModifierBreakdown_ValidShot_ReturnsDetailedBreakdown()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(5, 2), HexDirection.Bottom));
        var map = BattleMap.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        _rules.GetTerrainToHitModifier("LightWoods").Returns(1);

        // Act
        var result = _calculator.GetModifierBreakdown(_attacker, _target, _weapon, map);

        // Assert
        result.HasLineOfSight.ShouldBeTrue();
        result.GunneryBase.Value.ShouldBe(4);
        result.AttackerMovement.Value.ShouldBe(0);
        result.AttackerMovement.MovementType.ShouldBe(MovementType.StandingStill);
        result.TargetMovement.Value.ShouldBe(0);
        result.TargetMovement.HexesMoved.ShouldBe(1);
        result.RangeModifier.Value.ShouldBe(0);
        result.RangeModifier.Range.ShouldBe(WeaponRange.Short);
        result.TerrainModifiers.Count.ShouldBe(0); // Number of hexes between units
        result.Total.ShouldBe(4); // Base (4) 
    }
    
    [Fact]
    public void GetModifierBreakdown_ValidShot_ReturnsDetailedBreakdown2()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(4, 2), HexDirection.Bottom));
        var map = BattleMap.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new LightWoodsTerrain()));
        _rules.GetTerrainToHitModifier("LightWoods").Returns(1);

        // Act
        var result = _calculator.GetModifierBreakdown(_attacker, _target, _weapon, map);

        // Assert
        result.HasLineOfSight.ShouldBeTrue();
        result.GunneryBase.Value.ShouldBe(4);
        result.AttackerMovement.Value.ShouldBe(0);
        result.TargetMovement.Value.ShouldBe(0);
        result.RangeModifier.Value.ShouldBe(0);
        result.RangeModifier.Range.ShouldBe(WeaponRange.Short);
        result.TerrainModifiers.Count.ShouldBe(2); // Hexes between units (3,2) + target hex (4,2)
        result.TerrainModifiers.All(t => t.Value == 1).ShouldBeTrue();
        result.TerrainModifiers.All(t => t.TerrainId == "LightWoods").ShouldBeTrue();
        result.Total.ShouldBe(6); // Base (4) + terrain (2)
    }

    [Fact]
    public void GetModifierBreakdown_WithHeat_IncludesHeatModifier()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(5, 2), HexDirection.Bottom));
        var map = BattleMap.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        _rules.GetHeatModifier(0).Returns(2);

        // Act
        var result = _calculator.GetModifierBreakdown(_attacker, _target, _weapon, map);

        // Assert
        result.OtherModifiers.Count.ShouldBe(1);
        result.OtherModifiers[0].ShouldBeOfType<HeatAttackModifier>();
        var heatModifier = (HeatAttackModifier)result.OtherModifiers[0];
        heatModifier.Value.ShouldBe(2);
        heatModifier.HeatLevel.ShouldBe(0);
        result.Total.ShouldBe(6); // Base (4) + heat (2)
    }

    [Fact]
    public void GetToHitModifier_UndefinedMovementType_ThrowsException()
    {
        // Arrange
        var attackerData = MechFactoryTests.CreateDummyMechData();
        var attacker = _mechFactory.Create(attackerData);
        var map = BattleMap.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

        // Act & Assert
        Should.Throw<Exception>(() => _calculator.GetToHitNumber(attacker, _target, _weapon, map));
    }

    [Fact]
    public void GetModifierBreakdown_SecondaryTarget_IncludesSecondaryTargetModifier_WhenInFrontArc()
    {
        // Arrange
        const int expectedModifier = 1;
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(2, 2), HexDirection.Bottom));
        var map = BattleMap.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        
        // Setup rules for secondary target modifier
        _rules.GetSecondaryTargetModifier(true).Returns(expectedModifier);

        // Act
        var breakdown = _calculator.GetModifierBreakdown(_attacker, _target, _weapon, map, false);

        // Assert
        var secondaryTargetModifier = breakdown.AllModifiers.FirstOrDefault(m => m is SecondaryTargetModifier);
        secondaryTargetModifier.ShouldNotBeNull();
        secondaryTargetModifier.Value.ShouldBe(expectedModifier);
        
        // Verify the modifier is included in the total
        breakdown.Total.ShouldBe(4 + expectedModifier); // Base 4 (gunnery) + secondary target modifier
    }
    
    [Fact]
    public void GetModifierBreakdown_SecondaryTarget_IncludesSecondaryTargetModifier_WhenOtherArc()
    {
        // Arrange
        const int expectedModifier = 2;
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(5, 5), HexDirection.Top),
            new HexPosition(new HexCoordinates(7, 5), HexDirection.Bottom));
        var map = BattleMap.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        
        // Setup rules for secondary target modifier
        _rules.GetSecondaryTargetModifier(false).Returns(expectedModifier);

        // Act
        var breakdown = _calculator.GetModifierBreakdown(_attacker, _target, _weapon, map, false);

        // Assert
        var secondaryTargetModifier = breakdown.AllModifiers.FirstOrDefault(m => m is SecondaryTargetModifier);
        secondaryTargetModifier.ShouldNotBeNull();
        secondaryTargetModifier.Value.ShouldBe(expectedModifier);
        
        // Verify the modifier is included in the total
        breakdown.Total.ShouldBe(4 + expectedModifier); // Base 4 (gunnery) + secondary target modifier
    }

    [Fact]
    public void GetModifierBreakdown_PrimaryTarget_DoesNotIncludeSecondaryTargetModifier()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(2, 2), HexDirection.Bottom));
        var map = BattleMap.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

        // Act
        var breakdown = _calculator.GetModifierBreakdown(_attacker, _target, _weapon, map);

        // Assert
        var secondaryTargetModifier = breakdown.AllModifiers.FirstOrDefault(m => m is SecondaryTargetModifier);
        secondaryTargetModifier.ShouldBeNull();
        
        // Verify the total doesn't include a secondary target modifier
        breakdown.Total.ShouldBe(4); // Just the base gunnery skil-
    }
}

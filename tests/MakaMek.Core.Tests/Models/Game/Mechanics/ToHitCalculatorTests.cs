using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Tests.Models.Map;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.Generators;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics;

public class ToHitCalculatorTests
{
    private readonly IRulesProvider _rules;
    private readonly ToHitCalculator _sut;
    private Unit? _attacker;
    private Unit? _target;
    private readonly Weapon _weapon;
    private readonly MechFactory _mechFactory;

    public ToHitCalculatorTests()
    {
        _rules = Substitute.For<IRulesProvider>();
        _sut = new ToHitCalculator(_rules);

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

        _mechFactory = new MechFactory(_rules, Substitute.For<ILocalizationService>());

        // Setup weapon
        _weapon = new MediumLaser();

        // Default rules setup
        _rules.GetAttackerMovementModifier(MovementType.StandingStill).Returns(0);
        _rules.GetTargetMovementModifier(1).Returns(0);
        _rules.GetRangeModifier(WeaponRange.Short,Arg.Any<int>(), Arg.Any<int>()).Returns(0);
    }

    private void SetupAttackerAndTarget(HexPosition attackerPosition, HexPosition targetEndPosition)
    {
        // Setup attacker
        var attackerData = MechFactoryTests.CreateDummyMechData();
        _attacker = _mechFactory.Create(attackerData);
        _attacker.AssignPilot(new MechWarrior("John", "Doe"));
        _attacker.Deploy(attackerPosition);
        _attacker.Move(MovementType.StandingStill, []);
        _attacker.Parts.FirstOrDefault(p=>p.Location == PartLocation.RightArm)!.TryAddComponent(_weapon);

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
        var map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new HeavyWoodsTerrain()));

        // Act
        var result = _sut.GetToHitNumber(_attacker!, _target!, _weapon, map);

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
        var map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        _rules.GetRangeModifier(WeaponRange.OutOfRange,Arg.Any<int>(), Arg.Any<int>()).Returns(ToHitBreakdown.ImpossibleRoll);

        // Act
        var result = _sut.GetToHitNumber(_attacker!, _target!, _weapon, map);

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
        var map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        _rules.GetTerrainToHitModifier((MakaMekTerrains.LightWoods)).Returns(1);

        // Act
        var result = _sut.GetToHitNumber(_attacker!, _target!, _weapon, map);

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
        var map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new HeavyWoodsTerrain()));

        // Act
        var result = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, map);

        // Assert
        result.HasLineOfSight.ShouldBeFalse();
        result.Total.ShouldBe(ToHitBreakdown.ImpossibleRoll);
    }

    [Fact]
    public void GetModifierBreakdown_ShouldThrow_WhenPilotIsNotAssigned()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(5, 2), HexDirection.Bottom));
        var map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        _attacker!.UnassignPilot();

        // Act & Assert
        Should.Throw<Exception>(() => _sut.GetModifierBreakdown(_attacker, _target!, _weapon, map));
    }

    [Fact]
    public void GetModifierBreakdown_ValidShot_ReturnsDetailedBreakdown()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(5, 2), HexDirection.Bottom));
        var map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        _rules.GetTerrainToHitModifier((MakaMekTerrains.LightWoods)).Returns(1);

        // Act
        var result = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, map);

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
        var map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new LightWoodsTerrain()));
        _rules.GetTerrainToHitModifier((MakaMekTerrains.LightWoods)).Returns(1);

        // Act
        var result = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, map);

        // Assert
        result.HasLineOfSight.ShouldBeTrue();
        result.GunneryBase.Value.ShouldBe(4);
        result.AttackerMovement.Value.ShouldBe(0);
        result.TargetMovement.Value.ShouldBe(0);
        result.RangeModifier.Value.ShouldBe(0);
        result.RangeModifier.Range.ShouldBe(WeaponRange.Short);
        result.TerrainModifiers.Count.ShouldBe(2); // Hexes between units (3,2) + target hex (4,2)
        result.TerrainModifiers.All(t => t.Value == 1).ShouldBeTrue();
        result.TerrainModifiers.All(t => t.TerrainId == (MakaMekTerrains.LightWoods)).ShouldBeTrue();
        result.Total.ShouldBe(6); // Base (4) + terrain (2)
    }

    [Fact]
    public void GetModifierBreakdown_WithHeat_IncludesHeatModifier()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(5, 2), HexDirection.Bottom));
        var map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        _attacker!.ApplyHeat(new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources = [new WeaponHeatData
                {
                    HeatPoints = 15,
                    WeaponName = ""
                }
            ],
            DissipationData = default
        });
        
        // Act
        var result = _sut.GetModifierBreakdown(_attacker, _target!, _weapon, map);

        // Assert
        result.OtherModifiers.Count.ShouldBe(1);
        result.OtherModifiers[0].ShouldBeOfType<HeatRollModifier>();
        var heatModifier = (HeatRollModifier)result.OtherModifiers[0];
        heatModifier.Value.ShouldBe(2);
        heatModifier.HeatLevel.ShouldBe(15);
        result.Total.ShouldBe(6); // Base (4) + heat (2)
    }

    [Fact]
    public void GetToHitModifier_UndefinedMovementType_ThrowsException()
    {
        // Arrange
        var attackerData = MechFactoryTests.CreateDummyMechData();
        var attacker = _mechFactory.Create(attackerData);
        var map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

        // Act & Assert
        Should.Throw<Exception>(() => _sut.GetToHitNumber(attacker, _target!, _weapon, map));
    }

    [Fact]
    public void GetModifierBreakdown_SecondaryTarget_IncludesSecondaryTargetModifier_WhenInFrontArc()
    {
        // Arrange
        const int expectedModifier = 1;
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(2, 2), HexDirection.Bottom));
        var map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        
        // Setup rules for a secondary target modifier
        _rules.GetSecondaryTargetModifier(true).Returns(expectedModifier);

        // Act
        var breakdown = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, map, false);

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
        var map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        
        // Setup rules for a secondary target modifier
        _rules.GetSecondaryTargetModifier(false).Returns(expectedModifier);

        // Act
        var breakdown = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, map, false);

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
        var map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

        // Act
        var breakdown = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, map);

        // Assert
        var secondaryTargetModifier = breakdown.AllModifiers.FirstOrDefault(m => m is SecondaryTargetModifier);
        secondaryTargetModifier.ShouldBeNull();
        
        // Verify the total doesn't include a secondary target modifier
        breakdown.Total.ShouldBe(4); // Just the base gunnery skill
    }
    
     [Fact]
    public void GetModifierBreakdown_WithIntactSensors_DoesNotIncludeSensorModifier()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(2, 2), HexDirection.Bottom));
        var map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        
        // Act
        var result = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, map);

        // Assert
        result.OtherModifiers.OfType<SensorHitModifier>().ShouldBeEmpty();
    }

    [Fact]
    public void GetModifierBreakdown_WithOneSensorHit_IncludesSensorModifier()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(2, 2), HexDirection.Bottom));
        var map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        
        // Damage sensors once
        var sensors = _attacker!.GetAllComponents<Sensors>().First();
        sensors.Hit();
    
        // Act
        var result = _sut.GetModifierBreakdown(_attacker, _target!, _weapon, map);
    
        // Assert
        var sensorModifier = result.OtherModifiers.OfType<SensorHitModifier>().ShouldHaveSingleItem();
        sensorModifier.Value.ShouldBe(2);
        sensorModifier.SensorHits.ShouldBe(1);
    }

    [Fact]
    public void GetModifierBreakdown_WithAimedShotAtHead_ShouldIncludeAimedShotModifier()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(5, 2), HexDirection.Bottom));
        var map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

        // Act
        var result = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, map, true, PartLocation.Head);

        // Assert
        var aimedShotModifier = result.OtherModifiers.OfType<AimedShotModifier>().ShouldHaveSingleItem();
        aimedShotModifier.Value.ShouldBe(3);
        aimedShotModifier.TargetLocation.ShouldBe(PartLocation.Head);
        // Base gunnery (4) + aimed shot head (+3) = 7
        result.Total.ShouldBe(7);
    }

    [Theory]
    [InlineData(PartLocation.CenterTorso)]
    [InlineData(PartLocation.LeftArm)]
    [InlineData(PartLocation.RightArm)]
    [InlineData(PartLocation.LeftTorso)]
    [InlineData(PartLocation.RightTorso)]
    [InlineData(PartLocation.LeftLeg)]
    [InlineData(PartLocation.RightLeg)]
    public void GetModifierBreakdown_WithAimedShotAtBodyPart_ShouldIncludeAimedShotModifier(PartLocation targetLocation)
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(5, 2), HexDirection.Bottom));
        var map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

        // Act
        var result = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, map, true, targetLocation);

        // Assert
        var aimedShotModifier = result.OtherModifiers.OfType<AimedShotModifier>().ShouldHaveSingleItem();
        aimedShotModifier.Value.ShouldBe(-4);
        aimedShotModifier.TargetLocation.ShouldBe(targetLocation);
        result.Total.ShouldBe(0);
    }

    [Fact]
    public void GetModifierBreakdown_WithoutAimedShot_ShouldNotIncludeAimedShotModifier()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(5, 2), HexDirection.Bottom));
        var map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

        // Act
        var result = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, map);

        // Assert
        result.OtherModifiers.OfType<AimedShotModifier>().ShouldBeEmpty();
        result.Total.ShouldBe(4);
    }

    [Fact]
    public void GetModifierBreakdown_WithAimedShotAndOtherModifiers_ShouldIncludeBoth()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(5, 2), HexDirection.Bottom));
        var map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

        // Damage sensors to create another modifier
        var sensors = _attacker!.GetAllComponents<Sensors>().First();
        sensors.Hit();

        // Act
        var result = _sut.GetModifierBreakdown(_attacker, _target!, _weapon, map, true, PartLocation.CenterTorso);

        // Assert
        result.OtherModifiers.OfType<AimedShotModifier>().ShouldHaveSingleItem();
        result.OtherModifiers.OfType<SensorHitModifier>().ShouldHaveSingleItem();
        result.OtherModifiers.Count.ShouldBe(2);
        result.Total.ShouldBe(2);
    }
}

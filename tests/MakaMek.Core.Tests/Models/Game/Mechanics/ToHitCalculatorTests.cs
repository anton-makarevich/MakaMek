using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
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
    private static readonly IBattleMapFactory BattleMapFactory = new BattleMapFactory();

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

        // Setup aimed shot modifier rules
        _rules.GetAimedShotModifier(PartLocation.Head).Returns(3);
        _rules.GetAimedShotModifier(Arg.Is<PartLocation>(loc => loc != PartLocation.Head)).Returns(-4);

        _mechFactory = new MechFactory(_rules,
            new ClassicBattletechComponentProvider(),
            Substitute.For<ILocalizationService>());

        // Setup weapon
        _weapon = new MediumLaser();

        // Default rules setup
        _rules.GetAttackerMovementModifier(MovementType.StandingStill).Returns(0);
        _rules.GetTargetMovementModifier(1).Returns(0);
        _rules.GetRangeModifier(RangeBracket.Short,Arg.Any<int>(), Arg.Any<int>()).Returns(0);
    }

    private void SetupAttackerAndTarget(HexPosition attackerPosition, HexPosition targetEndPosition)
    {
        // Setup attacker
        var attackerData = MechFactoryTests.CreateDummyMechData();
        _attacker = _mechFactory.Create(attackerData);
        _attacker.AssignPilot(new MechWarrior("John", "Doe"));
        _attacker.Deploy(attackerPosition, null);
        _attacker.Move(MovementPath.CreateStandingStillPath(attackerPosition), null);
        _attacker.Parts.Values.FirstOrDefault(p=>p.Location == PartLocation.RightArm)!.TryAddComponent(_weapon);

        // Setup target
        var targetData = MechFactoryTests.CreateDummyMechData();
        _target = _mechFactory.Create(targetData);
        var targetStartPosition = new HexPosition(new HexCoordinates(targetEndPosition.Coordinates.Q-1, targetEndPosition.Coordinates.R), HexDirection.Bottom);
        _target.Deploy(targetStartPosition, null);
        _target.Move(new MovementPath([
            new PathSegment(targetStartPosition, targetEndPosition, 1)],
            MovementType.Walk), null);
    }

    [Fact]
    public void GetToHitModifier_NoLineOfSight_ReturnsMaxValue()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(8, 2), HexDirection.Bottom));
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new HeavyWoodsTerrain()));

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
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        _rules.GetRangeModifier(RangeBracket.OutOfRange,Arg.Any<int>(), Arg.Any<int>()).Returns(ToHitBreakdown.ImpossibleRoll);

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
            new HexPosition(new HexCoordinates(2, 5), HexDirection.Bottom));
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
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
            new HexPosition(new HexCoordinates(2, 5), HexDirection.Bottom));
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new HeavyWoodsTerrain()));

        // Act
        var result = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, map);

        // Assert
        result.HasLineOfSight.ShouldBeFalse(); // because of heavy woods
        result.Total.ShouldBe(ToHitBreakdown.ImpossibleRoll);
    }
    
    [Fact]
    public void GetModifierBreakdown_ShouldReturnNoArc_WhenTargetIsNotInAnyArc()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(5, 2), HexDirection.Bottom));
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

        // Act
        var result = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, map);

        // Assert
        result.FiringArc.ShouldBeNull();
        result.Total.ShouldBe(ToHitBreakdown.ImpossibleRoll);
    }

    [Fact]
    public void GetModifierBreakdown_ShouldThrow_WhenPilotIsNotAssigned()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(2, 5), HexDirection.Bottom));
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
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
            new HexPosition(new HexCoordinates(2, 5), HexDirection.Bottom));
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
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
        result.RangeModifier.Range.ShouldBe(RangeBracket.Short);
        result.TerrainModifiers.Count.ShouldBe(0); // Number of hexes between units
        result.Total.ShouldBe(4); // Base (4) 
    }
    
    [Fact]
    public void GetModifierBreakdown_ValidShot_ReturnsDetailedBreakdown2()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(2, 4), HexDirection.Bottom));
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new LightWoodsTerrain()));
        _rules.GetTerrainToHitModifier((MakaMekTerrains.LightWoods)).Returns(1);

        // Act
        var result = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, map);

        // Assert
        result.HasLineOfSight.ShouldBeTrue();
        result.GunneryBase.Value.ShouldBe(4);
        result.AttackerMovement.Value.ShouldBe(0);
        result.TargetMovement.Value.ShouldBe(0);
        result.RangeModifier.Value.ShouldBe(0);
        result.RangeModifier.Range.ShouldBe(RangeBracket.Short);
        result.TerrainModifiers.Count.ShouldBe(2); // Hexes between units (2,3) + target hex (2,4)
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
            new HexPosition(new HexCoordinates(2, 5), HexDirection.Bottom));
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        _attacker!.ApplyHeat(new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources = [new WeaponHeatData
                {
                    HeatPoints = 15,
                    WeaponName = ""
                }
            ],
            ExternalHeatSources = [],
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
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

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
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        
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
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        
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
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

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
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        
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
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        
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
            new HexPosition(new HexCoordinates(2, 5), HexDirection.Bottom));
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

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
            new HexPosition(new HexCoordinates(2, 5), HexDirection.Bottom));
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

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
            new HexPosition(new HexCoordinates(2, 5), HexDirection.Bottom));
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

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
            new HexPosition(new HexCoordinates(2, 5), HexDirection.Bottom));
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

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

    [Fact]
    public void AddAimedShotModifier_WithHeadTarget_ShouldAddCorrectModifier()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(2, 5), HexDirection.Bottom));
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

        var baseBreakdown = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, map);

        // Act
        var result = _sut.AddAimedShotModifier(baseBreakdown, PartLocation.Head);

        // Assert
        var aimedShotModifier = result.OtherModifiers.OfType<AimedShotModifier>().ShouldHaveSingleItem();
        aimedShotModifier.Value.ShouldBe(3);
        aimedShotModifier.TargetLocation.ShouldBe(PartLocation.Head);
        result.Total.ShouldBe(baseBreakdown.Total + 3);

        // Verify other properties remain unchanged
        result.GunneryBase.ShouldBe(baseBreakdown.GunneryBase);
        result.AttackerMovement.ShouldBe(baseBreakdown.AttackerMovement);
        result.TargetMovement.ShouldBe(baseBreakdown.TargetMovement);
        result.RangeModifier.ShouldBe(baseBreakdown.RangeModifier);
        result.TerrainModifiers.ShouldBe(baseBreakdown.TerrainModifiers);
        result.HasLineOfSight.ShouldBe(baseBreakdown.HasLineOfSight);
    }

    [Theory]
    [InlineData(PartLocation.CenterTorso)]
    [InlineData(PartLocation.LeftArm)]
    [InlineData(PartLocation.RightArm)]
    [InlineData(PartLocation.LeftTorso)]
    [InlineData(PartLocation.RightTorso)]
    [InlineData(PartLocation.LeftLeg)]
    [InlineData(PartLocation.RightLeg)]
    public void AddAimedShotModifier_WithBodyPartTarget_ShouldAddCorrectModifier(PartLocation targetLocation)
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(2, 5), HexDirection.Bottom));
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

        var baseBreakdown = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, map);

        // Act
        var result = _sut.AddAimedShotModifier(baseBreakdown, targetLocation);

        // Assert
        var aimedShotModifier = result.OtherModifiers.OfType<AimedShotModifier>().ShouldHaveSingleItem();
        aimedShotModifier.Value.ShouldBe(-4);
        aimedShotModifier.TargetLocation.ShouldBe(targetLocation);
        result.Total.ShouldBe(baseBreakdown.Total - 4);
    }

    [Fact]
    public void AddAimedShotModifier_WithExistingAimedShotModifier_ShouldReplaceExistingModifier()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(2, 5), HexDirection.Bottom));
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

        // Create a breakdown with the existing aimed shot modifier
        var breakdownWithAimedShot = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, map, true, PartLocation.Head);

        // Act - add different aimed shot modifier
        var result = _sut.AddAimedShotModifier(breakdownWithAimedShot, PartLocation.CenterTorso);

        // Assert
        var aimedShotModifiers = result.OtherModifiers.OfType<AimedShotModifier>().ToList();
        aimedShotModifiers.ShouldHaveSingleItem(); // Should only have one aimed shot modifier
        aimedShotModifiers[0].TargetLocation.ShouldBe(PartLocation.CenterTorso);
        aimedShotModifiers[0].Value.ShouldBe(-4);
    }

    [Fact]
    public void AddAimedShotModifier_WithOtherModifiers_ShouldPreserveOtherModifiers()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(2, 5), HexDirection.Bottom));
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

        // Damage sensors to create another modifier
        var sensors = _attacker!.GetAllComponents<Sensors>().First();
        sensors.Hit();

        var baseBreakdown = _sut.GetModifierBreakdown(_attacker, _target!, _weapon, map);

        // Act
        var result = _sut.AddAimedShotModifier(baseBreakdown, PartLocation.Head);

        // Assert
        result.OtherModifiers.OfType<AimedShotModifier>().ShouldHaveSingleItem();
        result.OtherModifiers.OfType<SensorHitModifier>().ShouldHaveSingleItem();
        result.OtherModifiers.Count.ShouldBe(2);
    }

    [Fact]
    public void AddAimedShotModifier_ShouldProduceSameResultAsFullRecalculation()
    {
        // Arrange
        SetupAttackerAndTarget(
            new HexPosition(new HexCoordinates(2,2), HexDirection.Bottom),
            new HexPosition(new HexCoordinates(2, 5), HexDirection.Bottom));
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

        var baseBreakdown = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, map);

        // Act & Assert - This test verifies the optimization works correctly
        // The optimized method should produce the same result as full recalculation
        var optimizedResult = _sut.AddAimedShotModifier(baseBreakdown, PartLocation.Head);
        var fullRecalculationResult = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, map, true, PartLocation.Head);

        // Assert results are equivalent
        optimizedResult.Total.ShouldBe(fullRecalculationResult.Total);
        optimizedResult.OtherModifiers.OfType<AimedShotModifier>().Single().Value
            .ShouldBe(fullRecalculationResult.OtherModifiers.OfType<AimedShotModifier>().Single().Value);
        optimizedResult.OtherModifiers.OfType<AimedShotModifier>().Single().TargetLocation
            .ShouldBe(fullRecalculationResult.OtherModifiers.OfType<AimedShotModifier>().Single().TargetLocation);
    }

    [Fact]
    public void GetToHitNumber_WithAttackScenario_ReturnsCorrectValue()
    {
        // Arrange
        var attackerPosition = new HexPosition(new HexCoordinates(2, 2), HexDirection.Bottom);
        var targetPosition = new HexPosition(new HexCoordinates(2, 5), HexDirection.Bottom);
        SetupAttackerAndTarget(attackerPosition, targetPosition); // we still need it to mount weapons
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

        var scenario = AttackScenario.FromHypothetical(
            attackerGunnery: 4,
            attackerPosition: attackerPosition,
            attackerMovementType: MovementType.Walk,
            targetPosition: targetPosition,
            targetHexesMoved: 0,
            attackerModifiers: new List<RollModifier>());

        _rules.GetAttackerMovementModifier(MovementType.Walk).Returns(0);
        _rules.GetTargetMovementModifier(0).Returns(0);
        _rules.GetRangeModifier(Arg.Any<RangeBracket>(), Arg.Any<int>(), Arg.Any<int>()).Returns(0);
        _rules.GetTerrainToHitModifier(Arg.Any<MakaMekTerrains>()).Returns(0);

        // Act
        var result = _sut.GetToHitNumber(scenario, _weapon, map);

        // Assert
        result.ShouldBe(4); // Base gunnery only
    }

    [Fact]
    public void GetModifierBreakdown_WithAttackScenario_IncludesAllModifiers()
    {
        // Arrange
        var attackerPosition = new HexPosition(new HexCoordinates(2, 2), HexDirection.Bottom);
        var targetPosition = new HexPosition(new HexCoordinates(2, 5), HexDirection.Bottom); 
        SetupAttackerAndTarget(attackerPosition, targetPosition);
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

        var heatModifier = new HeatRollModifier { Value = 2, HeatLevel = 15 };
        var scenario = AttackScenario.FromHypothetical(
            attackerGunnery: 4,
            attackerPosition: attackerPosition,
            attackerMovementType: MovementType.Run,
            targetPosition: targetPosition,
            targetHexesMoved: 3,
            attackerModifiers: new List<RollModifier> { heatModifier },
            attackerFacing: HexDirection.Top);

        _rules.GetAttackerMovementModifier(MovementType.Run).Returns(2);
        _rules.GetTargetMovementModifier(3).Returns(1);
        _rules.GetRangeModifier(Arg.Any<RangeBracket>(), Arg.Any<int>(), Arg.Any<int>()).Returns(0);
        _rules.GetTerrainToHitModifier(Arg.Any<MakaMekTerrains>()).Returns(0);

        // Act
        var result = _sut.GetModifierBreakdown(scenario, _weapon, map);

        // Assert
        result.GunneryBase.Value.ShouldBe(4);
        result.AttackerMovement.Value.ShouldBe(2);
        result.AttackerMovement.MovementType.ShouldBe(MovementType.Run);
        result.TargetMovement.Value.ShouldBe(1);
        result.TargetMovement.HexesMoved.ShouldBe(3);
        result.OtherModifiers.ShouldContain(m => m is HeatRollModifier);
        result.Total.ShouldBe(9); // 4 + 2 + 1 + 2
    }

    [Fact]
    public void GetModifierBreakdown_WithAttackScenarioAndAimedShot_IncludesAimedShotModifier()
    {
        // Arrange
        var attackerPosition = new HexPosition(new HexCoordinates(2, 2), HexDirection.Bottom);
        var targetPosition = new HexPosition(new HexCoordinates(2, 5), HexDirection.Bottom);
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

        var scenario = AttackScenario.FromHypothetical(
            attackerGunnery: 4,
            attackerPosition: attackerPosition,
            attackerMovementType: MovementType.Walk,
            targetPosition: targetPosition,
            targetHexesMoved: 0,
            attackerModifiers: new List<RollModifier>(),
            aimedShotTarget: PartLocation.Head);

        _rules.GetAttackerMovementModifier(MovementType.Walk).Returns(0);
        _rules.GetTargetMovementModifier(0).Returns(0);
        _rules.GetRangeModifier(Arg.Any<RangeBracket>(), Arg.Any<int>(), Arg.Any<int>()).Returns(0);
        _rules.GetTerrainToHitModifier(Arg.Any<MakaMekTerrains>()).Returns(0);
        _rules.GetAimedShotModifier(PartLocation.Head).Returns(3);

        // Act
        var result = _sut.GetModifierBreakdown(scenario, _weapon, map);

        // Assert
        var aimedShotModifier = result.OtherModifiers.OfType<AimedShotModifier>().ShouldHaveSingleItem();
        aimedShotModifier.TargetLocation.ShouldBe(PartLocation.Head);
        aimedShotModifier.Value.ShouldBe(3);
    }

    [Fact]
    public void GetModifierBreakdown_WithAttackScenarioAndSecondaryTarget_IncludesSecondaryTargetModifier()
    {
        // Arrange
        var attackerPosition = new HexPosition(new HexCoordinates(2, 2), HexDirection.Bottom);
        var targetPosition = new HexPosition(new HexCoordinates(2, 5), HexDirection.Bottom);
        var map = BattleMapFactory.GenerateMap(10, 10,
            new SingleTerrainGenerator(10, 10, new ClearTerrain()));

        var scenario = AttackScenario.FromHypothetical(
            attackerGunnery: 4,
            attackerPosition: attackerPosition,
            attackerMovementType: MovementType.Walk,
            targetPosition: targetPosition,
            targetHexesMoved: 0,
            attackerModifiers: new List<RollModifier>(),
            attackerFacing: HexDirection.Top,
            isPrimaryTarget: false);

        _rules.GetAttackerMovementModifier(MovementType.Walk).Returns(0);
        _rules.GetTargetMovementModifier(0).Returns(0);
        _rules.GetRangeModifier(Arg.Any<RangeBracket>(), Arg.Any<int>(), Arg.Any<int>()).Returns(0);
        _rules.GetTerrainToHitModifier(Arg.Any<MakaMekTerrains>()).Returns(0);
        _rules.GetSecondaryTargetModifier(Arg.Any<bool>()).Returns(1);

        // Act
        var result = _sut.GetModifierBreakdown(scenario, _weapon, map);

        // Assert
        result.OtherModifiers.OfType<SecondaryTargetModifier>().ShouldHaveSingleItem();
    }

    [Fact]
    public void GetModifierBreakdown_WithTerrainBelowLosLine_ExcludesTerrainModifiers()
    {
        // Arrange
        // Set up a scenario with attacker on elevated hex (1:1, level 2) shooting at target on elevated hex (1:4, level 2)
        // Intervening hexes (1:2, 1:3) at level 0 containing HeavyWoods (height 2, ceiling 2)
        // The LOS line at the intervening hex would be at approximately height 4 (level 2 + mech height 2)
        // So, the terrain ceiling of 2 does not reach it - terrain should be excluded from modifiers
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var targetPosition = new HexPosition(new HexCoordinates(1, 4), HexDirection.Bottom);
        
        SetupAttackerAndTarget(attackerPosition, targetPosition);
        
        // Create a map with elevated attacker/target positions and low intervening hexes
        var sut = BattleMapFactory.GenerateMap(1, 4, new SingleTerrainGenerator(1, 4, new ClearTerrain()));
        
        // Set up elevated positions for attacker and target (level 2)
        var attackerHex = new Hex(new HexCoordinates(1, 1), 2);
        attackerHex.AddTerrain(new ClearTerrain());
        sut.AddHex(attackerHex);
        
        var targetHex = new Hex(new HexCoordinates(1, 4), 2);
        targetHex.AddTerrain(new ClearTerrain());
        sut.AddHex(targetHex);
        
        // Set up intervening hexes at level 0 with HeavyWoods (ceiling 2, which is below LOS line at height ~4)
        for (var r = 2; r <= 3; r++)
        {
            var newHex = new Hex(new HexCoordinates(1, r)); // Level 0
            newHex.AddTerrain(new HeavyWoodsTerrain()); // Ceiling = 2
            sut.AddHex(newHex);
        }
        
        _rules.GetTerrainToHitModifier(MakaMekTerrains.HeavyWoods).Returns(2);
        
        // Act
        var result = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, sut);
        
        // Assert
        result.HasLineOfSight.ShouldBeTrue("LOS should be clear since terrain ceiling is below LOS line");
        result.TerrainModifiers.Count.ShouldBe(0, "Terrain below LOS line should not contribute modifiers");
        // Base gunnery (4) + no terrain modifiers = 4
        result.Total.ShouldBe(4);
    }
    [Fact]
    public void GetModifierBreakdown_WithPartialCover_IncludesPartialCoverModifier()
    {
        // Arrange
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var targetPosition = new HexPosition(new HexCoordinates(1, 4), HexDirection.Bottom);
        
        SetupAttackerAndTarget(attackerPosition, targetPosition);
        
        var map = BattleMapFactory.GenerateMap(1, 4, new SingleTerrainGenerator(1, 4, new ClearTerrain()));
        
        // Target at level 0
        var targetHex = new Hex(new HexCoordinates(1, 4));
        targetHex.AddTerrain(new ClearTerrain());
        map.AddHex(targetHex);
        
        // Adjacent hex at level 1 to provide partial cover
        var adjacentHex = new Hex(new HexCoordinates(1, 3), 1);
        adjacentHex.AddTerrain(new ClearTerrain());
        map.AddHex(adjacentHex);
        
        // Other hexes at level 0
        var attackerHex = new Hex(new HexCoordinates(1, 1));
        attackerHex.AddTerrain(new ClearTerrain());
        map.AddHex(attackerHex);
        
        var interveningHex = new Hex(new HexCoordinates(1, 2));
        interveningHex.AddTerrain(new ClearTerrain());
        map.AddHex(interveningHex);
        
        _rules.HasPartialCover(Arg.Any<IUnit>(), Arg.Any<LineOfSightResult>()).Returns(true);
        _rules.GetPartialCoverModifier().Returns(1);

        // Act
        var result = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, map);

        // Assert
        result.OtherModifiers.OfType<PartialCoverModifier>().ShouldHaveSingleItem()
            .Value.ShouldBe(1);
    }

    [Fact]
    public void GetModifierBreakdown_WithoutPartialCover_DoesNotIncludePartialCoverModifier()
    {
        // Arrange
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var targetPosition = new HexPosition(new HexCoordinates(1, 4), HexDirection.Bottom);
        
        SetupAttackerAndTarget(attackerPosition, targetPosition);
        
        var map = BattleMapFactory.GenerateMap(1, 4, new SingleTerrainGenerator(1, 4, new ClearTerrain()));
        
        _rules.HasPartialCover(Arg.Any<IUnit>(), Arg.Any<LineOfSightResult>()).Returns(false);

        // Act
        var result = _sut.GetModifierBreakdown(_attacker!, _target!, _weapon, map);

        // Assert
        result.OtherModifiers.OfType<PartialCoverModifier>().ShouldBeEmpty();
    }

    // ========== Underwater Combat Tests ==========

    [Fact]
    public void GetModifierBreakdown_UnderwaterAttack_UsesUnderwaterRangeTable()
    {
        // Arrange - Medium Laser at distance 3
        // Normal range: Short (0-3), Medium (4-6), Long (7-9)
        // Underwater range: Short (0-2), Medium (3-4), Long (5-6)
        // Distance 3 should be Medium in underwater range
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var targetPosition = new HexPosition(new HexCoordinates(1, 4), HexDirection.Bottom); // Distance 3

        // Create fresh units for this test (don't use SetupAttackerAndTarget which deploys units)
        var attackerData = MechFactoryTests.CreateDummyMechData();
        _attacker = _mechFactory.Create(attackerData);
        _attacker.AssignPilot(new MechWarrior("John", "Doe"));
        var attackerHex = new Hex(new HexCoordinates(1, 1));
        attackerHex.AddTerrain(new WaterTerrain(-3));
        _attacker.Deploy(attackerPosition, attackerHex);
        _attacker.Move(MovementPath.CreateStandingStillPath(attackerPosition), attackerHex);
        _attacker.Parts.Values.FirstOrDefault(p => p.Location == PartLocation.RightArm)!.TryAddComponent(_weapon);

        var targetData = MechFactoryTests.CreateDummyMechData();
        _target = _mechFactory.Create(targetData);
        var targetStartPosition = new HexPosition(new HexCoordinates(0, 4), HexDirection.Bottom);
        var targetHex = new Hex(new HexCoordinates(1, 4));
        targetHex.AddTerrain(new WaterTerrain(-3));
        _target.Deploy(targetStartPosition, targetHex);
        _target.Move(new MovementPath([
            new PathSegment(targetStartPosition, targetPosition, 1)],
            MovementType.Walk), targetHex);

        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        map.AddHex(attackerHex);
        map.AddHex(targetHex);

        _rules.GetRangeModifier(RangeBracket.Medium, Arg.Any<int>(), Arg.Any<int>()).Returns(2);

        // Act
        var result = _sut.GetModifierBreakdown(_attacker, _target, _weapon, map);

        // Assert - underwater range at distance 3 should be Medium (2 hexes short, 2 hexes medium)
        result.RangeModifier.Range.ShouldBe(RangeBracket.Medium);
    }

    [Fact]
    public void GetModifierBreakdown_SurfaceAttack_UsesStandardRangeTable()
    {
        // Arrange - Medium Laser at distance 3 on clear terrain
        // Normal range: Short (0-3), Medium (4-6), Long (7-9)
        // Distance 3 should be Short
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var targetPosition = new HexPosition(new HexCoordinates(1, 4), HexDirection.Bottom); // Distance 3

        // Create fresh units and deploy on clear terrain (not submerged)
        var attackerData = MechFactoryTests.CreateDummyMechData();
        _attacker = _mechFactory.Create(attackerData);
        _attacker.AssignPilot(new MechWarrior("John", "Doe"));
        var attackerHex = new Hex(new HexCoordinates(1, 1));
        attackerHex.AddTerrain(new ClearTerrain());
        _attacker.Deploy(attackerPosition, attackerHex);
        _attacker.Move(MovementPath.CreateStandingStillPath(attackerPosition), attackerHex);
        _attacker.Parts.Values.FirstOrDefault(p => p.Location == PartLocation.RightArm)!.TryAddComponent(_weapon);

        var targetData = MechFactoryTests.CreateDummyMechData();
        _target = _mechFactory.Create(targetData);
        var targetStartPosition = new HexPosition(new HexCoordinates(0, 4), HexDirection.Bottom);
        var targetHex = new Hex(new HexCoordinates(1, 4));
        targetHex.AddTerrain(new ClearTerrain());
        _target.Deploy(targetStartPosition, targetHex);
        _target.Move(new MovementPath([
            new PathSegment(targetStartPosition, targetPosition, 1)],
            MovementType.Walk), targetHex);

        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

        _rules.GetRangeModifier(RangeBracket.Short, Arg.Any<int>(), Arg.Any<int>()).Returns(0);

        // Act
        var result = _sut.GetModifierBreakdown(_attacker, _target, _weapon, map);

        // Assert - standard range at distance 3 should be Short
        result.RangeModifier.Range.ShouldBe(RangeBracket.Short);
    }

    [Fact]
    public void GetModifierBreakdown_ShallowWaterNotSubmerged_UsesStandardRangeTable()
    {
        // Arrange - Medium Laser in shallow water (depth 0 < height 2)
        // Units are NOT submerged, so standard range applies
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var targetPosition = new HexPosition(new HexCoordinates(1, 4), HexDirection.Bottom); // Distance 3

        // Create fresh units and deploy in shallow water (depth 0, not submerged)
        var attackerData = MechFactoryTests.CreateDummyMechData();
        _attacker = _mechFactory.Create(attackerData);
        _attacker.AssignPilot(new MechWarrior("John", "Doe"));
        var attackerHex = new Hex(new HexCoordinates(1, 1));
        attackerHex.AddTerrain(new WaterTerrain(0));
        _attacker.Deploy(attackerPosition, attackerHex);
        _attacker.Move(MovementPath.CreateStandingStillPath(attackerPosition), attackerHex);
        _attacker.Parts.Values.FirstOrDefault(p => p.Location == PartLocation.RightArm)!.TryAddComponent(_weapon);

        var targetData = MechFactoryTests.CreateDummyMechData();
        _target = _mechFactory.Create(targetData);
        var targetStartPosition = new HexPosition(new HexCoordinates(0, 4), HexDirection.Bottom);
        var targetHex = new Hex(new HexCoordinates(1, 4));
        targetHex.AddTerrain(new WaterTerrain(0));
        _target.Deploy(targetStartPosition, targetHex);
        _target.Move(new MovementPath([
            new PathSegment(targetStartPosition, targetPosition, 1)],
            MovementType.Walk), targetHex);

        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));

        _rules.GetRangeModifier(RangeBracket.Short, Arg.Any<int>(), Arg.Any<int>()).Returns(0);

        // Act
        var result = _sut.GetModifierBreakdown(_attacker, _target, _weapon, map);

        // Assert - not submerged, so standard range at distance 3 should be Short
        result.RangeModifier.Range.ShouldBe(RangeBracket.Short);
    }

    [Fact]
    public void GetModifierBreakdown_UnderwaterAttackWithAttackScenario_UsesUnderwaterRange()
    {
        // Arrange - Medium Laser underwater at distance 3
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var targetPosition = new HexPosition(new HexCoordinates(1, 4), HexDirection.Bottom); // Distance 3

        // Create fresh units for this test
        var attackerData = MechFactoryTests.CreateDummyMechData();
        _attacker = _mechFactory.Create(attackerData);
        _attacker.AssignPilot(new MechWarrior("John", "Doe"));
        var attackerHex = new Hex(new HexCoordinates(1, 1));
        attackerHex.AddTerrain(new WaterTerrain(-3));
        _attacker.Deploy(attackerPosition, attackerHex);
        _attacker.Move(MovementPath.CreateStandingStillPath(attackerPosition), attackerHex);
        _attacker.Parts.Values.FirstOrDefault(p => p.Location == PartLocation.RightArm)!.TryAddComponent(_weapon);

        var targetData = MechFactoryTests.CreateDummyMechData();
        _target = _mechFactory.Create(targetData);
        var targetStartPosition = new HexPosition(new HexCoordinates(0, 4), HexDirection.Bottom);
        var targetHex = new Hex(new HexCoordinates(1, 4));
        targetHex.AddTerrain(new WaterTerrain(-3));
        _target.Deploy(targetStartPosition, targetHex);
        _target.Move(new MovementPath([
            new PathSegment(targetStartPosition, targetPosition, 1)],
            MovementType.Walk), targetHex);

        // Use AttackScenario.FromUnits which includes water depth
        var scenario = AttackScenario.FromUnits(_attacker, _target, PartLocation.RightArm);

        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        map.AddHex(attackerHex);
        map.AddHex(targetHex);

        _rules.GetRangeModifier(RangeBracket.Medium, Arg.Any<int>(), Arg.Any<int>()).Returns(2);

        // Act
        var result = _sut.GetModifierBreakdown(scenario, _weapon, map);

        // Assert - underwater range should apply
        result.RangeModifier.Range.ShouldBe(RangeBracket.Medium);
        scenario.AttackerWaterDepth.ShouldBe(3);
        scenario.TargetWaterDepth.ShouldBe(3);
    }
}

using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.Localization;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.PhysicalAttack;

public class PhysicalAttackCalculatorTests
{
    private readonly IRulesProvider _rules;
    private readonly PhysicalAttackCalculator _sut;
    private static readonly BattleMapFactory BattleMapFactory = new();

    public PhysicalAttackCalculatorTests()
    {
        _rules = Substitute.For<IRulesProvider>();
        _rules.GetPhysicalAttackBaseModifier(PhysicalAttackType.Punch).Returns(0);
        _rules.GetPhysicalAttackBaseModifier(PhysicalAttackType.Kick).Returns(-2);
        _rules.GetPushDamagedShoulderModifier().Returns(2);
        _rules.GetAttackerMovementModifier(MovementType.StandingStill).Returns(0);
        _rules.GetAttackerMovementModifier(MovementType.Walk).Returns(1);
        _rules.GetTargetMovementModifier(0).Returns(0);
        _rules.GetTerrainToHitModifier(Arg.Any<MakaMekTerrains>()).Returns(0);
        _sut = new PhysicalAttackCalculator(_rules);
    }

    private (IUnit Attacker, IUnit Target) CreateUnits(
        HexPosition attackerPosition,
        HexPosition targetPosition,
        MovementPath? attackerMovement = null,
        MovementPath? targetMovement = null,
        IPilot? pilot = null)
    {
        var attacker = Substitute.For<IUnit>();
        attacker.Pilot.Returns(pilot ?? new MechWarrior("John", "Doe", gunnery: 4, piloting: 5));
        attacker.Position.Returns(attackerPosition);
        attacker.MovementTaken.Returns(attackerMovement ?? MovementPath.CreateSingleSegmentPath(attackerPosition));

        var target = Substitute.For<IUnit>();
        target.Pilot.Returns(new MechWarrior("Jane", "Roe", gunnery: 4, piloting: 5));
        target.Position.Returns(targetPosition);
        target.MovementTaken.Returns(targetMovement ?? MovementPath.CreateSingleSegmentPath(targetPosition));

        return (attacker, target);
    }

    private static IBattleMap CreateMap(Terrain? attackerTerrain = null, Terrain? targetTerrain = null)
    {
        var map = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        if (attackerTerrain != null)
        {
            var attackerHex = new Hex(new HexCoordinates(2, 2));
            attackerHex.AddTerrain(attackerTerrain);
            map.AddHex(attackerHex);
        }
        if (targetTerrain != null)
        {
            var targetHex = new Hex(new HexCoordinates(2, 3));
            targetHex.AddTerrain(targetTerrain);
            map.AddHex(targetHex);
        }
        return map;
    }

    private static MovementPath CreateWalkingPath(HexPosition from, HexPosition to)
    {
        return new MovementPath([
            new PathSegment(from, to, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        ], MovementType.Walk);
    }

    [Theory]
    [InlineData(PhysicalAttackType.Punch)]
    [InlineData(PhysicalAttackType.Kick)]
    public void GetModifierBreakdown_PilotingBase_EqualsAttackerPilotingSkill(PhysicalAttackType attackType)
    {
        // Arrange
        var (attacker, target) = CreateUnits(
            new HexPosition(2, 2, HexDirection.Bottom),
            new HexPosition(2, 3, HexDirection.Bottom));
        var map = CreateMap();

        // Act
        var result = _sut.GetModifierBreakdown(attacker, target, attackType, map);

        // Assert
        result.PilotingBase.Value.ShouldBe(5);
        result.PilotingBase.ShouldBeOfType<PilotingRollModifier>();
    }

    [Fact]
    public void GetModifierBreakdown_Punch_HasZeroTypeBase()
    {
        // Arrange
        var (attacker, target) = CreateUnits(
            new HexPosition(2, 2, HexDirection.Bottom),
            new HexPosition(2, 3, HexDirection.Bottom));
        var map = CreateMap();

        // Act
        var result = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Punch, map);

        // Assert
        result.AttackTypeModifier.Value.ShouldBe(0);
        result.AttackTypeModifier.AttackType.ShouldBe(PhysicalAttackType.Punch);
        result.Total.ShouldBe(5); // piloting (5) + punch base (0)
    }

    [Fact]
    public void GetModifierBreakdown_Kick_HasNegativeTwoTypeBase()
    {
        // Arrange
        var (attacker, target) = CreateUnits(
            new HexPosition(2, 2, HexDirection.Bottom),
            new HexPosition(2, 3, HexDirection.Bottom));
        var map = CreateMap();

        // Act
        var result = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Kick, map);

        // Assert
        result.AttackTypeModifier.Value.ShouldBe(-2);
        result.AttackTypeModifier.AttackType.ShouldBe(PhysicalAttackType.Kick);
        result.Total.ShouldBe(3); // piloting (5) + kick base (-2)
    }

    [Fact]
    public void GetModifierBreakdown_WalkingAttacker_IncludesAttackerMovementModifier()
    {
        // Arrange
        var attackerPosition = new HexPosition(2, 2, HexDirection.Bottom);
        var targetPosition = new HexPosition(2, 3, HexDirection.Bottom);
        var (attacker, target) = CreateUnits(
            attackerPosition,
            targetPosition,
            attackerMovement: CreateWalkingPath(attackerPosition, targetPosition));
        var map = CreateMap();

        // Act
        var result = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Punch, map);

        // Assert
        result.AttackerMovement.Value.ShouldBe(1);
        result.AttackerMovement.MovementType.ShouldBe(MovementType.Walk);
    }

    [Fact]
    public void GetModifierBreakdown_MovedTarget_IncludesTargetMovementModifier()
    {
        // Arrange
        var attackerPosition = new HexPosition(2, 2, HexDirection.Bottom);
        var targetPosition = new HexPosition(2, 3, HexDirection.Bottom);
        // a path with 3 hexes traveled
        var targetMovement = new MovementPath([
            new PathSegment(
                new HexPosition(3, 3, HexDirection.Bottom),
                new HexPosition(3, 4, HexDirection.Bottom),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]),
            new PathSegment(
                new HexPosition(3, 4, HexDirection.Bottom),
                new HexPosition(3, 5, HexDirection.Bottom),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]),
            new PathSegment(
                new HexPosition(3, 5, HexDirection.Bottom),
                targetPosition,
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        ], MovementType.Walk);
        var (attacker, target) = CreateUnits(
            attackerPosition,
            targetPosition,
            targetMovement: targetMovement);
        var map = CreateMap();

        // Act
        var result = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Punch, map);

        // Assert
        result.TargetMovement.HexesMoved.ShouldBe(3);
        _rules.Received(1).GetTargetMovementModifier(3);
    }

    [Theory]
    [InlineData(MakaMekTerrains.LightWoods, 1, 1)]
    [InlineData(MakaMekTerrains.HeavyWoods, 2, 1)]
    public void GetModifierBreakdown_TargetHexWoods_IncludesTerrainModifier(
        MakaMekTerrains terrainId,
        int rulesValue,
        int expectedCount)
    {
        // Arrange
        var (attacker, target) = CreateUnits(
            new HexPosition(2, 2, HexDirection.Bottom),
            new HexPosition(2, 3, HexDirection.Bottom));
        _rules.GetTerrainToHitModifier(terrainId).Returns(rulesValue);
        var map = CreateMap(targetTerrain: terrainId == MakaMekTerrains.LightWoods ? new LightWoodsTerrain() : new HeavyWoodsTerrain());

        // Act
        var result = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Punch, map);

        // Assert
        result.TerrainModifiers.Count.ShouldBe(expectedCount);
        var terrainModifier = result.TerrainModifiers[0];
        terrainModifier.TerrainId.ShouldBe(terrainId);
        terrainModifier.Value.ShouldBe(rulesValue);
        terrainModifier.Location.ShouldBe(new HexCoordinates(2, 3));
    }

    [Fact]
    public void GetModifierBreakdown_WoodsOnlyInAttackerHex_AddsNoTerrainModifier()
    {
        // Arrange
        var (attacker, target) = CreateUnits(
            new HexPosition(2, 2, HexDirection.Bottom),
            new HexPosition(2, 3, HexDirection.Bottom));
        _rules.GetTerrainToHitModifier(MakaMekTerrains.LightWoods).Returns(1);
        var map = CreateMap(attackerTerrain: new LightWoodsTerrain());

        // Act
        var result = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Punch, map);

        // Assert
        result.TerrainModifiers.ShouldBeEmpty();
        result.Total.ShouldBe(5); // piloting (5) + punch base (0)
    }

    [Fact]
    public void GetModifierBreakdown_ClearTerrain_AddsNoTerrainModifier()
    {
        // Arrange
        var (attacker, target) = CreateUnits(
            new HexPosition(2, 2, HexDirection.Bottom),
            new HexPosition(2, 3, HexDirection.Bottom));
        var map = CreateMap();

        // Act
        var result = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Punch, map);

        // Assert
        result.TerrainModifiers.ShouldBeEmpty();
    }

    [Fact]
    public void GetModifierBreakdown_CombinedModifiers_ReturnsFullBreakdownAndTotal()
    {
        // Arrange
        var attackerPosition = new HexPosition(2, 2, HexDirection.Bottom);
        var targetPosition = new HexPosition(2, 3, HexDirection.Bottom);
        var (attacker, target) = CreateUnits(
            attackerPosition,
            targetPosition,
            attackerMovement: CreateWalkingPath(attackerPosition, targetPosition));
        // target has moved 5 hexes
        _rules.GetTargetMovementModifier(5).Returns(2);
        var targetMovement = new MovementPath([
            new PathSegment(
                new HexPosition(2, 3, HexDirection.Bottom),
                new HexPosition(2, 4, HexDirection.Bottom),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]),
            new PathSegment(
                new HexPosition(2, 4, HexDirection.Bottom),
                new HexPosition(2, 5, HexDirection.Bottom),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]),
            new PathSegment(
                new HexPosition(2, 5, HexDirection.Bottom),
                new HexPosition(2, 6, HexDirection.Bottom),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]),
            new PathSegment(
                new HexPosition(2, 6, HexDirection.Bottom),
                new HexPosition(3, 6, HexDirection.Bottom),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }]),
            new PathSegment(
                new HexPosition(3, 6, HexDirection.Bottom),
                new HexPosition(3, 5, HexDirection.Bottom),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        ], MovementType.Walk);
        target.MovementTaken.Returns(targetMovement);
        _rules.GetTerrainToHitModifier(MakaMekTerrains.HeavyWoods).Returns(2);
        var map = CreateMap(targetTerrain: new HeavyWoodsTerrain());

        // Act
        var result = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Punch, map);

        // Assert
        var allModifiers = result.AllModifiers;
        allModifiers.Count.ShouldBe(5);
        allModifiers[0].ShouldBeOfType<PilotingRollModifier>();
        allModifiers[1].ShouldBeOfType<PhysicalAttackBaseModifier>();
        allModifiers[2].ShouldBeOfType<AttackerMovementModifier>();
        allModifiers[3].ShouldBeOfType<TargetMovementModifier>();
        allModifiers[4].ShouldBeOfType<TerrainRollModifier>();
        result.OtherModifiers.ShouldBeEmpty();
        result.Total.ShouldBe(10); // piloting (5) + punch (0) + walked (1) + target (2) + heavy woods (2)
    }

    [Fact]
    public void GetModifierBreakdown_ExcludesHeatAndSensorModifiers()
    {
        // Arrange
        var (attacker, _) = CreateUnits(
            new HexPosition(2, 2, HexDirection.Bottom),
            new HexPosition(2, 3, HexDirection.Bottom));
        var target = Substitute.For<IUnit>();
        target.Position.Returns(new HexPosition(2, 3, HexDirection.Bottom));
        target.MovementTaken.Returns(MovementPath.CreateSingleSegmentPath(new HexPosition(2, 3, HexDirection.Bottom)));
        attacker.GetAttackModifiers(Arg.Any<PartLocation>()).Returns(new List<RollModifier>
        {
            new HeatRollModifier { Value = 2, HeatLevel = 15 },
            new SensorHitModifier { Value = 2, SensorHits = 1 }
        });
        var map = CreateMap();
        var baseline = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Punch, map);

        // Act
        var result = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Punch, map);

        // Assert
        attacker.DidNotReceive().GetAttackModifiers(Arg.Any<PartLocation>());
        result.OtherModifiers.ShouldBeEmpty();
        result.AllModifiers.ShouldNotContain(m => m is HeatRollModifier);
        result.AllModifiers.ShouldNotContain(m => m is SensorHitModifier);
        result.Total.ShouldBe(baseline.Total);
    }

    [Fact]
    public void GetModifierBreakdown_WithPartialCover_IncludesPartialCoverModifier()
    {
        // Arrange
        var (attacker, target) = CreateUnits(
            new HexPosition(2, 2, HexDirection.Bottom),
            new HexPosition(2, 3, HexDirection.Bottom));
        _rules.HasPartialCover(Arg.Any<IUnit>(), Arg.Any<LineOfSightResult>()).Returns(true);
        _rules.GetPartialCoverModifier().Returns(1);
        var map = CreateMap();

        // Act
        var result = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Punch, map);

        // Assert
        var partialCover = result.OtherModifiers.OfType<PartialCoverModifier>().ShouldHaveSingleItem();
        partialCover.Value.ShouldBe(1);
        result.Total.ShouldBe(6); // piloting (5) + punch base (0) + partial cover (1)
    }

    [Fact]
    public void GetModifierBreakdown_WithoutPartialCover_DoesNotIncludePartialCoverModifier()
    {
        // Arrange
        var (attacker, target) = CreateUnits(
            new HexPosition(2, 2, HexDirection.Bottom),
            new HexPosition(2, 3, HexDirection.Bottom));
        _rules.HasPartialCover(Arg.Any<IUnit>(), Arg.Any<LineOfSightResult>()).Returns(false);
        var map = CreateMap();

        // Act
        var result = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Punch, map);

        // Assert
        result.OtherModifiers.OfType<PartialCoverModifier>().ShouldBeEmpty();
        result.Total.ShouldBe(5); // piloting (5) + punch base (0)
    }

    [Fact]
    public void GetModifierBreakdown_SkiddingTarget_IncludesSkiddingTargetModifier()
    {
        // Arrange
        var (attacker, target) = CreateUnits(
            new HexPosition(2, 2, HexDirection.Bottom),
            new HexPosition(2, 3, HexDirection.Bottom));
        target.IsSkidding.Returns(true);
        var map = CreateMap();

        // Act
        var result = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Punch, map);

        // Assert
        var skidding = result.OtherModifiers.OfType<SkiddingTargetModifier>().ShouldHaveSingleItem();
        skidding.Value.ShouldBe(2);
        result.Total.ShouldBe(7); // piloting (5) + punch base (0) + skidding target (2)
    }

    [Fact]
    public void GetModifierBreakdown_StandingTarget_DoesNotIncludeSkiddingTargetModifier()
    {
        // Arrange
        var (attacker, target) = CreateUnits(
            new HexPosition(2, 2, HexDirection.Bottom),
            new HexPosition(2, 3, HexDirection.Bottom));
        target.IsSkidding.Returns(false);
        var map = CreateMap();

        // Act
        var result = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Punch, map);

        // Assert
        result.OtherModifiers.OfType<SkiddingTargetModifier>().ShouldBeEmpty();
    }

    [Fact]
    public void GetModifierBreakdown_ProneTarget_IncludesProneTargetModifier()
    {
        // Arrange — physical attackers are always adjacent to their target
        var (attacker, target) = CreateUnits(
            new HexPosition(2, 2, HexDirection.Bottom),
            new HexPosition(2, 3, HexDirection.Bottom));
        target.Status.Returns(UnitStatus.Prone);
        _rules.GetProneTargetModifier(1).Returns(-2);
        var map = CreateMap();

        // Act
        var result = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Punch, map);

        // Assert
        var prone = result.OtherModifiers.OfType<ProneTargetModifier>().ShouldHaveSingleItem();
        prone.Value.ShouldBe(-2);
        _rules.Received(1).GetProneTargetModifier(1);
        result.Total.ShouldBe(3); // piloting (5) + punch base (0) + prone target adjacent (-2)
    }

    [Fact]
    public void GetModifierBreakdown_StandingTarget_DoesNotIncludeProneTargetModifier()
    {
        // Arrange
        var (attacker, target) = CreateUnits(
            new HexPosition(2, 2, HexDirection.Bottom),
            new HexPosition(2, 3, HexDirection.Bottom));
        target.Status.Returns(UnitStatus.Active);
        var map = CreateMap();

        // Act
        var result = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Punch, map);

        // Assert
        result.OtherModifiers.OfType<ProneTargetModifier>().ShouldBeEmpty();
        _rules.DidNotReceive().GetProneTargetModifier(Arg.Any<int>());
    }

    [Fact]
    public void GetModifierBreakdown_SkiddingAttacker_StacksSkiddingOnMovementModifier()
    {
        // Arrange
        var attackerPosition = new HexPosition(2, 2, HexDirection.Bottom);
        var targetPosition = new HexPosition(2, 3, HexDirection.Bottom);
        var (attacker, target) = CreateUnits(
            attackerPosition,
            targetPosition,
            attackerMovement: CreateWalkingPath(attackerPosition, targetPosition));
        attacker.IsSkidding.Returns(true);
        var map = CreateMap();

        // Act
        var result = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Punch, map);

        // Assert — attacker movement modifiers are cumulative: walked (+1) + skidding (+1)
        var skidding = result.OtherModifiers.OfType<SkiddingAttackerModifier>().ShouldHaveSingleItem();
        skidding.Value.ShouldBe(1);
        result.AttackerMovement.Value.ShouldBe(1);
        result.AttackerMovement.MovementType.ShouldBe(MovementType.Walk);
        result.Total.ShouldBe(7); // piloting (5) + punch base (0) + walked (1) + skidding (1)
    }

    [Fact]
    public void GetModifierBreakdown_StationaryAttacker_DoesNotIncludeSkiddingAttackerModifier()
    {
        // Arrange
        var (attacker, target) = CreateUnits(
            new HexPosition(2, 2, HexDirection.Bottom),
            new HexPosition(2, 3, HexDirection.Bottom));
        attacker.IsSkidding.Returns(false);
        var map = CreateMap();

        // Act
        var result = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Punch, map);

        // Assert
        result.OtherModifiers.OfType<SkiddingAttackerModifier>().ShouldBeEmpty();
        result.Total.ShouldBe(5); // piloting (5) + punch base (0)
    }

    [Fact]
    public void GetModifierBreakdown_DoesNotMutateUnits()
    {
        // Arrange
        var (attacker, target) = CreateUnits(
            new HexPosition(2, 2, HexDirection.Bottom),
            new HexPosition(2, 3, HexDirection.Bottom));
        var map = CreateMap();
        var attackerPosition = attacker.Position;
        var attackerMovement = attacker.MovementTaken;

        // Act
        _sut.GetToHitNumber(attacker, target, PhysicalAttackType.Punch, map);
        _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Kick, map);

        // Assert
        attacker.HasDeclaredPhysicalAttack.ShouldBeFalse(); // NSubstitute default
        attacker.Position.ShouldBe(attackerPosition);
        attacker.MovementTaken.ShouldBe(attackerMovement);
        attacker.DidNotReceive().DeclarePhysicalAttack(Arg.Any<PhysicalAttackDeclarationData>());
        attacker.DidNotReceive().DeclareWeaponAttack(Arg.Any<List<WeaponTargetData>>());
        attacker.DidNotReceive().ApplyHeat(Arg.Any<HeatData>());
        attacker.DidNotReceive().ResetPhaseState();
        attacker.DidNotReceive().ResetTurnState();
    }

    [Fact]
    public void GetToHitNumber_EqualsGetModifierBreakdownTotal()
    {
        // Arrange
        var (attacker, target) = CreateUnits(
            new HexPosition(2, 2, HexDirection.Bottom),
            new HexPosition(2, 3, HexDirection.Bottom));
        var map = CreateMap();

        // Act
        var toHitNumber = _sut.GetToHitNumber(attacker, target, PhysicalAttackType.Kick, map);
        var breakdownTotal = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Kick, map).Total;

        // Assert
        toHitNumber.ShouldBe(breakdownTotal);
    }

    [Fact]
    public void UnitOverload_AndScenarioOverload_ReturnSameResult()
    {
        // Arrange
        var attackerPosition = new HexPosition(2, 2, HexDirection.Bottom);
        var targetPosition = new HexPosition(2, 3, HexDirection.Bottom);
        var (attacker, target) = CreateUnits(
            attackerPosition,
            targetPosition,
            attackerMovement: CreateWalkingPath(attackerPosition, targetPosition));
        var map = CreateMap();
        var scenario = PhysicalAttackScenario.FromUnits(attacker, target, PhysicalAttackType.Kick);

        // Act
        var unitResult = _sut.GetToHitNumber(attacker, target, PhysicalAttackType.Kick, map);
        var scenarioResult = _sut.GetToHitNumber(scenario, map);
        var unitBreakdown = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Kick, map);
        var scenarioBreakdown = _sut.GetModifierBreakdown(scenario, map);

        // Assert
        scenarioResult.ShouldBe(unitResult);
        scenarioBreakdown.Total.ShouldBe(unitBreakdown.Total);
        scenarioBreakdown.AllModifiers.Count.ShouldBe(unitBreakdown.AllModifiers.Count);
    }

    [Theory]
    [InlineData(PhysicalAttackType.Charge)]
    [InlineData(PhysicalAttackType.DFA)]
    public void GetToHitNumber_UnsupportedAttackType_ThrowsNotSupportedException(PhysicalAttackType attackType)
    {
        // Arrange
        var sut = new PhysicalAttackCalculator(new TotalWarfareRulesProvider());
        var (attacker, target) = CreateUnits(
            new HexPosition(2, 2, HexDirection.Bottom),
            new HexPosition(2, 3, HexDirection.Bottom));
        var map = CreateMap();

        // Act & Assert
        Should.Throw<NotSupportedException>(() => sut.GetToHitNumber(attacker, target, attackType, map));
    }

    [Fact]
    public void GetModifierBreakdown_Push_AddsTwoPerDamagedShoulder()
    {
        // Arrange
        var attacker = CreateRealMech(new HexPosition(2, 2, HexDirection.Bottom));
        var target = CreateRealMech(new HexPosition(2, 3, HexDirection.Bottom));
        var map = CreateMap();
        attacker.Parts[PartLocation.LeftArm].GetComponent<ShoulderActuator>()!.Hit();
        _rules.GetPhysicalAttackBaseModifier(PhysicalAttackType.Push).Returns(-1);

        // Act
        var oneShoulder = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Push, map);

        attacker.Parts[PartLocation.RightArm].GetComponent<ShoulderActuator>()!.Hit();
        var twoShoulders = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Push, map);

        // Assert
        var oneModifier = oneShoulder.OtherModifiers.OfType<DamagedShoulderModifier>().ShouldHaveSingleItem();
        oneModifier.Value.ShouldBe(2);
        oneModifier.DamagedShoulders.ShouldBe(1);
        var twoModifier = twoShoulders.OtherModifiers.OfType<DamagedShoulderModifier>().ShouldHaveSingleItem();
        twoModifier.Value.ShouldBe(4);
        twoModifier.DamagedShoulders.ShouldBe(2);
    }

    [Fact]
    public void GetModifierBreakdown_IntactShoulders_HasNoDamagedShoulderModifier()
    {
        // Arrange
        var attacker = CreateRealMech(new HexPosition(2, 2, HexDirection.Bottom));
        var target = CreateRealMech(new HexPosition(2, 3, HexDirection.Bottom));
        var map = CreateMap();
        _rules.GetPhysicalAttackBaseModifier(PhysicalAttackType.Push).Returns(-1);

        // Act
        var result = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Push, map);

        // Assert
        result.OtherModifiers.OfType<DamagedShoulderModifier>().ShouldBeEmpty();
    }

    [Fact]
    public void GetModifierBreakdown_Punch_DoesNotAddThePushShoulderModifier()
    {
        // Arrange: a destroyed shoulder blocks a punch with that arm, but it never adds the push modifier
        var attacker = CreateRealMech(new HexPosition(2, 2, HexDirection.Bottom));
        var target = CreateRealMech(new HexPosition(2, 3, HexDirection.Bottom));
        var map = CreateMap();
        attacker.Parts[PartLocation.LeftArm].GetComponent<ShoulderActuator>()!.Hit();

        // Act
        var result = _sut.GetModifierBreakdown(attacker, target, PhysicalAttackType.Punch, map);

        // Assert
        result.OtherModifiers.OfType<DamagedShoulderModifier>().ShouldBeEmpty();
    }

    private static Mech CreateRealMech(HexPosition position)
    {
        var mech = new MechFactory(
                new TotalWarfareRulesProvider(),
                new ClassicBattletechComponentProvider(),
                Substitute.For<ILocalizationService>())
            .Create(MechFactoryTests.CreateDummyMechData());
        mech.AssignPilot(new MechWarrior("Test", "Pilot", gunnery: 4, piloting: 5));
        mech.Deploy(position, null);
        mech.Move(MovementPath.CreateSingleSegmentPath(position), null);
        return mech;
    }
}

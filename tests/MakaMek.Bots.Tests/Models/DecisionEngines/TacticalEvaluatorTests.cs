using NSubstitute;
using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Shouldly;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Bots.Tests.Models.DecisionEngines;

public class TacticalEvaluatorTests
{
    private readonly IClientGame _game;
    private readonly IBattleMap _battleMap;
    private readonly IToHitCalculator _toHitCalculator;
    private readonly TacticalEvaluator _sut;

    public TacticalEvaluatorTests()
    {
        _game = Substitute.For<IClientGame>();
        _battleMap = Substitute.For<IBattleMap>();
        _toHitCalculator = Substitute.For<IToHitCalculator>();
        
        _game.BattleMap.Returns(_battleMap);
        _game.ToHitCalculator.Returns(_toHitCalculator);
        
        _sut = new TacticalEvaluator(_game);
    }

    [Fact]
    public void EvaluatePath_WhenNoBattleMap_ShouldReturnZeroIndices()
    {
        // Arrange
        _game.BattleMap.Returns((IBattleMap?)null);
        var unit = Substitute.For<IUnit>();
        var path = MovementPath.CreateSingleSegmentPath(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));

        // Act
        var result = _sut.EvaluatePath(unit, path,[]);

        // Assert
        result.DefensiveIndex.ShouldBe(0);
        result.OffensiveIndex.ShouldBe(0);
    }

    [Fact]
    public void EvaluatePath_WhenEnemyVisible_ShouldCalculateDefensiveIndex()
    {
        // Arrange
        var unit = Substitute.For<IUnit>();
        var path = MovementPath.CreateSingleSegmentPath(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        
        var enemy = MovementEngineTests.CreateTestMech();
        var enemyPos = new HexPosition(new HexCoordinates(1, 4), HexDirection.Top);
        enemy.Move(new MovementPath(new List<PathSegment>
        {
            new(enemyPos, enemyPos, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }])
        }, MovementType.Walk),null);
        
        // Set up enemy weapon
        var weaponDef = new WeaponDefinition("TestLaser", 5, 1, new WeaponRange(0, 3, 6, 9), WeaponType.Energy, 100);
        var weapon = new TestWeapon(weaponDef);
        var part = enemy.Parts[PartLocation.RightArm];
        part.TryAddComponent(weapon);
        
        var enemies = new List<IUnit> { enemy };

        // Setup LoS
        _battleMap.GetLineOfSight(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(LineOfSightResult.Unblocked(new HexCoordinates(1, 1), new HexCoordinates(1, 4)));
        
        // Setup ToHit
        const int toHitNumber = 6; // High probability
        _toHitCalculator.GetToHitNumber(Arg.Any<AttackScenario>(), Arg.Any<Weapon>(), Arg.Any<IBattleMap>())
            .Returns(toHitNumber);

        // Act
        var result = _sut.EvaluatePath(unit, path, enemies);

        // Assert
        result.DefensiveIndex.ShouldBeGreaterThan(0);
        // Expected: Prob(6) * Damage(5) * ArcMultiplier(Front=1)
        // Prob(6) = 26/36 ~= 0.7222
        // 0.7222 * 5 = 3.6111
        var expectedProb = DiceUtils.Calculate2d6Probability(toHitNumber);
        result.DefensiveIndex.ShouldBe(expectedProb * 5, 0.001);
    }
    
    [Fact]
    public void EvaluatePath_WhenEnemyNotVisible_ShouldNotAffectDefensiveIndex()
    {
        // Arrange
        var unit = Substitute.For<IUnit>();
        var path = MovementPath.CreateSingleSegmentPath(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        
        var enemy = MovementEngineTests.CreateTestMech();
        var enemyPos = new HexPosition(new HexCoordinates(1, 4), HexDirection.Top);
        enemy.Move(new MovementPath(new List<PathSegment>
        {
            new(enemyPos, enemyPos, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }])
        }, MovementType.Walk), null);
        
        // Set up enemy weapon
        var weaponDef = new WeaponDefinition("TestLaser", 5, 1, new WeaponRange(0, 3, 6, 9), WeaponType.Energy, 100);
        var weapon = new TestWeapon(weaponDef);
        var part = enemy.Parts[PartLocation.RightArm];
        part.TryAddComponent(weapon);
        
        var enemies = new List<IUnit> { enemy };

        // Setup LoS - Obstruction
        _battleMap.GetLineOfSight(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<int>()).Returns(LineOfSightResult.Blocked(
            new HexCoordinates(1, 1), new HexCoordinates(1, 4), new HexCoordinates(1, 2)));

        // Act
        var result = _sut.EvaluatePath(unit, path, enemies);

        // Assert
        result.DefensiveIndex.ShouldBe(0);
    }
    
    [Fact]
    public void EvaluatePath_WhenWeaponOutOfRange_ShouldIgnore()
    {
        // Arrange
        var unit = Substitute.For<IUnit>();
        var path = MovementPath.CreateSingleSegmentPath(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        
        var enemy = MovementEngineTests.CreateTestMech();
        // 11 hexes away
        var enemyPos = new HexPosition(new HexCoordinates(1, 12), HexDirection.Top);
        enemy.Move(new MovementPath(new List<PathSegment>
        {
            new(enemyPos, enemyPos, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }])
        }, MovementType.Walk), null);
        
        var weaponDef = new WeaponDefinition("TestLaser", 5, 1, new WeaponRange(0, 3, 6, 9), WeaponType.Energy, 100);
        var weapon = new TestWeapon(weaponDef);
        var part = enemy.Parts[PartLocation.RightArm];
        part.TryAddComponent(weapon);
        
        var enemies = new List<IUnit> { enemy };

        _battleMap.GetLineOfSight(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(LineOfSightResult.Unblocked(new HexCoordinates(1, 1), new HexCoordinates(1, 12)));

        // Act
        var result = _sut.EvaluatePath(unit, path, enemies);

        // Assert
        result.DefensiveIndex.ShouldBe(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EvaluatePath_ShouldCalculateOffensiveIndex_WhenEnemyVisible(bool hasLos)
    {
        // Arrange
        var unit = MovementEngineTests.CreateTestMech();
        // Setup Pilot for Gunnery
        var pilot = Substitute.For<IPilot>();
        pilot.Gunnery.Returns(4);
        unit.AssignPilot(pilot);
        // Unit at (1,1) Facing Bottom (South) to see Enemy at (1,4)
        var unitPos = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var path = new MovementPath(new List<PathSegment>
        {
            new(unitPos, unitPos, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }])
        }, MovementType.Walk);
        unit.Move(path, null);
        
        var enemy = MovementEngineTests.CreateTestMech();
        var enemyPos = new HexPosition(new HexCoordinates(1, 4), HexDirection.Bottom);
        enemy.Move(new MovementPath(new List<PathSegment>
        {
            new(enemyPos, enemyPos, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }])
        }, MovementType.Walk), null);
        
        // Set up friendly weapon
        var weaponDef = new WeaponDefinition("TestLaser", 5, 1, new WeaponRange(0, 3, 6, 9), WeaponType.Energy, 100);
        var weapon = new TestWeapon(weaponDef);
        var part = unit.Parts[PartLocation.RightArm];
        part.TryAddComponent(weapon);
        
        var enemies = new List<IUnit> { enemy };

        // Setup LoS
        _battleMap.GetLineOfSight(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(hasLos 
                ? LineOfSightResult.Unblocked(new HexCoordinates(1, 1), new HexCoordinates(1, 4))
                : LineOfSightResult.Blocked(new HexCoordinates(1, 1), new HexCoordinates(1, 4), new HexCoordinates(1, 2)));
        
        // Setup ToHit
        const int toHitNumber = 8;
        _toHitCalculator.GetToHitNumber(Arg.Any<AttackScenario>(), Arg.Any<Weapon>(), Arg.Any<IBattleMap>())
            .Returns(toHitNumber);
        
        // Act
        var result = _sut.EvaluatePath(unit, path, enemies);

        // Assert
        if (hasLos)
        {
            result.OffensiveIndex.ShouldBeGreaterThan(0);
        }
        else
        {
            result.OffensiveIndex.ShouldBe(0);
        }
    }

    [Fact]
    public void EvaluatePath_WhenWeaponOutOfArc_ShouldIgnore()
    {
        // Arrange
        var unit = MovementEngineTests.CreateTestMech();
        // Setup Pilot for Gunnery
        var pilot = Substitute.For<IPilot>();
        pilot.Gunnery.Returns(4);
        unit.AssignPilot(pilot);
        // Unit at (1,1) Facing Top, Enemy at (1,4) is not in arc for a weapon on the right torso even when rotated
        var unitPos = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var path = new MovementPath(new List<PathSegment>
        {
            new(unitPos, unitPos, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }])
        }, MovementType.Walk);
        unit.Move(path, null);
        
        var enemy = MovementEngineTests.CreateTestMech();
        var enemyPos = new HexPosition(new HexCoordinates(1, 4), HexDirection.Bottom);
        enemy.Move(new MovementPath(new List<PathSegment>
        {
            new(enemyPos, enemyPos, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }])
        }, MovementType.Walk), null);
        
        // Set up friendly weapon
        var weaponDef = new WeaponDefinition("TestLaser", 5, 1, new WeaponRange(0, 3, 6, 9), WeaponType.Energy, 100);
        var weapon = new TestWeapon(weaponDef);
        var part = unit.Parts[PartLocation.RightTorso];
        part.TryAddComponent(weapon);
        
        var enemies = new List<IUnit> { enemy };

        // Setup LoS
        _battleMap.GetLineOfSight(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(LineOfSightResult.Unblocked(new HexCoordinates(1, 1), new HexCoordinates(1, 4)));
        
        // Setup ToHit
        const int toHitNumber = 8;
        _toHitCalculator.GetToHitNumber(Arg.Any<AttackScenario>(), Arg.Any<Weapon>(), Arg.Any<IBattleMap>())
            .Returns(toHitNumber);
        
        var result = _sut.EvaluatePath(unit, path, enemies);

        // Assert
        result.OffensiveIndex.ShouldBe(0);
    }
    
    [Fact]
    public void EvaluateTargets_ShouldReturnScores_WhenTargetsAreInRange()
    {
        // Arrange
        var unit = MovementEngineTests.CreateTestMech();
        var pilot = Substitute.For<IPilot>();
        pilot.Gunnery.Returns(4);
        unit.AssignPilot(pilot);
        
        // Unit at (1,1) Facing Bottom
        var unitPos = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var path = new MovementPath([new PathSegment(unitPos, unitPos, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }])], MovementType.Walk);
        unit.Move(path, null);

        // Enemy 1: In Range, hit prob > 0
        var enemy1 = MovementEngineTests.CreateTestMech();
        var enemy1Pos = new HexPosition(new HexCoordinates(1, 4), HexDirection.Top);
        enemy1.Move(new MovementPath([new PathSegment(enemy1Pos, enemy1Pos, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }])], MovementType.Walk), null);
        
        // Enemy 2: In Range but hit prob 0 (e.g., obscured but technically LoS exists)
        var enemy2 = MovementEngineTests.CreateTestMech();
        var enemy2Pos = new HexPosition(new HexCoordinates(1, 6), HexDirection.Top);
        enemy2.Move(new MovementPath([new PathSegment(enemy2Pos, enemy2Pos, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }])], MovementType.Walk), null);

        var weaponDef = new WeaponDefinition("TestLaser", 5, 1, new WeaponRange(0, 3, 6, 9), WeaponType.Energy, 100);
        var weapon = new TestWeapon(weaponDef);
        unit.Parts[PartLocation.RightArm].TryAddComponent(weapon);

        var potentialTargets = new List<IUnit> { enemy1, enemy2 };

        _battleMap.GetLineOfSight(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(LineOfSightResult.Unblocked(new HexCoordinates(1, 1), new HexCoordinates(1, 6)));
        
        // ToHit for Enemy 1 -> 8 (prob 0.4166)
        _toHitCalculator.GetToHitNumber(
            Arg.Is<AttackScenario>(s => s.TargetPosition.Coordinates == enemy1Pos.Coordinates), 
            Arg.Any<Weapon>(), 
            Arg.Any<IBattleMap>())
            .Returns(8);

        // ToHit for Enemy 2 -> 13 (impossible, prob 0)
        _toHitCalculator.GetToHitNumber(
            Arg.Is<AttackScenario>(s => s.TargetPosition.Coordinates == enemy2Pos.Coordinates), 
            Arg.Any<Weapon>(), 
            Arg.Any<IBattleMap>())
            .Returns(13);

        // Act
        var results = _sut.EvaluateTargets(unit, path, potentialTargets);

        // Assert
        results.Count.ShouldBe(1);
        results[0].TargetId.ShouldBe(enemy1.Id);
        results[0].ConfigurationScores.Count.ShouldBeGreaterThan(0);
        results[0].ConfigurationScores[0].Score.ShouldBeGreaterThan(0);
        results[0].ConfigurationScores[0].ViableWeapons.Count.ShouldBe(1);
        results[0].ConfigurationScores[0].ViableWeapons[0].Weapon.ShouldBe(weapon);
        results[0].ConfigurationScores[0].ViableWeapons[0].HitProbability.ShouldBe(DiceUtils.Calculate2d6Probability(8));
    }

    [Fact]
    public void EvaluatePath_WhenEnemyInRearArc_ShouldCorrectlyCountEnemiesInRearArc_EvenIfNoLoS()
    {
        // Arrange
        var unit = Substitute.For<IUnit>();
        
        // Bot moves from (0715) facing Top to (0616) facing BottomRight
        var startPosition = new HexPosition(new HexCoordinates(7, 15), HexDirection.Top);
        unit.Position.Returns(startPosition);
        var destinationPosition = new HexPosition(new HexCoordinates(6, 16), HexDirection.BottomRight);
        var path = MovementPath.CreateSingleSegmentPath(destinationPosition);
        
        // Enemy at (0112) facing BottomRight - should be in the rear arc of destination position
        var enemy = MovementEngineTests.CreateTestMech();
        var enemyPosition = new HexPosition(new HexCoordinates(1, 12), HexDirection.BottomRight);
        enemy.Move(new MovementPath(new List<PathSegment>
        {
            new(enemyPosition, enemyPosition, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }])
        }, MovementType.Walk), null);
        
        var enemies = new List<IUnit> { enemy };

        // Enemy should be counted even if there is no LoS
        _battleMap.GetLineOfSight(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(LineOfSightResult.Blocked(new HexCoordinates(1, 1), new HexCoordinates(1, 12), new HexCoordinates(1, 2)));
        
        // Setup ToHit
        const int toHitNumber = 8;
        _toHitCalculator.GetToHitNumber(Arg.Any<AttackScenario>(), Arg.Any<Weapon>(), Arg.Any<IBattleMap>())
            .Returns(toHitNumber);

        // Act
        var result = _sut.EvaluatePath(unit, path, enemies);

        // Assert
        // The enemy at (0112) should be correctly identified as being in the rear arc
        // of a unit at (0616) facing BottomRight
        result.EnemiesInRearArc.ShouldBe(1, "Enemy at (0112) should be in rear arc of unit at (0616) facing BottomRight");
    }

    [Fact]
    public void EvaluateTargets_ShouldExcludeWeapon_WhenTargetTooFar()
    {
        // Arrange
        var unit = MovementEngineTests.CreateTestMech();
        var pilot = Substitute.For<IPilot>();
        pilot.Gunnery.Returns(4);
        unit.AssignPilot(pilot);
        
        // Unit at (1,1) Facing Bottom
        var unitPos = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var path = new MovementPath([new PathSegment(unitPos, unitPos, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }])], MovementType.Walk);
        unit.Move(path, null);

        // Enemy at 10 hexes away - beyond long range
        var enemy = MovementEngineTests.CreateTestMech();
        var enemyPos = new HexPosition(new HexCoordinates(1, 11), HexDirection.Top);
        enemy.Move(new MovementPath([new PathSegment(enemyPos, enemyPos, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }])], MovementType.Walk), null);

        // Weapon with long range of 9 (enemy is at range 10)
        var weaponDef = new WeaponDefinition("TestLaser", 5, 1, new WeaponRange(0, 3, 6, 9), WeaponType.Energy, 100);
        var weapon = new TestWeapon(weaponDef);
        unit.Parts[PartLocation.RightArm].TryAddComponent(weapon);

        var potentialTargets = new List<IUnit> { enemy };

        _battleMap.GetLineOfSight(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(LineOfSightResult.Unblocked(new HexCoordinates(1, 1), new HexCoordinates(1, 11)));
        _toHitCalculator.GetToHitNumber(Arg.Any<AttackScenario>(), Arg.Any<Weapon>(), Arg.Any<IBattleMap>())
            .Returns(8);

        // Act
        var results = _sut.EvaluateTargets(unit, path, potentialTargets);

        // Assert
        results.Count.ShouldBe(0, "Weapon should be excluded when target is beyond long rangeBracket");
    }

    [Theory]
    [InlineData(PartLocation.RightLeg,0)]// Legs don't rotate with the torso
    [InlineData(PartLocation.RightTorso, 1)]
    public void EvaluateTargets_ShouldOnlyIncludeRotationConfig_WhenMountingPartSupportsIt(PartLocation partLocation, int expectedConfigs)
    {
        // Arrange
        var unit = MovementEngineTests.CreateTestMech();
        var pilot = Substitute.For<IPilot>();
        pilot.Gunnery.Returns(4);
        unit.AssignPilot(pilot);
        
        // Unit at (1,1) Facing Bottom
        var unitPos = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var path = new MovementPath([new PathSegment(unitPos, unitPos, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }])], MovementType.Walk);
        unit.Move(path, null);

        // Enemy at range
        var enemy = MovementEngineTests.CreateTestMech();
        var enemyPos = new HexPosition(new HexCoordinates(4, 1), HexDirection.Top);
        enemy.Move(new MovementPath([new PathSegment(enemyPos, enemyPos, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }])], MovementType.Walk), null);

        // Mount on a leg part - legs don't support torso rotation
        var weaponDef = new WeaponDefinition("TestLaser", 5, 1, new WeaponRange(0, 3, 6, 9), WeaponType.Energy, 100);
        var weapon = new TestWeapon(weaponDef);
        var legPart = unit.Parts[partLocation]; 
        legPart.TryAddComponent(weapon);

        var potentialTargets = new List<IUnit> { enemy };

        _battleMap.GetLineOfSight(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(LineOfSightResult.Unblocked(new HexCoordinates(1, 1), new HexCoordinates(4, 1)));
        _toHitCalculator.GetToHitNumber(Arg.Any<AttackScenario>(), Arg.Any<Weapon>(), Arg.Any<IBattleMap>())
            .Returns(8);

        // Act
        var results = _sut.EvaluateTargets(unit, path, potentialTargets);

        // Assert
        results.Count.ShouldBe(expectedConfigs);
    }
    
    [Fact]
    public void EvaluateTargets_WithTurnState_ShouldCacheResult()
    {
        // Arrange
        var unit = MovementEngineTests.CreateTestMech();
        var pilot = Substitute.For<IPilot>();
        pilot.Gunnery.Returns(4);
        unit.AssignPilot(pilot);
        
        // Unit at (1,1) Facing Bottom
        var unitPos = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var path = new MovementPath([new PathSegment(unitPos, unitPos, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }])], MovementType.Walk);
        unit.Move(path, null);

        // Enemy at rangeBracket
        var enemy = MovementEngineTests.CreateTestMech();
        var enemyPos = new HexPosition(new HexCoordinates(4, 1), HexDirection.Top);
        enemy.Move(new MovementPath([new PathSegment(enemyPos, enemyPos, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }])], MovementType.Walk), null);

        var weaponDef = new WeaponDefinition("TestLaser", 5, 1, new WeaponRange(0, 3, 6, 9), WeaponType.Energy, 100);
        var weapon = new TestWeapon(weaponDef);
        unit.Parts[PartLocation.RightArm].TryAddComponent(weapon);

        var potentialTargets = new List<IUnit> { enemy };

        _battleMap.GetLineOfSight(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(LineOfSightResult.Unblocked(new HexCoordinates(1, 1), new HexCoordinates(4, 1)));
        _toHitCalculator.GetToHitNumber(Arg.Any<AttackScenario>(), Arg.Any<Weapon>(), Arg.Any<IBattleMap>())
            .Returns(8);
        
        var turnState = Substitute.For<ITurnState>();
        turnState.TryGetTargetEvaluation(Arg.Any<TargetEvaluationKey>(), out Arg.Any<TargetEvaluationData>()).Returns(false);

        // Act
        _sut.EvaluateTargets(unit, path, potentialTargets, turnState);

        // Assert
        turnState.Received(1).AddTargetEvaluation(Arg.Any<TargetEvaluationKey>(), Arg.Any<TargetEvaluationData>());
        turnState.Received(1).TryGetTargetEvaluation(Arg.Any<TargetEvaluationKey>(), out Arg.Any<TargetEvaluationData>());
    }
    
    [Fact]
    public void EvaluateTargets_WithCachedResult_ShouldReturnCachedResult()
    {
        // Arrange
        var unit = MovementEngineTests.CreateTestMech();
        var pilot = Substitute.For<IPilot>();
        pilot.Gunnery.Returns(4);
        unit.AssignPilot(pilot);
        
        // Unit at (1,1) Facing Bottom 
        var unitPos = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var path = new MovementPath([new PathSegment(unitPos, unitPos, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }])], MovementType.Walk);
        unit.Move(path, null);

        // Enemy at rangeBracket
        var enemy = MovementEngineTests.CreateTestMech();
        var enemyPos = new HexPosition(new HexCoordinates(4, 1), HexDirection.Top);
        enemy.Move(new MovementPath([new PathSegment(enemyPos, enemyPos, [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }])], MovementType.Walk), null);

        var weaponDef = new WeaponDefinition("TestLaser", 5, 1, new WeaponRange(0, 3, 6, 9), WeaponType.Energy, 100);
        var weapon = new TestWeapon(weaponDef);
        unit.Parts[PartLocation.RightArm].TryAddComponent(weapon);
    
        var potentialTargets = new List<IUnit> { enemy };

        _battleMap.GetLineOfSight(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(LineOfSightResult.Unblocked(new HexCoordinates(1, 1), new HexCoordinates(4, 1)));
        _toHitCalculator.GetToHitNumber(Arg.Any<AttackScenario>(), Arg.Any<Weapon>(), Arg.Any<IBattleMap>())
            .Returns(8);
        
        var turnState = Substitute.For<ITurnState>();
        var cachedData = new TargetEvaluationData
        {
            TargetId = enemy.Id,
            ConfigurationScores = []
        };
        turnState.TryGetTargetEvaluation(Arg.Any<TargetEvaluationKey>(), out Arg.Any<TargetEvaluationData>())
            .ReturnsForAnyArgs(x => 
            {
                x[1] = cachedData;
                return true;
            });

        // Act
        var results = _sut.EvaluateTargets(unit, path, potentialTargets, turnState);

        // Assert
        results[0].ShouldBe(cachedData);
        turnState.DidNotReceive().AddTargetEvaluation(Arg.Any<TargetEvaluationKey>(), Arg.Any<TargetEvaluationData>());
    }

    [Fact]
    public void EvaluateTargets_PendingDfaTarget_UsesTheAirborneApproachPosition()
    {
        // Arrange
        var attacker = MovementEngineTests.CreateTestMech();
        var pilot = Substitute.For<IPilot>();
        pilot.Gunnery.Returns(4);
        attacker.AssignPilot(pilot);
        var attackerPos = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var attackerPath = new MovementPath(
            [new PathSegment(attackerPos, attackerPos,
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 0 }])],
            MovementType.Walk);
        attacker.Move(attackerPath, null);
        var weapon = new TestWeapon(new WeaponDefinition("TestLaser", 5, 1,
            new WeaponRange(0, 3, 6, 9), WeaponType.Energy, 100));
        attacker.Parts[PartLocation.RightArm].TryAddComponent(weapon);

        var target = CreatePendingDfaTarget(
            new HexPosition(new HexCoordinates(1, 3), HexDirection.Top, AirborneLevel: 5),
            new HexPosition(new HexCoordinates(1, 2), HexDirection.Top));

        _battleMap.GetLineOfSight(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(),
                Arg.Any<int>(), Arg.Any<int>())
            .Returns(LineOfSightResult.Unblocked(new HexCoordinates(1, 1), new HexCoordinates(1, 3)));
        _toHitCalculator.GetToHitNumber(Arg.Any<AttackScenario>(), Arg.Any<Weapon>(), Arg.Any<IBattleMap>())
            .Returns(8);

        // Act
        _sut.EvaluateTargets(attacker, attackerPath, [target]);

        // Assert - LOS and the scenario use the approach position, but HexesTraveled still comes
        // from MovementTaken (the full jump path).
        _battleMap.Received().GetLineOfSight(
            Arg.Any<HexPosition>(),
            Arg.Is<HexPosition>(p => p.Coordinates == new HexCoordinates(1, 3) && p.AirborneLevel == 5),
            Arg.Any<int>(),
            Arg.Any<int>());
        _toHitCalculator.Received().GetToHitNumber(
            Arg.Is<AttackScenario>(s => s.TargetPosition.Coordinates == new HexCoordinates(1, 3)
                                        && s.TargetPosition.AirborneLevel == 5),
            Arg.Any<Weapon>(),
            Arg.Any<IBattleMap>());
        target.MovementTaken!.HexesTraveled.ShouldBe(1,
            "the movement path is preserved for the TMM input");
    }

    [Fact]
    public void EvaluatePath_PendingDfaEnemy_UsesTheAirborneApproachPositionAsTheAttacker()
    {
        // Arrange
        var defender = Substitute.For<IUnit>();
        var defenderPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var defenderPath = MovementPath.CreateSingleSegmentPath(defenderPosition);

        var enemy = CreatePendingDfaTarget(
            new HexPosition(new HexCoordinates(1, 4), HexDirection.Top, AirborneLevel: 5),
            new HexPosition(new HexCoordinates(1, 5), HexDirection.Top));
        enemy.Parts[PartLocation.RightArm].TryAddComponent(new TestWeapon(
            new WeaponDefinition("TestLaser", 5, 1, new WeaponRange(0, 3, 6, 9), WeaponType.Energy, 100)));

        _battleMap.GetLineOfSight(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(),
                Arg.Any<int>(), Arg.Any<int>())
            .Returns(LineOfSightResult.Unblocked(new HexCoordinates(1, 4), new HexCoordinates(1, 1)));
        _toHitCalculator.GetToHitNumber(Arg.Any<AttackScenario>(), Arg.Any<Weapon>(), Arg.Any<IBattleMap>())
            .Returns(8);

        // Act
        _sut.EvaluatePath(defender, defenderPath, [enemy]);

        // Assert - the enemy fires from the approach hex while keeping the jump movement type
        _toHitCalculator.Received().GetToHitNumber(
            Arg.Is<AttackScenario>(s => s.AttackerPosition.Coordinates == new HexCoordinates(1, 4)
                                        && s.AttackerPosition.AirborneLevel == 5
                                        && s.AttackerMovementType == MovementType.Jump),
            Arg.Any<Weapon>(),
            Arg.Any<IBattleMap>());
    }

    [Fact]
    public void TargetEvaluationKey_DistinguishesGroundedAndAirborneTargetsAtTheSameCoordinates()
    {
        // Arrange
        var coordinates = new HexCoordinates(1, 3);
        var grounded = new TargetEvaluationKey(Guid.NewGuid(), new HexCoordinates(1, 1),
            HexDirection.Bottom, MovementType.Walk, Guid.NewGuid(), coordinates,
            HexDirection.Top);
        var airborne = grounded with { TargetAirborneLevel = 5 };

        // Act & Assert
        airborne.ShouldNotBe(grounded);
    }

    private static Mech CreatePendingDfaTarget(HexPosition approach, HexPosition declaredTarget)
    {
        var target = MovementEngineTests.CreateTestMech();
        target.RemoveFromBoard();
        target.Deploy(approach, null);
        target.Move(new MovementPath(
        [
            new PathSegment(approach, declaredTarget,
                [new JumpMovementCost { Value = 1 }])
        ], MovementType.Jump), null, true, approach);
        target.DeclarePhysicalAttack(new PhysicalAttackDeclarationData
        {
            AttackType = PhysicalAttackType.DFA,
            AttackerLimbs = [],
            TargetId = Guid.NewGuid()
        });
        return target;
    }

    private class TestWeapon(WeaponDefinition definition) : Weapon(definition);
}

using NSubstitute;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Shouldly;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Data.Game.Mechanics;

namespace Sanet.MakaMek.Bots.Tests.Models.DecisionEngines;

public class PositionEvaluatorTests
{
    private readonly IClientGame _game;
    private readonly IBattleMap _battleMap;
    private readonly IToHitCalculator _toHitCalculator;
    private readonly PositionEvaluator _sut;

    public PositionEvaluatorTests()
    {
        _game = Substitute.For<IClientGame>();
        _battleMap = Substitute.For<IBattleMap>();
        _toHitCalculator = Substitute.For<IToHitCalculator>();
        
        _game.BattleMap.Returns(_battleMap);
        _game.ToHitCalculator.Returns(_toHitCalculator);
        
        _sut = new PositionEvaluator(_game);
    }

    [Fact]
    public void EvaluatePath_WhenNoBattleMap_ShouldReturnZeroIndices()
    {
        // Arrange
        _game.BattleMap.Returns((IBattleMap?)null);
        var unit = Substitute.For<IUnit>();
        var path = MovementPath.CreateStandingStillPath(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));

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
        var path = MovementPath.CreateStandingStillPath(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        
        var enemy = MovementEngineTests.CreateTestMech();
        var enemyPos = new HexPosition(new HexCoordinates(1, 4), HexDirection.Top);
        enemy.Move(new MovementPath(new List<PathSegment>
        {
            new(enemyPos, enemyPos, 0)
        }, MovementType.Walk));
        
        // Set up enemy weapon
        var weaponDef = new WeaponDefinition("TestLaser", 5, 1, 0, 3, 6, 9, WeaponType.Energy, 100);
        var weapon = new TestWeapon(weaponDef);
        var part = enemy.Parts[PartLocation.RightArm];
        part.TryAddComponent(weapon);
        
        var enemies = new List<IUnit> { enemy };

        // Setup LoS
        _battleMap.HasLineOfSight(Arg.Any<HexCoordinates>(), Arg.Any<HexCoordinates>()).Returns(true);
        
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
        var path = MovementPath.CreateStandingStillPath(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        
        var enemy = MovementEngineTests.CreateTestMech();
        var enemyPos = new HexPosition(new HexCoordinates(1, 4), HexDirection.Top);
        enemy.Move(new MovementPath(new List<PathSegment>
        {
            new(enemyPos, enemyPos, 0)
        }, MovementType.Walk));
        
        // Set up enemy weapon
        var weaponDef = new WeaponDefinition("TestLaser", 5, 1, 0, 3, 6, 9, WeaponType.Energy, 100);
        var weapon = new TestWeapon(weaponDef);
        var part = enemy.Parts[PartLocation.RightArm];
        part.TryAddComponent(weapon);
        
        var enemies = new List<IUnit> { enemy };

        // Setup LoS - Obstruction
        _battleMap.HasLineOfSight(Arg.Any<HexCoordinates>(), Arg.Any<HexCoordinates>()).Returns(false);

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
        var path = MovementPath.CreateStandingStillPath(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        
        var enemy = MovementEngineTests.CreateTestMech();
        // 11 hexes away
        var enemyPos = new HexPosition(new HexCoordinates(1, 12), HexDirection.Top);
        enemy.Move(new MovementPath(new List<PathSegment>
        {
            new(enemyPos, enemyPos, 0)
        }, MovementType.Walk));
        
        var weaponDef = new WeaponDefinition("TestLaser", 5, 1, 0, 3, 6, 9, WeaponType.Energy, 100);
        var weapon = new TestWeapon(weaponDef);
        var part = enemy.Parts[PartLocation.RightArm];
        part.TryAddComponent(weapon);
        
        var enemies = new List<IUnit> { enemy };

        _battleMap.HasLineOfSight(Arg.Any<HexCoordinates>(), Arg.Any<HexCoordinates>()).Returns(true);

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
            new(unitPos, unitPos, 0)
        }, MovementType.Walk);
        unit.Move(path);
        
        var enemy = MovementEngineTests.CreateTestMech();
        var enemyPos = new HexPosition(new HexCoordinates(1, 4), HexDirection.Bottom);
        enemy.Move(new MovementPath(new List<PathSegment>
        {
            new(enemyPos, enemyPos, 0)
        }, MovementType.Walk));
        
        // Set up friendly weapon
        var weaponDef = new WeaponDefinition("TestLaser", 5, 1, 0, 3, 6, 9, WeaponType.Energy, 100);
        var weapon = new TestWeapon(weaponDef);
        var part = unit.Parts[PartLocation.RightArm];
        part.TryAddComponent(weapon);
        
        var enemies = new List<IUnit> { enemy };

        // Setup LoS
        _battleMap.HasLineOfSight(Arg.Any<HexCoordinates>(), Arg.Any<HexCoordinates>()).Returns(hasLos);
        
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
        // Unit at (1,1) Facing Top, Enemy at (1,4) is not in arc
        var unitPos = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var path = new MovementPath(new List<PathSegment>
        {
            new(unitPos, unitPos, 0)
        }, MovementType.Walk);
        unit.Move(path);
        
        var enemy = MovementEngineTests.CreateTestMech();
        var enemyPos = new HexPosition(new HexCoordinates(1, 4), HexDirection.Bottom);
        enemy.Move(new MovementPath(new List<PathSegment>
        {
            new(enemyPos, enemyPos, 0)
        }, MovementType.Walk));
        
        // Set up friendly weapon
        var weaponDef = new WeaponDefinition("TestLaser", 5, 1, 0, 3, 6, 9, WeaponType.Energy, 100);
        var weapon = new TestWeapon(weaponDef);
        var part = unit.Parts[PartLocation.RightArm];
        part.TryAddComponent(weapon);
        
        var enemies = new List<IUnit> { enemy };

        // Setup LoS
        _battleMap.HasLineOfSight(Arg.Any<HexCoordinates>(), Arg.Any<HexCoordinates>()).Returns(true);
        
        // Setup ToHit
        const int toHitNumber = 8;
        _toHitCalculator.GetToHitNumber(Arg.Any<AttackScenario>(), Arg.Any<Weapon>(), Arg.Any<IBattleMap>())
            .Returns(toHitNumber);
        
        var result = _sut.EvaluatePath(unit, path, enemies);

        // Assert
        result.OffensiveIndex.ShouldBe(0);
    }

    private class TestWeapon(WeaponDefinition definition) : Weapon(definition);
}

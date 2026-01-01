using NSubstitute;
using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Bots.Exceptions;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.Models.DecisionEngines;

public class MovementEngineTests
{
    private readonly IClientGame _clientGame;
    private readonly ITacticalEvaluator _tacticalEvaluator;
    private readonly IPlayer _player;
    private readonly IBattleMap _battleMap;
    private readonly MovementEngine _sut;

    public MovementEngineTests()
    {
        _clientGame = Substitute.For<IClientGame>();
        _tacticalEvaluator = Substitute.For<ITacticalEvaluator>();
        _player = Substitute.For<IPlayer>();
        _battleMap = Substitute.For<IBattleMap>();
        
        _clientGame.Id.Returns(Guid.NewGuid());
        _clientGame.BattleMap.Returns(_battleMap);
        _player.Id.Returns(Guid.NewGuid());
        _player.Name.Returns("Test Player");
        
        _sut = new MovementEngine(_clientGame, _tacticalEvaluator);
    }

    [Fact]
    public async Task MakeDecision_WhenAllUnitsHaveMoved_ShouldReturnEarly()
    {
        // Arrange
        var unit1 = CreateMockUnit(hasMoved: true);
        var unit2 = CreateMockUnit(hasMoved: true);
        _player.AliveUnits.Returns([unit1, unit2]);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        await _clientGame.DidNotReceive().MoveUnit(Arg.Any<MoveUnitCommand>());
    }
    
    [Fact]
    public async Task MakeDecision_WhenImmobileUnit_ShouldSkipTurn()
    {
        // Arrange
        var immobileUnit = CreateMockUnit(hasMoved: false, isDeployed: true, isImmobile: true);
        _player.AliveUnits.Returns([immobileUnit]);
        _clientGame.Players.Returns([_player]);
        
        MoveUnitCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.MoveUnit(Arg.Do<MoveUnitCommand>(cmd => { capturedCommand = cmd; commandCaptured = true; }));
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        commandCaptured.ShouldBeTrue();
        capturedCommand.PlayerId.ShouldBe(_player.Id);
        capturedCommand.UnitId.ShouldBe(immobileUnit.Id);
        capturedCommand.MovementType.ShouldBe(MovementType.StandingStill);
        capturedCommand.MovementPath.Count.ShouldBe(1);
    }

    [Fact]
    public async Task MakeDecision_WhenProneMechCanStandup_ShouldAttemptStandup()
    {
        // Arrange
        var proneMech = CreateProneMech(canStandup: true);
        _player.AliveUnits.Returns([proneMech]);
        _clientGame.Players.Returns([_player]);
        
        TryStandupCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.TryStandupUnit(Arg.Do<TryStandupCommand>(cmd => { capturedCommand = cmd; commandCaptured = true; }));
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        commandCaptured.ShouldBeTrue();
        capturedCommand.PlayerId.ShouldBe(_player.Id);
        capturedCommand.UnitId.ShouldBe(proneMech.Id);
        capturedCommand.GameOriginId.ShouldBe(_clientGame.Id);
        capturedCommand.MovementTypeAfterStandup.ShouldBe(MovementType.Walk);
        // NewFacing should be one of the valid directions (random)
        HexDirectionExtensions.AllDirections.ShouldContain(capturedCommand.NewFacing);
    }

    [Fact]
    public async Task MakeDecision_WhenProneMechCannotStandup_ShouldSkipTurn()
    {
        // Arrange
        var proneMech = CreateProneMech(canStandup: false);
        _player.AliveUnits.Returns([proneMech]);
        _clientGame.Players.Returns([_player]);
        
        MoveUnitCommand? capturedCommand = null;
        await _clientGame.MoveUnit(Arg.Do<MoveUnitCommand>(cmd => capturedCommand = cmd));
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        capturedCommand.ShouldNotBeNull();
        capturedCommand.Value.MovementType.ShouldBe(MovementType.StandingStill);
    }
    
    [Fact]
    public async Task MakeDecision_WhenNoBattleMap_ShouldSkipTurn() // Should we throw instead? 
    {
        // Arrange
        var unit = CreateMockUnit(hasMoved: false, isDeployed: true);
        _player.AliveUnits.Returns([unit]);
        _clientGame.BattleMap.Returns((IBattleMap?)null);
        _clientGame.Players.Returns([_player]);
        
        MoveUnitCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.MoveUnit(Arg.Do<MoveUnitCommand>(cmd => { capturedCommand = cmd; commandCaptured = true; }));
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        commandCaptured.ShouldBeTrue();
        capturedCommand.PlayerId.ShouldBe(_player.Id);
        capturedCommand.UnitId.ShouldBe(unit.Id);
        capturedCommand.MovementType.ShouldBe(MovementType.StandingStill);
        capturedCommand.MovementPath.Count.ShouldBe(1);
    }
    
    [Fact]
    public async Task MakeDecision_WhenNoReachableHexes_ShouldSkipTurn()
    {
        // Arrange
        var unit = CreateMockUnit(hasMoved: false, isDeployed: true);
        _player.AliveUnits.Returns([unit]);
        _clientGame.Players.Returns([_player]);
        
        // Mock empty reachable hexes for all movement types
        _battleMap.GetReachableHexes(Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(new List<(HexCoordinates coordinates, int cost)>());
        _battleMap.GetJumpReachableHexes(Arg.Any<HexCoordinates>(), Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(new List<HexCoordinates>());
        
        MoveUnitCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.MoveUnit(Arg.Do<MoveUnitCommand>(cmd => { capturedCommand = cmd; commandCaptured = true; }));
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        commandCaptured.ShouldBeTrue();
        capturedCommand.PlayerId.ShouldBe(_player.Id);
        capturedCommand.UnitId.ShouldBe(unit.Id);
        capturedCommand.MovementType.ShouldBe(MovementType.StandingStill);
        capturedCommand.MovementPath.Count.ShouldBe(1);
    }
    
    [Fact]
    public async Task MakeDecision_WhenValidConditions_ShouldMoveUnit()
    {
        // Arrange
        var unit = CreateMockUnit(hasMoved: false, isDeployed: true);
        _player.AliveUnits.Returns([unit]);
        _clientGame.Players.Returns([_player]);

        // Mock reachable hexes
        var targetHex = new HexCoordinates(2, 2);
        var reachableHexes = new List<(HexCoordinates coordinates, int cost)>
        {
            (targetHex, 1)
        };
        _battleMap.GetReachableHexes(Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(reachableHexes);
        
        // Mock path finding
        var targetPosition = new HexPosition(targetHex, HexDirection.Top);
        var pathSegment = new PathSegment(unit.Position!, targetPosition, 1);
        var movementPath = new MovementPath([pathSegment], MovementType.Walk);
        _battleMap.FindPath(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(), Arg.Any<MovementType>(), 
            Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(movementPath);
        
        // Mock position evaluator
        var positionScore = new PositionScore
        {
            Position = targetPosition,
            MovementType = MovementType.Walk,
            Path = movementPath,
            OffensiveIndex = 10,
            DefensiveIndex = 5,
            EnemiesInRearArc = 0
        };
        _tacticalEvaluator.EvaluatePath(unit, Arg.Any<MovementPath>(), Arg.Any<IReadOnlyList<IUnit>>())
            .Returns(positionScore);
        
        MoveUnitCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.MoveUnit(Arg.Do<MoveUnitCommand>(cmd => { capturedCommand = cmd; commandCaptured = true; }));
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        commandCaptured.ShouldBeTrue();
        capturedCommand.PlayerId.ShouldBe(_player.Id);
        capturedCommand.UnitId.ShouldBe(unit.Id);
        capturedCommand.MovementType.ShouldBe(MovementType.Walk);
        capturedCommand.GameOriginId.ShouldBe(_clientGame.Id);
    }
    
    [Fact]
    public async Task MakeDecision_ShouldSelectLrmBoatFirst()
    {
        // Arrange
        var lrmBoat = CreateMockUnit(hasMoved: false, id: Guid.NewGuid());
        ConfigureLrmBoat(lrmBoat);
        var scout = CreateMockUnit(hasMoved: false, id: Guid.NewGuid());
        ConfigureScout(scout);
        
        _player.AliveUnits.Returns([lrmBoat, scout]);
        _clientGame.Players.Returns([_player]);
        
        SetupValidMovement();
        
        MoveUnitCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.MoveUnit(Arg.Do<MoveUnitCommand>(cmd => { capturedCommand = cmd; commandCaptured = true; }));
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert - Should move LRM Boat (Priority 90) over Scout (Priority 20)
        commandCaptured.ShouldBeTrue();
        capturedCommand.UnitId.ShouldBe(lrmBoat.Id);
    }

    [Fact]
    public async Task MakeDecision_WhenWeLostInitiative_ShouldDelayBrawlers()
    {
        // Arrange - We move first (Lost Initiative) -> EnemyUnitsRemaining > 0
        var enemy = CreateMockUnit(hasMoved: false, id: Guid.NewGuid());
        var enemyPlayer = Substitute.For<IPlayer>();
        enemyPlayer.Id.Returns(Guid.NewGuid());
        enemyPlayer.AliveUnits.Returns([enemy]);
        
        var brawler = CreateMockUnit(hasMoved: false, id: Guid.NewGuid());
        ConfigureBrawler(brawler);
        var trooper = CreateMockUnit(hasMoved: false, id: Guid.NewGuid());
        // Trooper default priority 50. Brawler 30.
        // Lost Initiative: Brawler penalty -30 if enemies remain.
        // Brawler Final: 0. Trooper Final: 50.
        
        _player.AliveUnits.Returns([brawler, trooper]);
        _clientGame.Players.Returns([_player, enemyPlayer]);
        
        SetupValidMovement();
        
        MoveUnitCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.MoveUnit(Arg.Do<MoveUnitCommand>(cmd => { capturedCommand = cmd; commandCaptured = true; }));
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert - Should move Trooper (Higher Priority)
        commandCaptured.ShouldBeTrue();
        capturedCommand.UnitId.ShouldBe(trooper.Id);
    }

    [Fact]
    public async Task MakeDecision_WhenMultipleRoles_ShouldPrioritizeCorrectly()
    {
        // Arrange
        var lrmBoat = CreateMockUnit(hasMoved: false, id: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        ConfigureLrmBoat(lrmBoat);
        
        var defaultUnit = CreateMockUnit(hasMoved: false, id: Guid.Parse("00000000-0000-0000-0000-000000000002"));
        
        var brawler = CreateMockUnit(hasMoved: false, id: Guid.Parse("00000000-0000-0000-0000-000000000003"));
        ConfigureBrawler(brawler);
        
        var scout = CreateMockUnit(hasMoved: false, id: Guid.Parse("00000000-0000-0000-0000-000000000004"));
        ConfigureScout(scout);
        
        _player.AliveUnits.Returns([scout, brawler, defaultUnit, lrmBoat]); // Intentionally out of order
        _clientGame.Players.Returns([_player]);
        
        SetupValidMovement();
        
        MoveUnitCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.MoveUnit(Arg.Do<MoveUnitCommand>(cmd => { capturedCommand = cmd; commandCaptured = true; }));
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert - Should select LrmBoat (Priority 90)
        commandCaptured.ShouldBeTrue();
        capturedCommand.UnitId.ShouldBe(lrmBoat.Id);
    }

    [Fact]
    public async Task MakeDecision_WhenProneMech_ShouldMoveLast()// TODO:or first?
    {
        // Arrange
        // Create a real prone mech with destroyed gyro (can't stand up but not immobile)
        var proneMech = CreateProneMech(canStandup: true); // Create it able to stand initially
        // Destroy the gyro so it can't stand up
        var gyro = proneMech.GetAvailableComponents<Gyro>().FirstOrDefault();
        gyro.ShouldNotBeNull();
        gyro.Hit(); // First hit
        gyro.Hit(); // Second hit - destroys it
        
        var scout = CreateMockUnit(hasMoved: false, id: Guid.NewGuid());
        ConfigureScout(scout);
        
        _player.AliveUnits.Returns([proneMech, scout]);
        _clientGame.Players.Returns([_player]);
        
        SetupValidMovement();
        
        MoveUnitCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.MoveUnit(Arg.Do<MoveUnitCommand>(cmd => { capturedCommand = cmd; commandCaptured = true; }));
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert - Should move scout first (prone has priority 0)
        commandCaptured.ShouldBeTrue();
        capturedCommand.UnitId.ShouldBe(scout.Id);
    }

    [Fact]
    public async Task MakeDecision_WhenNoEnemiesRemaining_AllShouldGetBonus()
    {
        // Arrange - No enemies left to move
        var enemyMoved = CreateMockUnit(hasMoved: true, id: Guid.NewGuid());
        var enemyPlayer = Substitute.For<IPlayer>();
        enemyPlayer.Id.Returns(Guid.NewGuid());
        enemyPlayer.AliveUnits.Returns([enemyMoved]);
        
        var brawler = CreateMockUnit(hasMoved: false, id: Guid.NewGuid());
        ConfigureBrawler(brawler); // Base priority 30 + 30 bonus = 60
        
        var scout = CreateMockUnit(hasMoved: false, id: Guid.NewGuid());
        ConfigureScout(scout); // Base priority 20 + 30 bonus = 50
        
        _player.AliveUnits.Returns([scout, brawler]);
        _clientGame.Players.Returns([_player, enemyPlayer]);
        
        SetupValidMovement();
        
        MoveUnitCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.MoveUnit(Arg.Do<MoveUnitCommand>(cmd => { capturedCommand = cmd; commandCaptured = true; }));
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert - Brawler should move first (60 > 50 with initiative bonus)
        commandCaptured.ShouldBeTrue();
        capturedCommand.UnitId.ShouldBe(brawler.Id);
    }
    
    [Fact]
    public async Task ExecuteMoveForUnit_WhenMultipleMovementTypes_ShouldEvaluateAll()
    {
        // Arrange
        var mech = CreateTestMech();
        _player.AliveUnits.Returns([mech]);
        _clientGame.Players.Returns([_player]);
        
        var targetHex = new HexCoordinates(2, 2);
        var reachableHexes = new List<(HexCoordinates coordinates, int cost)> { (targetHex, 1) };
        var jumpReachableHexes = new List<HexCoordinates> { targetHex };
        
        _battleMap.GetReachableHexes(Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(reachableHexes);
        _battleMap.GetJumpReachableHexes(Arg.Any<HexCoordinates>(), Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(jumpReachableHexes);
        
        var targetPosition = new HexPosition(targetHex, HexDirection.Top);
        var pathSegment = new PathSegment(mech.Position!, targetPosition, 1);
        _battleMap.FindPath(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(), Arg.Any<MovementType>(), 
            Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(callInfo => new MovementPath([pathSegment], callInfo.ArgAt<MovementType>(2)));
        
        // Track which movement types were evaluated
        var evaluatedMovementTypes = new List<MovementType>();
        _tacticalEvaluator.EvaluatePath(mech, Arg.Any<MovementPath>(), Arg.Any<IReadOnlyList<IUnit>>())
            .Returns(callInfo =>
            {
                var path = callInfo.ArgAt<MovementPath>(1);
                evaluatedMovementTypes.Add(path.MovementType);
                return new PositionScore
                {
                    Position = targetPosition,
                    MovementType = path.MovementType,
                    Path = path,
                    OffensiveIndex = 10,
                    DefensiveIndex = 5,
                    EnemiesInRearArc = 0
                };
            });
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert - Should evaluate Walk, Run, and Jump
        evaluatedMovementTypes.ShouldContain(MovementType.Walk);
        evaluatedMovementTypes.ShouldContain(MovementType.Run);
        evaluatedMovementTypes.ShouldContain(MovementType.Jump);
    }

    [Fact]
    public async Task ExecuteMoveForUnit_WhenJumpIsBest_ShouldUseJump()
    {
        // Arrange
        var mech = CreateTestMech();
        _player.AliveUnits.Returns([mech]);
        _clientGame.Players.Returns([_player]);
        
        var targetHex = new HexCoordinates(2, 2);
        var reachableHexes = new List<(HexCoordinates coordinates, int cost)> { (targetHex, 1) };
        var jumpReachableHexes = new List<HexCoordinates> { targetHex };
        
        _battleMap.GetReachableHexes(Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(reachableHexes);
        _battleMap.GetJumpReachableHexes(Arg.Any<HexCoordinates>(), Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(jumpReachableHexes);
        
        var targetPosition = new HexPosition(targetHex, HexDirection.Top);
        var pathSegment = new PathSegment(mech.Position!, targetPosition, 1);
        _battleMap.FindPath(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(), Arg.Any<MovementType>(), 
            Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(callInfo => new MovementPath([pathSegment], callInfo.ArgAt<MovementType>(2)));
        
        // Mock evaluator to score Jump highest
        _tacticalEvaluator.EvaluatePath(mech, Arg.Any<MovementPath>(), Arg.Any<IReadOnlyList<IUnit>>())
            .Returns(callInfo =>
            {
                var path = callInfo.ArgAt<MovementPath>(1);
                var score = path.MovementType == MovementType.Jump ? 100 : 10;
                return new PositionScore
                {
                    Position = targetPosition,
                    MovementType = path.MovementType,
                    Path = path,
                    OffensiveIndex = score,
                    DefensiveIndex = 5,
                    EnemiesInRearArc = 0
                };
            });
        
        MoveUnitCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.MoveUnit(Arg.Do<MoveUnitCommand>(cmd => { capturedCommand = cmd; commandCaptured = true; }));
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        commandCaptured.ShouldBeTrue();
        capturedCommand.MovementType.ShouldBe(MovementType.Jump);
    }
    
    [Theory]
    [InlineData(10, 5, 20, 10, 1)]
    [InlineData(10, 5, 20, 5, 2)]
    public async Task ExecuteMoveForUnit_ShouldSelectOptimalPath(
        int offensiveIndex1,
        int defensiveIndex1,
        int offensiveIndex2,
        int defensiveIndex2,
        int optimalOption)
    {
        // Arrange
        var mech = CreateTestMech();
        _player.AliveUnits.Returns([mech]);
        _clientGame.Players.Returns([_player]);
        
        var targetHex1 = new HexCoordinates(2, 2);
        var targetHex2 = new HexCoordinates(3, 3);
        var reachableHexes = new List<(HexCoordinates coordinates, int cost)> { (targetHex1, 1), (targetHex2, 2) };
        _battleMap.GetReachableHexes(Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(reachableHexes);
        
        var targetPosition1 = new HexPosition(targetHex1, HexDirection.Top);
        var targetPosition2 = new HexPosition(targetHex2, HexDirection.Top);
        var pathSegment1 = new PathSegment(mech.Position!, targetPosition1, 1);
        var pathSegment2 = new PathSegment(mech.Position!, targetPosition2, 2);
        _battleMap.FindPath(Arg.Any<HexPosition>(), targetPosition1, MovementType.Walk, 
                Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(callInfo => new MovementPath([pathSegment1], callInfo.ArgAt<MovementType>(2)));
        _battleMap.FindPath(Arg.Any<HexPosition>(), targetPosition2, Arg.Any<MovementType>(), 
                Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(callInfo => new MovementPath([pathSegment2], callInfo.ArgAt<MovementType>(2)));
        
        // Mock evaluator to score options according to test data
        _tacticalEvaluator.EvaluatePath(mech, Arg.Any<MovementPath>(), Arg.Any<IReadOnlyList<IUnit>>())
            .Returns(callInfo =>
            {
                var path = callInfo.ArgAt<MovementPath>(1);
                
                var offensiveIndex = path.Destination.Coordinates.ToData() == targetHex1.ToData() ? offensiveIndex1 : offensiveIndex2;
                var defensiveIndex = path.Destination.Coordinates.ToData() == targetHex1.ToData() ? defensiveIndex1 : defensiveIndex2;
                return new PositionScore
                {
                    Position = path.Destination,
                    MovementType = path.MovementType,
                    Path = path,
                    OffensiveIndex = offensiveIndex,
                    DefensiveIndex = defensiveIndex,
                    EnemiesInRearArc = 0
                };
            });
        
        MoveUnitCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.MoveUnit(Arg.Do<MoveUnitCommand>(cmd => { capturedCommand = cmd; commandCaptured = true; }));
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        commandCaptured.ShouldBeTrue();
        capturedCommand.MovementPath.Count.ShouldBe(1);
        capturedCommand.MovementPath[^1].To.Coordinates
            .ShouldBe(optimalOption == 1 ? targetHex1.ToData() : targetHex2.ToData());
    }

    [Fact]
    public async Task ExecuteMoveForUnit_WhenAllScoresTied_ShouldPreferMoreHexesTraveled()
    {
        // Arrange
        var mech = CreateTestMech();
        _player.AliveUnits.Returns([mech]);
        _clientGame.Players.Returns([_player]);

        var targetHex1 = new HexCoordinates(2, 2);
        var targetHex2 = new HexCoordinates(3, 3);
        var reachableHexes = new List<(HexCoordinates coordinates, int cost)> { (targetHex1, 1), (targetHex2, 1) };
        _battleMap.GetReachableHexes(Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(reachableHexes);

        var targetPosition1 = new HexPosition(targetHex1, HexDirection.Top);
        var targetPosition2 = new HexPosition(targetHex2, HexDirection.Top);

        // Path 1: shorter
        var pathSegment1 = new PathSegment(mech.Position!, targetPosition1, 1);
        var path1 = new MovementPath([pathSegment1], MovementType.Walk);

        // Path 2: longer (two segments, different hexes)
        var intermediate = new HexPosition(new HexCoordinates(2, 3), HexDirection.Top);
        var pathSegment2A = new PathSegment(mech.Position!, intermediate, 1);
        var pathSegment2B = new PathSegment(intermediate, targetPosition2, 1);
        var path2 = new MovementPath([pathSegment2A, pathSegment2B], MovementType.Walk);

        _battleMap.FindPath(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(), MovementType.Walk,
                Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(callInfo =>
            {
                var destination = callInfo.ArgAt<HexPosition>(1);
                return destination.Coordinates.ToData() == targetHex1.ToData() ? path1 : path2;
            });

        _tacticalEvaluator.EvaluatePath(mech, Arg.Any<MovementPath>(), Arg.Any<IReadOnlyList<IUnit>>())
            .Returns(callInfo =>
            {
                var path = callInfo.ArgAt<MovementPath>(1);

                // Tied primary scores
                return new PositionScore
                {
                    Position = path.Destination,
                    MovementType = path.MovementType,
                    Path = path,
                    OffensiveIndex = 0,
                    DefensiveIndex = 0,
                    EnemiesInRearArc = 0
                };
            });

        MoveUnitCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.MoveUnit(Arg.Do<MoveUnitCommand>(cmd => { capturedCommand = cmd; commandCaptured = true; }));

        // Act
        await _sut.MakeDecision(_player);

        // Assert - should pick path 2 as it includes more hexes
        commandCaptured.ShouldBeTrue();
        capturedCommand.MovementPath[^1].To.Coordinates.ShouldBe(targetHex2.ToData());
    }

    [Fact]
    public async Task ExecuteMoveForUnit_ShouldPreferFrontFacingPositions_WhenRearArcExposureDiffers()
    {
        // Arrange
        var mech = CreateTestMech();
        _player.AliveUnits.Returns([mech]);
        _clientGame.Players.Returns([_player]);

        var targetHex1 = new HexCoordinates(2, 2);
        var targetHex2 = new HexCoordinates(3, 3);
        var reachableHexes = new List<(HexCoordinates coordinates, int cost)> { (targetHex1, 1), (targetHex2, 1) };
        _battleMap.GetReachableHexes(Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(reachableHexes);

        var targetPosition1 = new HexPosition(targetHex1, HexDirection.Top);
        var targetPosition2 = new HexPosition(targetHex2, HexDirection.Top);
        var pathSegment1 = new PathSegment(mech.Position!, targetPosition1, 1);
        var pathSegment2 = new PathSegment(mech.Position!, targetPosition2, 1);
        var path1 = new MovementPath([pathSegment1], MovementType.Walk);
        var path2 = new MovementPath([pathSegment2], MovementType.Walk);

        _battleMap.FindPath(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(), MovementType.Walk,
                Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(callInfo =>
            {
                var destination = callInfo.ArgAt<HexPosition>(1);
                return destination.Coordinates.ToData() == targetHex1.ToData() ? path1 : path2;
            });

        _tacticalEvaluator.EvaluatePath(mech, Arg.Any<MovementPath>(), Arg.Any<IReadOnlyList<IUnit>>())
            .Returns(callInfo =>
            {
                var path = callInfo.ArgAt<MovementPath>(1);

                // Both defensive/offensive are equal, only rear-arc differs
                var enemiesInRearArc = path.Destination.Coordinates.ToData() == targetHex1.ToData() ? 2 : 1;
                return new PositionScore
                {
                    Position = path.Destination,
                    MovementType = path.MovementType,
                    Path = path,
                    OffensiveIndex = 0,
                    DefensiveIndex = 0,
                    EnemiesInRearArc = enemiesInRearArc
                };
            });

        MoveUnitCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.MoveUnit(Arg.Do<MoveUnitCommand>(cmd => { capturedCommand = cmd; commandCaptured = true; }));

        // Act
        await _sut.MakeDecision(_player);

        // Assert
        commandCaptured.ShouldBeTrue();
        capturedCommand.MovementPath[^1].To.Coordinates.ShouldBe(targetHex2.ToData());
    }
    
    [Fact]
    public async Task ExecuteMoveForUnit_WhenNoCandidateScores_ShouldSkipTurn()
    {
        // Arrange
        var unit = CreateMockUnit(hasMoved: false, isDeployed: true);
        _player.AliveUnits.Returns([unit]);
        _clientGame.Players.Returns([_player]);
        
        // Mock empty reachable hexes (no candidates)
        _battleMap.GetReachableHexes(Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(new List<(HexCoordinates coordinates, int cost)>());
        
        MoveUnitCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.MoveUnit(Arg.Do<MoveUnitCommand>(cmd => { capturedCommand = cmd; commandCaptured = true; }));
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        commandCaptured.ShouldBeTrue();
        capturedCommand.MovementType.ShouldBe(MovementType.StandingStill);
    }

    [Fact]
    public async Task MakeDecision_WhenBotDecisionException_ShouldRethrow()
    {
        // Arrange
        _player.AliveUnits.Returns([]);
        _clientGame.Players.Returns([_player]);
        
        // Simulate SkipTurn being called with no valid units (will throw BotDecisionException)
        var unit = CreateMockUnit(hasMoved: false, isDeployed: false);
        unit.Position.Returns((HexPosition?)null);
        _player.AliveUnits.Returns([unit]);
        
        // Act & Assert
        var exception = await Should.ThrowAsync<BotDecisionException>(async () =>
        {
            await _sut.MakeDecision(_player);
        });
        
        exception.DecisionEngineType.ShouldBe(nameof(MovementEngine));
        exception.PlayerId.ShouldBe(_player.Id);
    }

    [Fact]
    public async Task MakeDecision_WhenUnexpectedException_ShouldSkipTurn()
    {
        // Arrange
        var unit = CreateMockUnit(hasMoved: false, isDeployed: true);
        _player.AliveUnits.Returns([unit]);
        _clientGame.Players.Returns([_player]);
        
        // Set up the battle map to return valid paths
        var targetHex = new HexCoordinates(2, 2);
        var reachableHexes = new List<(HexCoordinates coordinates, int cost)> { (targetHex, 1) };
        
        _battleMap.GetReachableHexes(Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(reachableHexes);
        
        var targetPosition = new HexPosition(targetHex, HexDirection.Top);
        var startPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var pathSegment = new PathSegment(startPosition, targetPosition, 1);
        var movementPath = new MovementPath([pathSegment], MovementType.Walk);
        
        _battleMap.FindPath(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(), Arg.Any<MovementType>(), 
            Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(movementPath);
        
        // Mock evaluator to throw an unexpected exception (this must come AFTER battle map setup)
        _tacticalEvaluator.EvaluatePath(Arg.Any<IUnit>(), Arg.Any<MovementPath>(), Arg.Any<IReadOnlyList<IUnit>>())
            .Returns<Task<PositionScore>>(_ => throw new InvalidOperationException("Unexpected error"));
        
        MoveUnitCommand capturedCommand = default;
        var commandCaptured = false;
        await _clientGame.MoveUnit(Arg.Do<MoveUnitCommand>(cmd => { capturedCommand = cmd; commandCaptured = true; }));
        
        // Act - Should not throw, should gracefully skip turn
        await _sut.MakeDecision(_player);
        
        // Assert
        commandCaptured.ShouldBeTrue();
        capturedCommand.MovementType.ShouldBe(MovementType.StandingStill);
    }
    
    private void SetupValidMovement()
    {
        var targetHex = new HexCoordinates(2, 2);
        var reachableHexes = new List<(HexCoordinates coordinates, int cost)> { (targetHex, 1) };
        
        _battleMap.GetReachableHexes(Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(reachableHexes);
        
        var targetPosition = new HexPosition(targetHex, HexDirection.Top);
        var startPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var pathSegment = new PathSegment(startPosition, targetPosition, 1);
        var movementPath = new MovementPath([pathSegment], MovementType.Walk);
        
        _battleMap.FindPath(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(), Arg.Any<MovementType>(), 
            Arg.Any<int>(), Arg.Any<IReadOnlySet<HexCoordinates>>())
            .Returns(movementPath);
        
        var positionScore = new PositionScore
        {
            Position = targetPosition,
            MovementType = MovementType.Walk,
            Path = movementPath,
            OffensiveIndex = 10,
            DefensiveIndex = 5,
            EnemiesInRearArc = 0
        };
        
        _tacticalEvaluator.EvaluatePath(Arg.Any<IUnit>(), Arg.Any<MovementPath>(), Arg.Any<IReadOnlyList<IUnit>>())
            .Returns(positionScore);
    }

    private static void ConfigureLrmBoat(IUnit unit)
    {
        // Mock 20 LRM tubes
        var lrm20 = new Lrm20();
        unit.GetAvailableComponents<Weapon>().Returns([lrm20]);
    }

    private static void ConfigureScout(IUnit unit)
    {
        unit.GetAvailableComponents<Weapon>().Returns([]);
        unit.GetMovementPoints(MovementType.Walk).Returns(7);
        unit.GetMovementPoints(MovementType.Jump).Returns(0);
    }
    
    private static void ConfigureBrawler(IUnit unit)
    {
        unit.GetAvailableComponents<Weapon>().Returns([]);
        unit.GetMovementPoints(MovementType.Walk).Returns(3);
        unit.GetMovementPoints(MovementType.Jump).Returns(0);
    }

    private static IUnit CreateMockUnit(bool hasMoved, bool isDeployed = true, bool isImmobile = false, Guid? id = null)
    {
        var unit = Substitute.For<IUnit>();
        unit.Id.Returns(id ?? Guid.NewGuid());
        unit.HasMoved.Returns(hasMoved);
        unit.IsImmobile.Returns(isImmobile);
        unit.IsDeployed.Returns(isDeployed);
        unit.GetMovementPoints(MovementType.Walk).Returns(4);
        unit.GetMovementPoints(MovementType.Run).Returns(6);
        unit.GetMovementPoints(MovementType.Jump).Returns(0);
        unit.GetAvailableComponents<Weapon>().Returns([]);
        
        // Mock status
        unit.Status.Returns(isImmobile ? UnitStatus.Immobile : UnitStatus.Active);
        
        if (isDeployed)
        {
            var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
            unit.Position.Returns(position);
        }
        else
        {
            unit.Position.Returns((HexPosition?)null);
        }
        
        return unit;
    }

    private static Mech CreateProneMech(bool canStandup)
    {
        var mech = CreateTestMech();
        mech.SetProne();
        
        if (!canStandup)
        {
            // Shutdown the mech so it can't stand up
            var shutdownData = new ShutdownData { Reason = ShutdownReason.Voluntary, Turn = 1 };
            mech.Shutdown(shutdownData);
        }
        
        return mech;
    }

    internal static Mech CreateTestMech()
    {
        var parts = CreateBasicPartsData(engineRating: 200);
        var centerTorso = parts.Single(p => p.Location == PartLocation.CenterTorso);
        
        // Add jump jets to enable jumping
        var jumpJetData = new ComponentData
        {
            Type = MakaMekComponent.JumpJet,
            Assignments = [new LocationSlotAssignment(PartLocation.CenterTorso, 3, 1)]
        };
        centerTorso.TryAddComponent(new JumpJets(jumpJetData)).ShouldBeTrue();
        
        var mech = new Mech("Test Jumper", "TST-1J", 50, parts);
        var pilot = new MechWarrior("Test", "Pilot");
        mech.AssignPilot(pilot);
        
        // Deploy the mech
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        mech.Deploy(position);
        
        return mech;
    }

    private static List<UnitPart> CreateBasicPartsData(int engineRating = 100)
    {
        var engineData = new ComponentData
        {
            Type = MakaMekComponent.Engine,
            Assignments =
            [
                new LocationSlotAssignment(PartLocation.CenterTorso, 0, 3),
                new LocationSlotAssignment(PartLocation.CenterTorso, 7, 3)
            ],
            SpecificData = new EngineStateData(EngineType.Fusion, engineRating)
        };
        var centerTorso = new CenterTorso("CenterTorso", 31, 10, 6);
        centerTorso.TryAddComponent(new Engine(engineData), [0, 1, 2, 7, 8, 9]).ShouldBeTrue();
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
}

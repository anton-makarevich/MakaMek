using NSubstitute;
using Sanet.MakaMek.Bots.Exceptions;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.Models.DecisionEngines;

public class MovementEngineTests
{
    private readonly IClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly IBattleMap _battleMap;
    private readonly MovementEngine _sut;

    public MovementEngineTests()
    {
        _clientGame = Substitute.For<IClientGame>();
        _player = Substitute.For<IPlayer>();
        _battleMap = Substitute.For<IBattleMap>();
        
        _clientGame.Id.Returns(Guid.NewGuid());
        _clientGame.BattleMap.Returns(_battleMap);
        _player.Id.Returns(Guid.NewGuid());
        _player.Name.Returns("Test Player");
        
        _sut = new MovementEngine(_clientGame);
    }

    [Fact]
    public async Task MakeDecision_WhenNoUnmovedUnits_ShouldThrowBotDecisionException()
    {
        // Arrange
        var movedUnit = CreateMockUnit(hasMoved: true);
        _player.AliveUnits.Returns([movedUnit]);
        
        // Act & Assert
        var exception = await Should.ThrowAsync<BotDecisionException>(async () => await _sut.MakeDecision(_player));
        exception.DecisionEngineType.ShouldBe(nameof(MovementEngine));
        exception.PlayerId.ShouldBe(_player.Id);
    }

    [Fact]
    public async Task MakeDecision_WhenUnitIsImmobile_ShouldSendStandingStillCommand()
    {
        // Arrange
        var immobileUnit = CreateMockUnit(hasMoved: false, isImmobile: true);
        _player.AliveUnits.Returns([immobileUnit]);
        _clientGame.Players.Returns([_player]);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert - immobile units can still send StandingStill command (matching UI behavior)
        await _clientGame.Received(1).MoveUnit(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.PlayerId == _player.Id &&
            cmd.UnitId == immobileUnit.Id &&
            cmd.MovementType == MovementType.StandingStill &&
            cmd.MovementPath.Count == 0));
    }

    [Fact]
    public async Task MakeDecision_WhenNoBattleMap_ShouldSendStandingStillCommand()
    {
        // Arrange
        var unit = CreateMockUnit(hasMoved: false, isDeployed: true);
        _player.AliveUnits.Returns([unit]);
        _clientGame.BattleMap.Returns((BattleMap?)null);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        await _clientGame.Received(1).MoveUnit(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.PlayerId == _player.Id &&
            cmd.UnitId == unit.Id &&
            cmd.MovementType == MovementType.StandingStill &&
            cmd.MovementPath.Count == 0));
    }

    [Fact]
    public async Task MakeDecision_WhenNoReachableHexes_ShouldSendStandingStillCommand()
    {
        // Arrange
        var unit = CreateMockUnit(hasMoved: false, isDeployed: true); 
        _player.AliveUnits.Returns([unit]);
        _clientGame.Players.Returns([_player]);
        unit.GetMovementPoints(MovementType.Walk).Returns(0);
        unit.GetMovementPoints(MovementType.Jump).Returns(0);

        // Act
        await _sut.MakeDecision(_player);

        // Assert
        await _clientGame.Received(1).MoveUnit(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.PlayerId == _player.Id &&
            cmd.UnitId == unit.Id &&
            cmd.MovementType == MovementType.StandingStill &&
            cmd.MovementPath.Count == 0));
    }

    [Fact]
    public async Task MakeDecision_WhenValidConditions_ShouldMoveUnit()
    {
        // Arrange
        var unit = CreateMockUnit(hasMoved: false, isDeployed: true);
        _player.AliveUnits.Returns([unit]);
        _clientGame.Players.Returns([_player]);

        // Mock reachable hexes
        var reachableHexes = new List<(HexCoordinates coordinates, int cost)>
        {
            (new HexCoordinates(2, 2), 1)
        };
        _battleMap.GetReachableHexes(Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<IEnumerable<HexCoordinates>>())
            .Returns(reachableHexes);
        
        // Mock path finding
        var pathSegment = new PathSegment(new HexPosition(1, 1, HexDirection.Top), new HexPosition(2, 2, HexDirection.Top), 1);
        _battleMap.FindPath(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<IEnumerable<HexCoordinates>>())
            .Returns([pathSegment]);

        // Act
        await _sut.MakeDecision(_player);

        // Assert
        await _clientGame.Received(1).MoveUnit(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.PlayerId == _player.Id &&
            cmd.UnitId == unit.Id &&
            (cmd.MovementType == MovementType.Walk || cmd.MovementType == MovementType.Run)));
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
        
        // Mock a map and reachable hexes to avoid null checks failing
        var reachableHexes = new List<(HexCoordinates coordinates, int cost)> { (new HexCoordinates(2, 2), 1) };
        _battleMap.GetReachableHexes(Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<IEnumerable<HexCoordinates>>())
            .Returns(reachableHexes);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        // Should move LRM Boat (Priority 90) over Scout (Priority 20 + Initiative Adjustment)
        await _clientGame.Received(1).MoveUnit(Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == lrmBoat.Id));
    }

    [Fact]
    public async Task MakeDecision_WhenWeLostInitiative_ShouldDelayBrawlers()
    {
        // Arrange
        // We move first (Lost Initiative) -> EnemyUnitsRemaining > 0 (simulated by existing logic finding enemies)
        var enemy = CreateMockUnit(hasMoved: false, id: Guid.NewGuid());
        var enemyPlayer = Substitute.For<IPlayer>();
        enemyPlayer.Id.Returns(Guid.NewGuid());
        enemyPlayer.AliveUnits.Returns([enemy]);
        _clientGame.Players.Returns([_player, enemyPlayer]);

        var brawler = CreateMockUnit(hasMoved: false, id: Guid.NewGuid());
        ConfigureBrawler(brawler); 
        var trooper = CreateMockUnit(hasMoved: false, id: Guid.NewGuid());
        // Trooper default priority 50. Brawler 30.
        // Lost Initiative: Brawler penalty -30 if enemies remain.
        // Brawler Final: 0. Trooper Final: 50.
        
        _player.AliveUnits.Returns([brawler, trooper]);
        
        var reachableHexes = new List<(HexCoordinates coordinates, int cost)> { (new HexCoordinates(2, 2), 1) };
        _battleMap.GetReachableHexes(Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<IEnumerable<HexCoordinates>>())
            .Returns(reachableHexes);

        // Act
        await _sut.MakeDecision(_player);

        // Assert
        // Should move Trooper (Higher Priority)
        await _clientGame.Received(1).MoveUnit(Arg.Is<MoveUnitCommand>(cmd => cmd.UnitId == trooper.Id));
    }

    private void ConfigureLrmBoat(IUnit unit)
    {
        // Mock 20 LRM tubes
        var lrm20 = new Lrm20();
        unit.GetAvailableComponents<Weapon>().Returns([lrm20]);
    }

    private void ConfigureScout(IUnit unit)
    {
        unit.GetAvailableComponents<Weapon>().Returns([]);
        unit.GetMovementPoints(MovementType.Walk).Returns(7);
        unit.GetMovementPoints(MovementType.Jump).Returns(0);
    }
    
    private void ConfigureBrawler(IUnit unit)
    {
        unit.GetAvailableComponents<Weapon>().Returns([]);
        unit.GetMovementPoints(MovementType.Walk).Returns(3);
        unit.GetMovementPoints(MovementType.Jump).Returns(0);
    }

    private IUnit CreateMockUnit(bool hasMoved, bool isDeployed = true, bool isImmobile = false, Guid? id = null)
    {
        var unit = Substitute.For<IUnit>();
        unit.Id.Returns(id ?? Guid.NewGuid());
        unit.HasMoved.Returns(hasMoved);
        unit.IsImmobile.Returns(isImmobile);
        unit.IsDeployed.Returns(isDeployed);
        unit.GetMovementPoints(MovementType.Walk).Returns(4);
        unit.GetMovementPoints(MovementType.Run).Returns(6);
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
}


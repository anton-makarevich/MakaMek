using NSubstitute;
using Sanet.MakaMek.Bots.DecisionEngines;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Tests.Utils;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.DecisionEngines;

public class MovementEngineTests
{
    private readonly ClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly MovementEngine _sut;
    private readonly BattleMap _battleMap;

    public MovementEngineTests()
    {
        _clientGame = Substitute.For<ClientGame>();
        _player = Substitute.For<IPlayer>();
        _battleMap = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        
        _clientGame.BattleMap.Returns(_battleMap);
        _clientGame.Id.Returns(Guid.NewGuid());
        _player.Id.Returns(Guid.NewGuid());
        
        _sut = new MovementEngine(_clientGame, _player, BotDifficulty.Easy);
    }

    [Fact]
    public async Task MakeDecision_ShouldMoveUnit_WhenUnmovedUnitExists()
    {
        // Arrange
        var unit = Substitute.For<Unit>();
        unit.Id.Returns(Guid.NewGuid());
        unit.HasMoved.Returns(false);
        unit.IsImmobile.Returns(false);
        unit.Position.Returns(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));
        unit.GetMovementPoints(MovementType.Walk).Returns(5);
        unit.GetMovementPoints(MovementType.Run).Returns(7);
        
        var aliveUnits = new List<Unit> { unit };
        _player.AliveUnits.Returns(aliveUnits);
        _clientGame.Players.Returns(new List<IPlayer> { _player });
        _clientGame.MoveUnit(Arg.Any<MoveUnitCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.Received(1).MoveUnit(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.UnitId == unit.Id &&
            cmd.PlayerId == _player.Id &&
            cmd.GameOriginId == _clientGame.Id));
    }

    [Fact]
    public async Task MakeDecision_ShouldNotMoveUnit_WhenAllUnitsMoved()
    {
        // Arrange
        var unit = Substitute.For<Unit>();
        unit.HasMoved.Returns(true);
        
        var aliveUnits = new List<Unit> { unit };
        _player.AliveUnits.Returns(aliveUnits);

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.DidNotReceive().MoveUnit(Arg.Any<MoveUnitCommand>());
    }

    [Fact]
    public async Task MakeDecision_ShouldNotMoveUnit_WhenUnitIsImmobile()
    {
        // Arrange
        var unit = Substitute.For<Unit>();
        unit.HasMoved.Returns(false);
        unit.IsImmobile.Returns(true);
        
        var aliveUnits = new List<Unit> { unit };
        _player.AliveUnits.Returns(aliveUnits);

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.DidNotReceive().MoveUnit(Arg.Any<MoveUnitCommand>());
    }

    [Fact]
    public async Task MakeDecision_ShouldAttemptStandup_WhenMechIsProne()
    {
        // Arrange
        var mech = Substitute.For<Mech>();
        mech.Id.Returns(Guid.NewGuid());
        mech.HasMoved.Returns(false);
        mech.IsImmobile.Returns(false);
        mech.IsProne.Returns(true);
        mech.CanStandup().Returns(true);
        mech.Position.Returns(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));
        
        var aliveUnits = new List<Unit> { mech };
        _player.AliveUnits.Returns(aliveUnits);
        _clientGame.TryStandupUnit(Arg.Any<TryStandupCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.Received(1).TryStandupUnit(Arg.Is<TryStandupCommand>(cmd =>
            cmd.UnitId == mech.Id &&
            cmd.PlayerId == _player.Id &&
            cmd.GameOriginId == _clientGame.Id));
    }

    [Fact]
    public async Task MakeDecision_ShouldSelectWalkOrRun_WhenMoving()
    {
        // Arrange
        var unit = Substitute.For<Unit>();
        unit.Id.Returns(Guid.NewGuid());
        unit.HasMoved.Returns(false);
        unit.IsImmobile.Returns(false);
        unit.Position.Returns(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));
        unit.GetMovementPoints(MovementType.Walk).Returns(5);
        unit.GetMovementPoints(MovementType.Run).Returns(7);
        
        var aliveUnits = new List<Unit> { unit };
        _player.AliveUnits.Returns(aliveUnits);
        _clientGame.Players.Returns(new List<IPlayer> { _player });
        _clientGame.MoveUnit(Arg.Any<MoveUnitCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.Received(1).MoveUnit(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.MovementType == MovementType.Walk || cmd.MovementType == MovementType.Run));
    }

    [Fact]
    public async Task MakeDecision_ShouldMoveWithEmptyPath_WhenNoValidDestination()
    {
        // Arrange
        var unit = Substitute.For<Unit>();
        unit.Id.Returns(Guid.NewGuid());
        unit.HasMoved.Returns(false);
        unit.IsImmobile.Returns(false);
        unit.Position.Returns(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));
        unit.GetMovementPoints(MovementType.Walk).Returns(0); // No movement points
        unit.GetMovementPoints(MovementType.Run).Returns(0);
        
        var aliveUnits = new List<Unit> { unit };
        _player.AliveUnits.Returns(aliveUnits);
        _clientGame.Players.Returns(new List<IPlayer> { _player });
        _clientGame.MoveUnit(Arg.Any<MoveUnitCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.Received(1).MoveUnit(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.MovementPath.Count == 0));
    }

    [Fact]
    public async Task MakeDecision_ShouldHandleException_WhenMovementFails()
    {
        // Arrange
        var unit = Substitute.For<Unit>();
        unit.Id.Returns(Guid.NewGuid());
        unit.HasMoved.Returns(false);
        unit.IsImmobile.Returns(false);
        unit.Position.Returns(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));
        unit.GetMovementPoints(MovementType.Walk).Returns(5);
        
        var aliveUnits = new List<Unit> { unit };
        _player.AliveUnits.Returns(aliveUnits);
        _clientGame.Players.Returns(new List<IPlayer> { _player });
        _clientGame.MoveUnit(Arg.Any<MoveUnitCommand>()).Returns(Task.FromException<bool>(new Exception("Test exception")));

        // Act & Assert - should not throw
        await _sut.MakeDecision();
    }

    [Fact]
    public async Task MakeDecision_ShouldNotThrow_WhenBattleMapIsNull()
    {
        // Arrange
        _clientGame.BattleMap.Returns((BattleMap?)null);
        
        var unit = Substitute.For<Unit>();
        unit.HasMoved.Returns(false);
        unit.IsImmobile.Returns(false);
        unit.Position.Returns(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));
        
        _player.AliveUnits.Returns(new List<Unit> { unit });

        // Act & Assert - should not throw
        await _sut.MakeDecision();
    }

    [Fact]
    public async Task MakeDecision_ShouldNotThrow_WhenUnitPositionIsNull()
    {
        // Arrange
        var unit = Substitute.For<Unit>();
        unit.HasMoved.Returns(false);
        unit.IsImmobile.Returns(false);
        unit.Position.Returns((HexPosition?)null);
        
        _player.AliveUnits.Returns(new List<Unit> { unit });

        // Act & Assert - should not throw
        await _sut.MakeDecision();
    }
}


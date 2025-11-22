using NSubstitute;
using Sanet.MakaMek.Bots.DecisionEngines;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.DecisionEngines;

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
        
        _sut = new MovementEngine(_clientGame, _player, BotDifficulty.Easy);
    }

    [Fact]
    public async Task MakeDecision_WhenNoUnmovedUnits_ShouldNotMoveAnything()
    {
        // Arrange
        var movedUnit = CreateMockUnit(hasMoved: true);
        _player.AliveUnits.Returns([movedUnit]);
        
        // Act
        await _sut.MakeDecision();
        
        // Assert
        await _clientGame.DidNotReceive().MoveUnit(Arg.Any<MoveUnitCommand>());
    }

    [Fact]
    public async Task MakeDecision_WhenUnitNotDeployed_ShouldNotMoveAnything()
    {
        // Arrange
        var undeployedUnit = CreateMockUnit(hasMoved: false, isDeployed: false);
        _player.AliveUnits.Returns([undeployedUnit]);
        
        // Act
        await _sut.MakeDecision();
        
        // Assert
        await _clientGame.DidNotReceive().MoveUnit(Arg.Any<MoveUnitCommand>());
    }

    [Fact]
    public async Task MakeDecision_WhenNoBattleMap_ShouldNotMoveAnything()
    {
        // Arrange
        var unit = CreateMockUnit(hasMoved: false, isDeployed: true);
        _player.AliveUnits.Returns([unit]);
        _clientGame.BattleMap.Returns((BattleMap?)null);
        
        // Act
        await _sut.MakeDecision();
        
        // Assert
        await _clientGame.DidNotReceive().MoveUnit(Arg.Any<MoveUnitCommand>());
    }

    [Fact]
    public async Task MakeDecision_WhenNoReachableHexes_ShouldStandStill()
    {
        // Arrange
        var unit = CreateMockUnit(hasMoved: false, isDeployed: true);
        _player.AliveUnits.Returns([unit]);
        _clientGame.Players.Returns([_player]);
        
        // Mock no reachable hexes
        _battleMap.GetReachableHexes(Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<IEnumerable<HexCoordinates>>())
            .Returns([]);
        
        // Act
        await _sut.MakeDecision();
        
        // Assert
        await _clientGame.Received(1).MoveUnit(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.PlayerId == _player.Id &&
            cmd.UnitId == unit.Id &&
            cmd.MovementType == MovementType.Walk &&
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
        var pathSegment = Substitute.For<PathSegment>();
        var pathSegmentData = new PathSegmentData
        {
            From = new HexPositionData { Coordinates = new HexCoordinateData(1, 1), Facing = 0 },
            To = new HexPositionData { Coordinates = new HexCoordinateData(2, 2), Facing = 0 },
            Cost = 1
        };
        pathSegment.ToData().Returns(pathSegmentData);
        _battleMap.FindPath(Arg.Any<HexPosition>(), Arg.Any<HexPosition>(), Arg.Any<int>(), Arg.Any<IEnumerable<HexCoordinates>>())
            .Returns([pathSegment]);
        
        // Act
        await _sut.MakeDecision();
        
        // Assert
        await _clientGame.Received(1).MoveUnit(Arg.Is<MoveUnitCommand>(cmd =>
            cmd.PlayerId == _player.Id &&
            cmd.UnitId == unit.Id &&
            cmd.MovementType == MovementType.Walk));
    }

    [Fact]
    public async Task MakeDecision_WhenExceptionThrown_ShouldNotThrow()
    {
        // Arrange
        _player.AliveUnits.Returns((IReadOnlyList<Unit>?)null!); // This will cause an exception
        
        // Act & Assert
        await Should.NotThrowAsync(async () => await _sut.MakeDecision());
    }

    private IUnit CreateMockUnit(bool hasMoved, bool isDeployed = true)
    {
        var unit = Substitute.For<IUnit>();
        unit.Id.Returns(Guid.NewGuid());
        unit.MovementTypeUsed.Returns(hasMoved ? MovementType.Walk : null);
        unit.GetMovementPoints(MovementType.Walk).Returns(4);
        
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

using NSubstitute;
using Sanet.MakaMek.Bots.Exceptions;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.Models.DecisionEngines;

public class DeploymentEngineTests
{
    private readonly IClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly IBattleMap _battleMap;
    private readonly DeploymentEngine _sut;

    public DeploymentEngineTests()
    {
        _clientGame = Substitute.For<IClientGame>();
        _player = Substitute.For<IPlayer>();
        _battleMap = Substitute.For<IBattleMap>();
        
        _clientGame.Id.Returns(Guid.NewGuid());
        _clientGame.BattleMap.Returns(_battleMap);
        _player.Id.Returns(Guid.NewGuid());
        _player.Name.Returns("Test Player");
        
        _sut = new DeploymentEngine(_clientGame);
    }

    [Fact]
    public async Task MakeDecision_WhenNoUndeployedUnits_ShouldThrowBotDecisionException()
    {
        // Arrange
        var deployedUnit = CreateMockUnit(isDeployed: true);
        _player.Units.Returns([deployedUnit]);
        _battleMap.Width.Returns(5);
        _battleMap.Height.Returns(5);
        _clientGame.Players.Returns([_player]);
        
        // Act
        var exception = await Should.ThrowAsync<BotDecisionException>(
            async () => await _sut.MakeDecision(_player));
        
        // Assert
        exception.Message.ShouldContain("no undeployed units");
        exception.DecisionEngineType.ShouldBe(nameof(DeploymentEngine));
        exception.PlayerId.ShouldBe(_player.Id);
    }

    [Fact]
    public async Task MakeDecision_WhenNoBattleMap_ShouldThrowBotDecisionException()
    {
        // Arrange
        var undeployedUnit = CreateMockUnit(isDeployed: false);
        _player.Units.Returns([undeployedUnit]);
        _clientGame.BattleMap.Returns((BattleMap?)null);
        
        // Act
        var exception = await Should.ThrowAsync<BotDecisionException>(
            async () => await _sut.MakeDecision(_player));
        
        // Assert
        exception.Message.ShouldContain("no valid deployment hexes");
        exception.DecisionEngineType.ShouldBe(nameof(DeploymentEngine));
        exception.PlayerId.ShouldBe(_player.Id);
    }

    [Fact]
    public async Task MakeDecision_WhenNoValidHexes_ShouldThrowBotDecisionException()
    {
        // Arrange
        var undeployedUnit = CreateMockUnit(isDeployed: false);
        _player.Units.Returns([undeployedUnit]);
        _battleMap.Width.Returns(0);
        _battleMap.Height.Returns(0);
        
        // Act
        var exception = await Should.ThrowAsync<BotDecisionException>(
            async () => await _sut.MakeDecision(_player));
        
        // Assert
        exception.Message.ShouldContain("no valid deployment hexes");
        exception.DecisionEngineType.ShouldBe(nameof(DeploymentEngine));
        exception.PlayerId.ShouldBe(_player.Id);
    }

    [Fact]
    public async Task MakeDecision_WhenValidConditions_ShouldDeployUnitOnEdge()
    {
        // Arrange
        var undeployedUnit = CreateMockUnit(isDeployed: false);
        _player.Units.Returns([undeployedUnit]);
        _battleMap.Width.Returns(5);
        _battleMap.Height.Returns(5);
        
        // Mock no other players have units deployed
        _clientGame.Players.Returns([_player]);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        await _clientGame.Received(1).DeployUnit(Arg.Is<DeployUnitCommand>(cmd =>
            cmd.PlayerId == _player.Id &&
            cmd.UnitId == undeployedUnit.Id &&
            cmd.GameOriginId == _clientGame.Id &&
            IsEdgeHex(new HexCoordinates(cmd.Position), 5, 5)));
    }

    [Fact]
    public async Task MakeDecision_WhenExceptionThrown_ShouldNotThrow()
    {
        // Arrange
        _player.Units.Returns((IReadOnlyList<Unit>?)null!); // This will cause an exception
        
        // Act & Assert
        await Should.NotThrowAsync(async () => await _sut.MakeDecision(_player));
    }

    [Fact]
    public async Task MakeDecision_WhenMultipleUndeployedUnits_ShouldDeployFirstOne()
    {
        // Arrange
        var unit1 = CreateMockUnit(isDeployed: false);
        var unit2 = CreateMockUnit(isDeployed: false);
        _player.Units.Returns([unit1, unit2]);
        _battleMap.Width.Returns(5);
        _battleMap.Height.Returns(5);
        _clientGame.Players.Returns([_player]);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        await _clientGame.Received(1).DeployUnit(Arg.Is<DeployUnitCommand>(cmd =>
            cmd.UnitId == unit1.Id));
    }

    [Fact]
    public async Task MakeDecision_WhenEdgeHexesOccupied_ShouldDeployOnUnoccupiedEdge()
    {
        // Arrange
        var undeployedUnit = CreateMockUnit(isDeployed: false);
        var occupyingUnit = CreateMockUnit(isDeployed: true, new HexCoordinates(1, 1));
        
        var otherPlayer = Substitute.For<IPlayer>();
        otherPlayer.Units.Returns([occupyingUnit]);
        
        _player.Units.Returns([undeployedUnit]);
        _battleMap.Width.Returns(5);
        _battleMap.Height.Returns(5);
        _clientGame.Players.Returns([_player, otherPlayer]);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        await _clientGame.Received(1).DeployUnit(Arg.Is<DeployUnitCommand>(cmd =>
            new HexCoordinates(cmd.Position) != new HexCoordinates(1, 1) &&
            IsEdgeHex(new HexCoordinates(cmd.Position), 5, 5)));
    }

    [Fact]
    public async Task MakeDecision_ShouldOnlyConsiderDeployedUnitsAsOccupied()
    {
        // Arrange
        var undeployedUnit = CreateMockUnit(isDeployed: false);
        var undeployedOtherUnit = CreateMockUnit(isDeployed: false); // Not deployed, should not block
        
        var otherPlayer = Substitute.For<IPlayer>();
        otherPlayer.Units.Returns([undeployedOtherUnit]);
        
        _player.Units.Returns([undeployedUnit]);
        _battleMap.Width.Returns(5);
        _battleMap.Height.Returns(5);
        _clientGame.Players.Returns([_player, otherPlayer]);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert - should deploy successfully since no hexes are actually occupied
        await _clientGame.Received(1).DeployUnit(Arg.Any<DeployUnitCommand>());
    }

    [Fact]
    public async Task MakeDecision_WhenEnemyDeployed_ShouldFaceNearestEnemy()
    {
        // Arrange
        var undeployedUnit = CreateMockUnit(isDeployed: false);
        _player.Units.Returns([undeployedUnit]);
        
        // Deploy enemy at position (3, 3)
        var enemyUnit = CreateMockUnit(isDeployed: true, new HexCoordinates(3, 3));
        var enemyPlayer = Substitute.For<IPlayer>();
        enemyPlayer.Id.Returns(Guid.NewGuid());
        enemyPlayer.Units.Returns([enemyUnit]);
        
        _battleMap.Width.Returns(5);
        _battleMap.Height.Returns(5);
        _clientGame.Players.Returns([_player, enemyPlayer]);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        var receivedCalls = _clientGame.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(_clientGame.DeployUnit))
            .ToList();
        
        receivedCalls.Count.ShouldBe(1);
        var command = (DeployUnitCommand)receivedCalls[0].GetArguments()[0]!;
        
        var deployPos = new HexCoordinates(command.Position);
        var enemyPos = new HexCoordinates(3, 3);
        var directionHex = deployPos.Neighbor((HexDirection)command.Direction);
        var lineToEnemy = deployPos.LineTo(enemyPos);
        
        // The direction should point to one of the hexes in the line of sight
        lineToEnemy.Any(segment => 
            segment.MainOption.Equals(directionHex) || 
            (segment.SecondOption != null && segment.SecondOption.Equals(directionHex)))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task MakeDecision_WhenNoEnemyDeployed_ShouldFaceMapCenter()
    {
        // Arrange
        var undeployedUnit = CreateMockUnit(isDeployed: false);
        _player.Units.Returns([undeployedUnit]);
        
        _battleMap.Width.Returns(5);
        _battleMap.Height.Returns(5);
        _clientGame.Players.Returns([_player]);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        var receivedCalls = _clientGame.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(_clientGame.DeployUnit))
            .ToList();
        
        receivedCalls.Count.ShouldBe(1);
        var command = (DeployUnitCommand)receivedCalls[0].GetArguments()[0]!;
        
        var deployPos = new HexCoordinates(command.Position);
        var mapCenter = new HexCoordinates(3, 3); // (5+1)/2 = 3
        var directionHex = deployPos.Neighbor((HexDirection)command.Direction);
        var lineToCenter = deployPos.LineTo(mapCenter);
        
        // The direction should point to one of the hexes in the line of sight
        lineToCenter.Any(segment => 
            segment.MainOption.Equals(directionHex) || 
            (segment.SecondOption != null && segment.SecondOption.Equals(directionHex)))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task MakeDecision_WhenMultipleEnemiesDeployed_ShouldFaceNearestOne()
    {
        // Arrange
        var undeployedUnit = CreateMockUnit(isDeployed: false);
        _player.Units.Returns([undeployedUnit]);
        
        // Deploy two enemies at different distances
        var nearEnemy = CreateMockUnit(isDeployed: true, new HexCoordinates(2, 2));
        var farEnemy = CreateMockUnit(isDeployed: true, new HexCoordinates(5, 5));
        
        var enemyPlayer = Substitute.For<IPlayer>();
        enemyPlayer.Id.Returns(Guid.NewGuid());
        enemyPlayer.Units.Returns([nearEnemy, farEnemy]);
        
        _battleMap.Width.Returns(5);
        _battleMap.Height.Returns(5);
        _clientGame.Players.Returns([_player, enemyPlayer]);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        var receivedCalls = _clientGame.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(_clientGame.DeployUnit))
            .ToList();
        
        receivedCalls.Count.ShouldBe(1);
        var command = (DeployUnitCommand)receivedCalls[0].GetArguments()[0]!;
        
        var deployPos = new HexCoordinates(command.Position);
        var nearEnemyPos = new HexCoordinates(2, 2);
        var farEnemyPos = new HexCoordinates(5, 5);
        
        // Calculate distances
        var distanceToNear = deployPos.DistanceTo(nearEnemyPos);
        var distanceToFar = deployPos.DistanceTo(farEnemyPos);
        
        // Determine which enemy is actually nearest
        var nearestEnemyPos = distanceToNear <= distanceToFar ? nearEnemyPos : farEnemyPos;
        
        // Verify the direction points toward the nearest enemy
        var directionHex = deployPos.Neighbor((HexDirection)command.Direction);
        var lineToNearest = deployPos.LineTo(nearestEnemyPos);
        
        lineToNearest.Any(segment => 
            segment.MainOption.Equals(directionHex) || 
            (segment.SecondOption != null && segment.SecondOption.Equals(directionHex)))
            .ShouldBeTrue();
    }


    [Fact]
    public async Task MakeDecision_WhenDeployingAtMapCenter_ShouldThrowBotDecisionException()
    {
        // Arrange - Create a 1x1 map where the only hex is the center
        var undeployedUnit = CreateMockUnit(isDeployed: false);
        _player.Units.Returns([undeployedUnit]);
        
        _battleMap.Width.Returns(1);
        _battleMap.Height.Returns(1);
        _clientGame.Players.Returns([_player]);
        
        // Act - The only valid deployment hex (1,1) is also the map center
        var exception = await Should.ThrowAsync<BotDecisionException>(
            async () => await _sut.MakeDecision(_player));
        
        // Assert
        exception.Message.ShouldContain("cannot deploy at target position");
        exception.DecisionEngineType.ShouldBe(nameof(DeploymentEngine));
        exception.PlayerId.ShouldBe(_player.Id);
    }
    
    private IUnit CreateMockUnit(bool isDeployed, HexCoordinates? coordinates = null)
    {
        var unit = Substitute.For<IUnit>();
        unit.Id.Returns(Guid.NewGuid());
        
        if (isDeployed)
        {
            var coords = coordinates ?? new HexCoordinates(1, 1);
            var position = new HexPosition(coords, HexDirection.Top);
            unit.Position.Returns(position);
        }
        else
        {
            unit.Position.Returns((HexPosition?)null);
        }
        unit.IsDeployed.Returns(isDeployed);
        
        return unit;
    }

    private static bool IsEdgeHex(HexCoordinates coords, int width, int height)
    {
        return coords.Q == 1 || coords.Q == width || 
               coords.R == 1 || coords.R == height;
    }
}

using NSubstitute;
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
    public async Task MakeDecision_WhenNoUndeployedUnits_ShouldNotDeployAnything()
    {
        // Arrange
        var deployedUnit = CreateMockUnit(isDeployed: true);
        _player.Units.Returns([deployedUnit]);
        _battleMap.Width.Returns(5);
        _battleMap.Height.Returns(5);
        _clientGame.Players.Returns([_player]);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        await _clientGame.DidNotReceive().DeployUnit(Arg.Any<DeployUnitCommand>());
    }

    [Fact]
    public async Task MakeDecision_WhenNoBattleMap_ShouldNotDeployAnything()
    {
        // Arrange
        var undeployedUnit = CreateMockUnit(isDeployed: false);
        _player.Units.Returns([undeployedUnit]);
        _clientGame.BattleMap.Returns((BattleMap?)null);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        await _clientGame.DidNotReceive().DeployUnit(Arg.Any<DeployUnitCommand>());
    }

    [Fact]
    public async Task MakeDecision_WhenNoValidHexes_ShouldNotDeployAnything()
    {
        // Arrange
        var undeployedUnit = CreateMockUnit(isDeployed: false);
        _player.Units.Returns([undeployedUnit]);
        _battleMap.Width.Returns(0);
        _battleMap.Height.Returns(0);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        await _clientGame.DidNotReceive().DeployUnit(Arg.Any<DeployUnitCommand>());
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

    [Theory]
    [InlineData(3, 3, 8)]  // 3x3 map has 8 edge hexes (perimeter)
    [InlineData(5, 5, 16)] // 5x5 map has 16 edge hexes
    [InlineData(10, 10, 36)] // 10x10 map has 36 edge hexes
    public void GetDeploymentArea_ShouldReturnCorrectNumberOfEdgeHexes(int width, int height, int expectedCount)
    {
        // Arrange
        _battleMap.Width.Returns(width);
        _battleMap.Height.Returns(height);
        
        // Act - use reflection to call protected method
        var method = typeof(DeploymentEngine).GetMethod("GetDeploymentArea", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (HashSet<HexCoordinates>)method!.Invoke(_sut, null)!;
        
        // Assert
        result.Count.ShouldBe(expectedCount);
    }

    [Theory]
    [InlineData(1, 1, true)]   // Top-left corner
    [InlineData(5, 1, true)]   // Top-right corner
    [InlineData(1, 5, true)]   // Bottom-left corner
    [InlineData(5, 5, true)]   // Bottom-right corner
    [InlineData(3, 1, true)]   // Top edge
    [InlineData(3, 5, true)]   // Bottom edge
    [InlineData(1, 3, true)]   // Left edge
    [InlineData(5, 3, true)]   // Right edge
    [InlineData(3, 3, false)]  // Center (not edge)
    [InlineData(2, 2, false)]  // Interior (not edge)
    public void GetDeploymentArea_ShouldIncludeOnlyEdgeHexes(int q, int r, bool shouldBeIncluded)
    {
        // Arrange
        _battleMap.Width.Returns(5);
        _battleMap.Height.Returns(5);
        
        // Act
        var method = typeof(DeploymentEngine).GetMethod("GetDeploymentArea", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (HashSet<HexCoordinates>)method!.Invoke(_sut, null)!;
        
        // Assert
        var hex = new HexCoordinates(q, r);
        result.Contains(hex).ShouldBe(shouldBeIncluded);
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
        var mapCenter = new HexCoordinates(2, 2); // 5/2 = 2 (integer division)
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

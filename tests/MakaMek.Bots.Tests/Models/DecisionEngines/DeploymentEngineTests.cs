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
        _battleMap.GetHex(Arg.Any<HexCoordinates>()).Returns(new Hex(new HexCoordinates(1, 1)));
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
    public async Task MakeDecision_WhenValidConditions_ShouldDeployUnit()
    {
        // Arrange
        var undeployedUnit = CreateMockUnit(isDeployed: false);
        _player.Units.Returns([undeployedUnit]);
        _battleMap.Width.Returns(5);
        _battleMap.Height.Returns(5);
        
        // Mock a valid hex
        var hex = new Hex(new HexCoordinates(1,1));
        _battleMap.GetHex(Arg.Any<HexCoordinates>()).Returns(hex);
        
        // Mock no other players have units deployed
        _clientGame.Players.Returns([_player]);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        await _clientGame.Received(1).DeployUnit(Arg.Is<DeployUnitCommand>(cmd =>
            cmd.PlayerId == _player.Id &&
            cmd.UnitId == undeployedUnit.Id &&
            cmd.GameOriginId == _clientGame.Id));
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
        
        var hex = new Hex(new HexCoordinates(1,1));
        _battleMap.GetHex(Arg.Any<HexCoordinates>()).Returns(hex);
        _clientGame.Players.Returns([_player]);
        
        // Act
        await _sut.MakeDecision(_player);
        
        // Assert
        await _clientGame.Received(1).DeployUnit(Arg.Is<DeployUnitCommand>(cmd =>
            cmd.UnitId == unit1.Id));
    }

    private IUnit CreateMockUnit(bool isDeployed)
    {
        var unit = Substitute.For<IUnit>();
        
        if (isDeployed)
        {
            var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
            unit.Position.Returns(position);
        }
        else
        {
            unit.Position.Returns((HexPosition?)null);
        }
        unit.IsDeployed.Returns(isDeployed);
        
        return unit;
    }
}

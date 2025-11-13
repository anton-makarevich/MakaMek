using NSubstitute;
using Sanet.MakaMek.Bots.DecisionEngines;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Tests.Utils;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.DecisionEngines;

public class DeploymentEngineTests
{
    private readonly ClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly DeploymentEngine _sut;
    private readonly BattleMap _battleMap;

    public DeploymentEngineTests()
    {
        _clientGame = Substitute.For<ClientGame>();
        _player = Substitute.For<IPlayer>();
        _battleMap = BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain()));
        
        _clientGame.BattleMap.Returns(_battleMap);
        _clientGame.Id.Returns(Guid.NewGuid());
        _player.Id.Returns(Guid.NewGuid());
        
        _sut = new DeploymentEngine(_clientGame, _player, BotDifficulty.Easy);
    }

    [Fact]
    public async Task MakeDecision_ShouldDeployUnit_WhenUndeployedUnitExists()
    {
        // Arrange
        var unit = Substitute.For<Unit>();
        unit.Id.Returns(Guid.NewGuid());
        unit.IsDeployed.Returns(false);
        
        var units = new List<Unit> { unit };
        _player.Units.Returns(units);
        _clientGame.Players.Returns(new List<IPlayer> { _player });
        _clientGame.DeployUnit(Arg.Any<DeployUnitCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.Received(1).DeployUnit(Arg.Is<DeployUnitCommand>(cmd =>
            cmd.UnitId == unit.Id &&
            cmd.PlayerId == _player.Id &&
            cmd.GameOriginId == _clientGame.Id));
    }

    [Fact]
    public async Task MakeDecision_ShouldNotDeployUnit_WhenAllUnitsDeployed()
    {
        // Arrange
        var unit = Substitute.For<Unit>();
        unit.IsDeployed.Returns(true);
        
        var units = new List<Unit> { unit };
        _player.Units.Returns(units);

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.DidNotReceive().DeployUnit(Arg.Any<DeployUnitCommand>());
    }

    [Fact]
    public async Task MakeDecision_ShouldSelectValidHex_WhenDeploying()
    {
        // Arrange
        var unit = Substitute.For<Unit>();
        unit.Id.Returns(Guid.NewGuid());
        unit.IsDeployed.Returns(false);
        
        var units = new List<Unit> { unit };
        _player.Units.Returns(units);
        _clientGame.Players.Returns(new List<IPlayer> { _player });
        _clientGame.DeployUnit(Arg.Any<DeployUnitCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.Received(1).DeployUnit(Arg.Is<DeployUnitCommand>(cmd =>
            cmd.Position.Q >= 1 && cmd.Position.Q <= 5 &&
            cmd.Position.R >= 1 && cmd.Position.R <= 5));
    }

    [Fact]
    public async Task MakeDecision_ShouldSelectValidDirection_WhenDeploying()
    {
        // Arrange
        var unit = Substitute.For<Unit>();
        unit.Id.Returns(Guid.NewGuid());
        unit.IsDeployed.Returns(false);
        
        var units = new List<Unit> { unit };
        _player.Units.Returns(units);
        _clientGame.Players.Returns(new List<IPlayer> { _player });
        _clientGame.DeployUnit(Arg.Any<DeployUnitCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.Received(1).DeployUnit(Arg.Is<DeployUnitCommand>(cmd =>
            cmd.Direction >= 0 && cmd.Direction <= 5));
    }

    [Fact]
    public async Task MakeDecision_ShouldNotDeployOnOccupiedHex_WhenOtherUnitsPresent()
    {
        // Arrange
        var undeployedUnit = Substitute.For<Unit>();
        undeployedUnit.Id.Returns(Guid.NewGuid());
        undeployedUnit.IsDeployed.Returns(false);
        
        var deployedUnit = Substitute.For<Unit>();
        deployedUnit.IsDeployed.Returns(true);
        deployedUnit.Position.Returns(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));
        
        var units = new List<Unit> { undeployedUnit };
        _player.Units.Returns(units);
        
        var otherPlayer = Substitute.For<IPlayer>();
        otherPlayer.Units.Returns(new List<Unit> { deployedUnit });
        
        _clientGame.Players.Returns(new List<IPlayer> { _player, otherPlayer });
        _clientGame.DeployUnit(Arg.Any<DeployUnitCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.Received(1).DeployUnit(Arg.Is<DeployUnitCommand>(cmd =>
            !(cmd.Position.Q == 1 && cmd.Position.R == 1)));
    }

    [Fact]
    public async Task MakeDecision_ShouldHandleException_WhenDeploymentFails()
    {
        // Arrange
        var unit = Substitute.For<Unit>();
        unit.Id.Returns(Guid.NewGuid());
        unit.IsDeployed.Returns(false);
        
        var units = new List<Unit> { unit };
        _player.Units.Returns(units);
        _clientGame.Players.Returns(new List<IPlayer> { _player });
        _clientGame.DeployUnit(Arg.Any<DeployUnitCommand>()).Returns(Task.FromException<bool>(new Exception("Test exception")));

        // Act & Assert - should not throw
        await _sut.MakeDecision();
    }

    [Fact]
    public async Task MakeDecision_ShouldNotThrow_WhenBattleMapIsNull()
    {
        // Arrange
        _clientGame.BattleMap.Returns((BattleMap?)null);
        
        var unit = Substitute.For<Unit>();
        unit.IsDeployed.Returns(false);
        _player.Units.Returns(new List<Unit> { unit });

        // Act & Assert - should not throw
        await _sut.MakeDecision();
    }
}


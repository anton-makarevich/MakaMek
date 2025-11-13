using NSubstitute;
using Sanet.MakaMek.Bots.DecisionEngines;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.DecisionEngines;

public class EndPhaseEngineTests
{
    private readonly ClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly EndPhaseEngine _sut;

    public EndPhaseEngineTests()
    {
        _clientGame = Substitute.For<ClientGame>();
        _player = Substitute.For<IPlayer>();
        
        _clientGame.Id.Returns(Guid.NewGuid());
        _player.Id.Returns(Guid.NewGuid());
        
        _sut = new EndPhaseEngine(_clientGame, _player, BotDifficulty.Easy);
    }

    [Fact]
    public async Task MakeDecision_ShouldEndTurn_WhenCalled()
    {
        // Arrange
        _player.AliveUnits.Returns(new List<Unit>());
        _clientGame.EndTurn(Arg.Any<TurnEndedCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.Received(1).EndTurn(Arg.Is<TurnEndedCommand>(cmd =>
            cmd.PlayerId == _player.Id &&
            cmd.GameOriginId == _clientGame.Id));
    }

    [Fact]
    public async Task MakeDecision_ShouldAttemptStartup_WhenUnitIsShutdown()
    {
        // Arrange
        var shutdownUnit = Substitute.For<Unit>();
        shutdownUnit.Id.Returns(Guid.NewGuid());
        shutdownUnit.IsShutdown.Returns(true);
        
        var aliveUnits = new List<Unit> { shutdownUnit };
        _player.AliveUnits.Returns(aliveUnits);
        _clientGame.StartupUnit(Arg.Any<StartupUnitCommand>()).Returns(Task.FromResult(true));
        _clientGame.EndTurn(Arg.Any<TurnEndedCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.Received(1).StartupUnit(Arg.Is<StartupUnitCommand>(cmd =>
            cmd.UnitId == shutdownUnit.Id &&
            cmd.PlayerId == _player.Id &&
            cmd.GameOriginId == _clientGame.Id));
    }

    [Fact]
    public async Task MakeDecision_ShouldNotAttemptStartup_WhenUnitIsNotShutdown()
    {
        // Arrange
        var activeUnit = Substitute.For<Unit>();
        activeUnit.IsShutdown.Returns(false);
        
        var aliveUnits = new List<Unit> { activeUnit };
        _player.AliveUnits.Returns(aliveUnits);
        _clientGame.EndTurn(Arg.Any<TurnEndedCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.DidNotReceive().StartupUnit(Arg.Any<StartupUnitCommand>());
    }

    [Fact]
    public async Task MakeDecision_ShouldShutdownUnit_WhenHeatIsAboveThreshold()
    {
        // Arrange
        var overheatedUnit = Substitute.For<Unit>();
        overheatedUnit.Id.Returns(Guid.NewGuid());
        overheatedUnit.IsShutdown.Returns(false);
        overheatedUnit.CurrentHeat.Returns(26); // Above threshold
        
        var aliveUnits = new List<Unit> { overheatedUnit };
        _player.AliveUnits.Returns(aliveUnits);
        _clientGame.ShutdownUnit(Arg.Any<ShutdownUnitCommand>()).Returns(Task.FromResult(true));
        _clientGame.EndTurn(Arg.Any<TurnEndedCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.Received(1).ShutdownUnit(Arg.Is<ShutdownUnitCommand>(cmd =>
            cmd.UnitId == overheatedUnit.Id &&
            cmd.PlayerId == _player.Id &&
            cmd.GameOriginId == _clientGame.Id));
    }

    [Fact]
    public async Task MakeDecision_ShouldNotShutdownUnit_WhenHeatIsBelowThreshold()
    {
        // Arrange
        var normalUnit = Substitute.For<Unit>();
        normalUnit.IsShutdown.Returns(false);
        normalUnit.CurrentHeat.Returns(25); // Below threshold
        
        var aliveUnits = new List<Unit> { normalUnit };
        _player.AliveUnits.Returns(aliveUnits);
        _clientGame.EndTurn(Arg.Any<TurnEndedCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.DidNotReceive().ShutdownUnit(Arg.Any<ShutdownUnitCommand>());
    }

    [Fact]
    public async Task MakeDecision_ShouldNotShutdownUnit_WhenAlreadyShutdown()
    {
        // Arrange
        var shutdownUnit = Substitute.For<Unit>();
        shutdownUnit.IsShutdown.Returns(true);
        shutdownUnit.CurrentHeat.Returns(30); // High heat but already shutdown
        
        var aliveUnits = new List<Unit> { shutdownUnit };
        _player.AliveUnits.Returns(aliveUnits);
        _clientGame.StartupUnit(Arg.Any<StartupUnitCommand>()).Returns(Task.FromResult(true));
        _clientGame.EndTurn(Arg.Any<TurnEndedCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.DidNotReceive().ShutdownUnit(Arg.Any<ShutdownUnitCommand>());
    }

    [Fact]
    public async Task MakeDecision_ShouldProcessMultipleUnits_WhenMultipleUnitsNeedAction()
    {
        // Arrange
        var shutdownUnit = Substitute.For<Unit>();
        shutdownUnit.Id.Returns(Guid.NewGuid());
        shutdownUnit.IsShutdown.Returns(true);
        shutdownUnit.CurrentHeat.Returns(10);
        
        var overheatedUnit = Substitute.For<Unit>();
        overheatedUnit.Id.Returns(Guid.NewGuid());
        overheatedUnit.IsShutdown.Returns(false);
        overheatedUnit.CurrentHeat.Returns(28);
        
        var aliveUnits = new List<Unit> { shutdownUnit, overheatedUnit };
        _player.AliveUnits.Returns(aliveUnits);
        _clientGame.StartupUnit(Arg.Any<StartupUnitCommand>()).Returns(Task.FromResult(true));
        _clientGame.ShutdownUnit(Arg.Any<ShutdownUnitCommand>()).Returns(Task.FromResult(true));
        _clientGame.EndTurn(Arg.Any<TurnEndedCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.Received(1).StartupUnit(Arg.Is<StartupUnitCommand>(cmd =>
            cmd.UnitId == shutdownUnit.Id));
        await _clientGame.Received(1).ShutdownUnit(Arg.Is<ShutdownUnitCommand>(cmd =>
            cmd.UnitId == overheatedUnit.Id));
    }

    [Fact]
    public async Task MakeDecision_ShouldStillEndTurn_WhenStartupFails()
    {
        // Arrange
        var shutdownUnit = Substitute.For<Unit>();
        shutdownUnit.Id.Returns(Guid.NewGuid());
        shutdownUnit.IsShutdown.Returns(true);
        
        _player.AliveUnits.Returns(new List<Unit> { shutdownUnit });
        _clientGame.StartupUnit(Arg.Any<StartupUnitCommand>())
            .Returns(Task.FromException<bool>(new Exception("Test exception")));
        _clientGame.EndTurn(Arg.Any<TurnEndedCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.Received(1).EndTurn(Arg.Any<TurnEndedCommand>());
    }

    [Fact]
    public async Task MakeDecision_ShouldStillEndTurn_WhenShutdownFails()
    {
        // Arrange
        var overheatedUnit = Substitute.For<Unit>();
        overheatedUnit.Id.Returns(Guid.NewGuid());
        overheatedUnit.IsShutdown.Returns(false);
        overheatedUnit.CurrentHeat.Returns(30);
        
        _player.AliveUnits.Returns(new List<Unit> { overheatedUnit });
        _clientGame.ShutdownUnit(Arg.Any<ShutdownUnitCommand>())
            .Returns(Task.FromException<bool>(new Exception("Test exception")));
        _clientGame.EndTurn(Arg.Any<TurnEndedCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.Received(1).EndTurn(Arg.Any<TurnEndedCommand>());
    }

    [Fact]
    public async Task MakeDecision_ShouldNotThrow_WhenEndTurnFails()
    {
        // Arrange
        _player.AliveUnits.Returns(new List<Unit>());
        _clientGame.EndTurn(Arg.Any<TurnEndedCommand>())
            .Returns(Task.FromException<bool>(new Exception("Test exception")));

        // Act & Assert - should not throw
        await _sut.MakeDecision();
    }
}


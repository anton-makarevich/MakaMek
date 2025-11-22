using NSubstitute;
using Sanet.MakaMek.Bots.DecisionEngines;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Bots.Tests.DecisionEngines;

public class EndPhaseEngineTests
{
    private readonly IClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly EndPhaseEngine _sut;

    public EndPhaseEngineTests()
    {
        _clientGame = Substitute.For<IClientGame>();
        _player = Substitute.For<IPlayer>();
        
        _clientGame.Id.Returns(Guid.NewGuid());
        _player.Id.Returns(Guid.NewGuid());
        _player.Name.Returns("Test Player");
        
        _sut = new EndPhaseEngine(_clientGame, _player, BotDifficulty.Easy);
    }

    [Fact]
    public async Task MakeDecision_ShouldAlwaysEndTurn()
    {
        // Arrange
        _player.AliveUnits.Returns([]);
        
        // Act
        await _sut.MakeDecision();
        
        // Assert
        await _clientGame.Received(1).EndTurn(Arg.Is<TurnEndedCommand>(cmd =>
            cmd.PlayerId == _player.Id &&
            cmd.GameOriginId == _clientGame.Id));
    }

    [Fact]
    public async Task MakeDecision_WhenShutdownUnits_ShouldAttemptStartup()
    {
        // Arrange
        var shutdownUnit = CreateMockUnit(isShutdown: true, currentHeat: 15);
        _player.AliveUnits.Returns([shutdownUnit]);
        
        // Act
        await _sut.MakeDecision();
        
        // Assert
        await _clientGame.Received(1).StartupUnit(Arg.Is<StartupUnitCommand>(cmd =>
            cmd.PlayerId == _player.Id &&
            cmd.UnitId == shutdownUnit.Id &&
            cmd.GameOriginId == _clientGame.Id));
        
        await _clientGame.Received(1).EndTurn(Arg.Any<TurnEndedCommand>());
    }

    [Fact]
    public async Task MakeDecision_WhenOverheatedUnits_ShouldShutdownUnit()
    {
        // Arrange
        var overheatedUnit = CreateMockUnit(isShutdown: false, currentHeat: 30);
        _player.AliveUnits.Returns([overheatedUnit]);
        
        // Act
        await _sut.MakeDecision();
        
        // Assert
        await _clientGame.Received(1).ShutdownUnit(Arg.Is<ShutdownUnitCommand>(cmd =>
            cmd.PlayerId == _player.Id &&
            cmd.UnitId == overheatedUnit.Id &&
            cmd.GameOriginId == _clientGame.Id));
        
        await _clientGame.Received(1).EndTurn(Arg.Any<TurnEndedCommand>());
    }

    [Fact]
    public async Task MakeDecision_WhenNormalHeatUnits_ShouldNotShutdown()
    {
        // Arrange
        var normalUnit = CreateMockUnit(isShutdown: false, currentHeat: 20);
        _player.AliveUnits.Returns([normalUnit]);
        
        // Act
        await _sut.MakeDecision();
        
        // Assert
        await _clientGame.DidNotReceive().ShutdownUnit(Arg.Any<ShutdownUnitCommand>());
        await _clientGame.Received(1).EndTurn(Arg.Any<TurnEndedCommand>());
    }

    [Fact]
    public async Task MakeDecision_WhenExceptionInHandlers_ShouldStillEndTurn()
    {
        // Arrange
        _player.AliveUnits.Returns((IReadOnlyList<IUnit>?)null!); // This will cause an exception
        
        // Act
        await _sut.MakeDecision();
        
        // Assert - Should still attempt to end turn even if other operations fail
        await _clientGame.Received(1).EndTurn(Arg.Any<TurnEndedCommand>());
    }

    [Fact]
    public async Task MakeDecision_WhenMultipleUnitTypes_ShouldHandleAll()
    {
        // Arrange
        var shutdownUnit = CreateMockUnit(isShutdown: true, currentHeat: 15);
        var overheatedUnit = CreateMockUnit(isShutdown: false, currentHeat: 30);
        var normalUnit = CreateMockUnit(isShutdown: false, currentHeat: 10);
        
        _player.AliveUnits.Returns([shutdownUnit, overheatedUnit, normalUnit]);
        
        // Act
        await _sut.MakeDecision();
        
        // Assert
        await _clientGame.Received(1).StartupUnit(Arg.Is<StartupUnitCommand>(cmd =>
            cmd.UnitId == shutdownUnit.Id));
        
        await _clientGame.Received(1).ShutdownUnit(Arg.Is<ShutdownUnitCommand>(cmd =>
            cmd.UnitId == overheatedUnit.Id));
        
        await _clientGame.Received(1).EndTurn(Arg.Any<TurnEndedCommand>());
    }

    private IUnit CreateMockUnit(bool isShutdown, int currentHeat)
    {
        var unit = Substitute.For<IUnit>();
        unit.Id.Returns(Guid.NewGuid());
        unit.IsShutdown.Returns(isShutdown);
        unit.CurrentHeat.Returns(currentHeat);
        return unit;
    }
}

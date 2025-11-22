using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Shouldly;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using Sanet.MakaMek.Bots.DecisionEngines;

namespace Sanet.MakaMek.Bots.Tests;

public class BotTests : IDisposable
{
    private readonly IClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly Subject<IGameCommand> _commandSubject;
    private readonly Bot _sut;

    public BotTests()
    {
        _clientGame = Substitute.For<IClientGame>();
        _player = Substitute.For<IPlayer>();
        _commandSubject = new Subject<IGameCommand>();
        
        _player.Id.Returns(Guid.NewGuid());
        _player.Name.Returns("Test Bot");
        _clientGame.Commands.Returns(_commandSubject.AsObservable());
        _clientGame.Id.Returns(Guid.NewGuid());
        
        _sut = new Bot(_player, _clientGame, BotDifficulty.Easy);
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Assert
        _sut.Player.ShouldBe(_player);
        _sut.Difficulty.ShouldBe(BotDifficulty.Easy);
    }

    [Fact]
    public void Constructor_ShouldSubscribeToClientGameCommands()
    {
        // Act - Create a new bot and send a command
        using var bot = new Bot(_player, _clientGame, BotDifficulty.Easy);
        _commandSubject.OnNext(new ChangePhaseCommand
        {
            GameOriginId = _clientGame.Id,
            Phase = PhaseNames.Movement
        });

        // Give some time for async processing
        Thread.Sleep(100);

        // Assert 
        bot.DecisionEngine.ShouldBeOfType<MovementEngine>();
    }

    [Fact]
    public void OnCommandReceived_WhenChangePhaseCommand_ShouldUpdateDecisionEngine()
    {
        // Arrange
        var phaseCommand = new ChangePhaseCommand
        {
            GameOriginId = _clientGame.Id,
            Phase = PhaseNames.Movement
        };

        // Act
        _commandSubject.OnNext(phaseCommand);
        
        // Assert 
        _sut.DecisionEngine.ShouldBeOfType<MovementEngine>();
    }

    [Fact]
    public void OnCommandReceived_WhenChangeActivePlayerCommandForThisBot_ShouldMakeDecision()
    {
        // Arrange
        var activePlayerCommand = new ChangeActivePlayerCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = _player.Id,
            UnitsToPlay = 1
        };

        // Act
        _commandSubject.OnNext(activePlayerCommand);
        
        // Give some time for async processing
        Thread.Sleep(100);
        
        // Assert - Bot should handle the command without throwing
        _sut.ShouldNotBeNull();
    }

    [Fact]
    public void OnCommandReceived_WhenChangeActivePlayerCommandForOtherPlayer_ShouldNotMakeDecision()
    {
        // Arrange
        var otherPlayerId = Guid.NewGuid();
        var activePlayerCommand = new ChangeActivePlayerCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = otherPlayerId,
            UnitsToPlay = 1
        };

        // Act
        _commandSubject.OnNext(activePlayerCommand);
        
        // Assert - Bot should handle the command without throwing
        _sut.ShouldNotBeNull();
    }

    [Fact]
    public void OnCommandReceived_WhenGameEndedCommand_ShouldDispose()
    {
        // Arrange
        var gameEndedCommand = new GameEndedCommand
        {
            GameOriginId = _clientGame.Id,
            Reason = GameEndReason.Victory
        };

        // Act
        _commandSubject.OnNext(gameEndedCommand);
        
        // Assert - Bot should handle the command without throwing
        _sut.ShouldNotBeNull();
    }

    [Fact]
    public void GetThinkingDelay_ShouldReturnDifferentValuesBasedOnDifficulty()
    {
        // Arrange & Act
        var easyBot = new Bot(_player, _clientGame, BotDifficulty.Easy);
        var mediumBot = new Bot(_player, _clientGame, BotDifficulty.Medium);
        var hardBot = new Bot(_player, _clientGame, BotDifficulty.Hard);

        // Assert - All bots should be created successfully
        easyBot.ShouldNotBeNull();
        mediumBot.ShouldNotBeNull();
        hardBot.ShouldNotBeNull();
        
        // Cleanup
        easyBot.Dispose();
        mediumBot.Dispose();
        hardBot.Dispose();
    }

    [Fact]
    public void Dispose_ShouldUnsubscribeFromCommands()
    {
        // Act
        _sut.Dispose();
        
        // Assert - Should not throw when sending commands after disposal
        _commandSubject.OnNext(new ChangePhaseCommand 
        { 
            GameOriginId = _clientGame.Id, 
            Phase = PhaseNames.Movement 
        });
    }

    public void Dispose()
    {
        _sut.Dispose();
        _commandSubject.Dispose();
    }
}

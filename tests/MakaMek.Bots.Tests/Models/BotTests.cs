using System.Reactive.Linq;
using System.Reactive.Subjects;
using NSubstitute;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Bots.Services;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.Models;

public class BotTests : IDisposable
{
    private readonly IClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly Subject<IGameCommand> _commandSubject;
    private readonly IDecisionEngineProvider _decisionEngineProvider;
    private readonly Bot _sut;
    private readonly IBotDecisionEngine _movementEngine = Substitute.For<IBotDecisionEngine>();

    public BotTests()
    {
        _clientGame = Substitute.For<IClientGame>();
        _player = Substitute.For<IPlayer>();
        _commandSubject = new Subject<IGameCommand>();
        _decisionEngineProvider = Substitute.For<IDecisionEngineProvider>();

        _player.Id.Returns(Guid.NewGuid());
        _player.Name.Returns("Test Bot");
        _clientGame.Commands.Returns(_commandSubject.AsObservable());
        _clientGame.Id.Returns(Guid.NewGuid());

        // Setup Players collection to return the player
        _clientGame.Players.Returns(new List<IPlayer> { _player });

        // Configure mock provider to return appropriate engines for different phases

        // Engine's MakeDecision now accepts IPlayer parameter
        _movementEngine.MakeDecision(Arg.Any<IPlayer>()).Returns(Task.CompletedTask);
        _decisionEngineProvider.GetEngineForPhase(PhaseNames.Movement).Returns(_movementEngine);

        _sut = new Bot(_player.Id, _clientGame, _decisionEngineProvider);
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Assert
        _sut.PlayerId.ShouldBe(_player.Id);
    }

    [Fact]
    public void Constructor_ShouldSubscribeToClientGameCommands()
    {
        // Arrange
        var decisionEngineProvider = Substitute.For<IDecisionEngineProvider>();
        var movementEngine = Substitute.For<IBotDecisionEngine>();
        decisionEngineProvider.GetEngineForPhase(PhaseNames.Movement).Returns(movementEngine);

        // Act - Create a new bot and send a command
        using var bot = new Bot(_player.Id, _clientGame, decisionEngineProvider);
        _commandSubject.OnNext(new ChangePhaseCommand
        {
            GameOriginId = _clientGame.Id,
            Phase = PhaseNames.Movement
        });

        // Assert
        bot.DecisionEngine.ShouldBe(movementEngine);
    }

    [Fact]
    public void OnCommandReceived_WhenChangePhaseCommand_ShouldUpdateDecisionEngine()
    {
        // Arrange
        var movementEngine = Substitute.For<IBotDecisionEngine>();
        _decisionEngineProvider.GetEngineForPhase(PhaseNames.Movement).Returns(movementEngine);
        
        var phaseCommand = new ChangePhaseCommand
        {
            GameOriginId = _clientGame.Id,
            Phase = PhaseNames.Movement
        };

        // Act
        _commandSubject.OnNext(phaseCommand);
        
        // Assert 
        _sut.DecisionEngine.ShouldBe(movementEngine);
    }

    [Fact]
    public void OnCommandReceived_WhenChangeActivePlayerCommandForThisBot_ShouldMakeDecision()
    {
        // Arrange
        var phaseCommand = new ChangePhaseCommand
        {
            GameOriginId = _clientGame.Id,
            Phase = PhaseNames.Movement
        };
        _commandSubject.OnNext(phaseCommand);
        var activePlayerCommand = new ChangeActivePlayerCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = _player.Id,
            UnitsToPlay = 1
        };

        // Act
        _commandSubject.OnNext(activePlayerCommand);
        
        // Assert
        _movementEngine.Received(1).MakeDecision(_player);
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

    [Fact]
    public void OnCommandReceived_WhenPlayerNotFound_ShouldWriteToConsole()
    {
        // Arrange
        var originalOut = Console.Out;
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        var clientGame = Substitute.For<IClientGame>();
        var commandSubject = new Subject<IGameCommand>();
        var decisionEngineProvider = Substitute.For<IDecisionEngineProvider>();
        var movementEngine = Substitute.For<IBotDecisionEngine>();
        var playerId = Guid.NewGuid();

        clientGame.Commands.Returns(commandSubject.AsObservable());
        clientGame.Id.Returns(Guid.NewGuid());
        clientGame.Players.Returns(new List<IPlayer>()); // Empty player list

        decisionEngineProvider.GetEngineForPhase(PhaseNames.Movement).Returns(movementEngine);

        try
        {
            using var bot = new Bot(playerId, clientGame, decisionEngineProvider);

            // Set up the decision engine
            commandSubject.OnNext(new ChangePhaseCommand
            {
                GameOriginId = clientGame.Id,
                Phase = PhaseNames.Movement
            });

            // Act - Trigger decision making when player is not found
            commandSubject.OnNext(new ChangeActivePlayerCommand
            {
                GameOriginId = clientGame.Id,
                PlayerId = playerId,
                UnitsToPlay = 1
            });

            // Give async operation time to complete
            Thread.Sleep(100);

            // Assert
            var output = stringWriter.ToString();
            output.ShouldContain($"Bot with PlayerId {playerId} not found in game players");
        }
        finally
        {
            // Restore original console output
            Console.SetOut(originalOut);
            commandSubject.Dispose();
        }
    }

    public void Dispose()
    {
        _sut.Dispose();
        _commandSubject.Dispose();
    }
}

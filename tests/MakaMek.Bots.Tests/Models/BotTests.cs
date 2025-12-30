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
    private readonly Subject<PhaseStepState?> _phaseStepChanges;
    private readonly Subject<PhaseNames> _phaseSubject;
    private readonly IDecisionEngineProvider _decisionEngineProvider;
    private readonly Bot _sut;
    private readonly IBotDecisionEngine _movementEngine = Substitute.For<IBotDecisionEngine>();

    public BotTests()
    {
        _clientGame = Substitute.For<IClientGame>();
        _player = Substitute.For<IPlayer>();
        _commandSubject = new Subject<IGameCommand>();
        _phaseStepChanges = new Subject<PhaseStepState?>();
        _phaseSubject = new Subject<PhaseNames>();
        _decisionEngineProvider = Substitute.For<IDecisionEngineProvider>();

        _player.Id.Returns(Guid.NewGuid());
        _player.Name.Returns("Test Bot");
        _clientGame.TurnPhase.Returns(PhaseNames.Movement);
        _clientGame.Commands.Returns(_commandSubject.AsObservable());
        _clientGame.PhaseStepChanges.Returns(_phaseStepChanges.AsObservable());
        _clientGame.PhaseChanges.Returns(_phaseSubject.AsObservable());
        _clientGame.Id.Returns(Guid.NewGuid());

        // Set up Players collection to return the player
        _clientGame.Players.Returns(new List<IPlayer> { _player });

        // Configure a mock provider to return appropriate engines for different phases

        // Engine's MakeDecision now accepts IPlayer parameter
        _movementEngine.MakeDecision(Arg.Any<IPlayer>()).Returns(Task.CompletedTask);
        _decisionEngineProvider.GetEngineForPhase(PhaseNames.Movement).Returns(_movementEngine);

        _sut = new Bot(_player.Id, _clientGame, _decisionEngineProvider,0);
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Assert
        _sut.PlayerId.ShouldBe(_player.Id);
    }

    [Fact]
    public void Constructor_ShouldSubscribeToClientGameObservables()
    {
        // Arrange
        var decisionEngineProvider = Substitute.For<IDecisionEngineProvider>();
        var movementEngine = Substitute.For<IBotDecisionEngine>();
        decisionEngineProvider.GetEngineForPhase(PhaseNames.Movement).Returns(movementEngine);

        // Act - Create a new bot and trigger phase change
        using var bot = new Bot(_player.Id, _clientGame, decisionEngineProvider);
        _phaseSubject.OnNext(PhaseNames.Movement);

        // Assert
        bot.DecisionEngine.ShouldBe(movementEngine);
    }

    [Fact]
    public void OnPhaseChanged_WhenPhaseChanges_ShouldUpdateDecisionEngine()
    {
        // Arrange
        var movementEngine = Substitute.For<IBotDecisionEngine>();
        _decisionEngineProvider.GetEngineForPhase(PhaseNames.Movement).Returns(movementEngine);

        // Act
        _phaseSubject.OnNext(PhaseNames.Movement);

        // Assert
        _sut.DecisionEngine.ShouldBe(movementEngine);
    }

    [Fact]
    public async Task OnActivePlayerChanged_WhenActivePlayerIsThisBot_ShouldMakeDecision()
    {
        // Arrange - Set up the decision engine first
        _phaseSubject.OnNext(PhaseNames.Movement);

        // Act
        _phaseStepChanges.OnNext(new PhaseStepState(PhaseNames.Movement, _player, 1));

        // Assert
        // wait for bg task to complete
        await Task.Delay(100);
        await _movementEngine.Received(1).MakeDecision(_player);
    }
    
    [Fact]
    public async Task OnActivePlayerChanged_WhenPhaseMismatch_ShouldNotMakeDecision()
    {
        // Arrange - Set up the decision engine first
        _clientGame.TurnPhase.Returns(PhaseNames.Initiative);
        _phaseSubject.OnNext(PhaseNames.Movement);

        // Act
        _phaseStepChanges.OnNext(new PhaseStepState(PhaseNames.Movement, _player, 1));

        // Assert
        // wait for bg task to complete
        await Task.Delay(100);
        await _movementEngine.DidNotReceive().MakeDecision(Arg.Any<IPlayer>());
    }

    [Fact]
    public void OnActivePlayerChanged_WhenActivePlayerIsOtherPlayer_ShouldNotMakeDecision()
    {
        // Arrange
        var otherPlayer = Substitute.For<IPlayer>();
        otherPlayer.Id.Returns(Guid.NewGuid());

        // Act
        _phaseStepChanges.OnNext(new PhaseStepState(PhaseNames.Movement, otherPlayer, 1));

        // Assert
        _movementEngine.DidNotReceive().MakeDecision(Arg.Any<IPlayer>());
    }

    [Fact]
    public void OnActivePlayerChanged_WhenActivePlayerIsNull_ShouldNotMakeDecision()
    {
        // Act
        _phaseStepChanges.OnNext(null);

        // Assert
        _movementEngine.DidNotReceive().MakeDecision(Arg.Any<IPlayer>());
    }

    [Fact]
    public void OnGameEnded_WhenGameEndedCommand_ShouldDispose()
    {
        // Arrange
        var gameEndedCommand = new GameEndedCommand
        {
            GameOriginId = _clientGame.Id,
            Reason = GameEndReason.Victory
        };

        // Set up the decision engine first
        _phaseSubject.OnNext(PhaseNames.Movement);

        // Act - Send GameEndedCommand through Commands observable
        _commandSubject.OnNext(gameEndedCommand);

        // Assert - Verify that the bot was disposed by checking that subsequent active player changes don't trigger decisions
        Should.NotThrow(() =>
        {
            _phaseSubject.OnNext(PhaseNames.Movement);
            _phaseStepChanges.OnNext(new PhaseStepState(PhaseNames.Movement, _player, 1));
        });
    }

    [Fact]
    public void Dispose_ShouldUnsubscribeFromObservables()
    {
        // Act
        _sut.Dispose();

        // Assert - Should not throw when triggering observables after disposal
        _phaseSubject.OnNext(PhaseNames.Movement);
        _phaseStepChanges.OnNext(new PhaseStepState(PhaseNames.Movement, _player, 1));
    }

    [Fact]
    public void Dispose_ShouldPreventActionsAfterDisposal()
    {
        // Arrange
        _phaseSubject.OnNext(PhaseNames.Movement);

        // Act
        _sut.Dispose();
        _phaseStepChanges.OnNext(new PhaseStepState(PhaseNames.Movement, _player, 1));

        // Assert - Should not make a decision after disposal
        _movementEngine.DidNotReceive().MakeDecision(Arg.Any<IPlayer>());
    }

    [Fact]
    public async Task OnActivePlayerChanged_WhenPlayerNotFound_ShouldWriteToConsole()
    {
        // Arrange
        var originalOut = Console.Out;
        await using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        var clientGame = Substitute.For<IClientGame>();
        var commandSubject = new Subject<IGameCommand>();
        var phaseStepChanges = new Subject<PhaseStepState?>();
        var phaseSubject = new Subject<PhaseNames>();
        var decisionEngineProvider = Substitute.For<IDecisionEngineProvider>();
        var movementEngine = Substitute.For<IBotDecisionEngine>();
        var playerId = Guid.NewGuid();
        var player = Substitute.For<IPlayer>();
        player.Id.Returns(playerId);

        clientGame.Commands.Returns(commandSubject.AsObservable());
        clientGame.TurnPhase.Returns(PhaseNames.Movement);
        clientGame.PhaseStepChanges.Returns(phaseStepChanges.AsObservable());
        clientGame.PhaseChanges.Returns(phaseSubject.AsObservable());
        clientGame.Id.Returns(Guid.NewGuid());
        clientGame.Players.Returns(new List<IPlayer>()); // Empty player list

        decisionEngineProvider.GetEngineForPhase(PhaseNames.Movement).Returns(movementEngine);

        try
        {
            using var bot = new Bot(playerId, clientGame, decisionEngineProvider);

            // Set up the decision engine
            phaseSubject.OnNext(PhaseNames.Movement);

            // Act - Trigger decision-making when a player is not found
            phaseStepChanges.OnNext(new PhaseStepState(PhaseNames.Movement, player, 1));

            // Give async operation time to complete - poll with timeout
            string output;
            var timeout = TimeSpan.FromSeconds(1);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                output = stringWriter.ToString();
                if (output.Contains($"Bot with PlayerId {playerId} not found"))
                    break;
                await Task.Delay(10);
            }

            // Assert
            output = stringWriter.ToString();
            output.ShouldContain($"Bot with PlayerId {playerId} not found in game players");
        }
        finally
        {
            // Restore original console output
            Console.SetOut(originalOut);
            commandSubject.Dispose();
            phaseStepChanges.Dispose();
            phaseSubject.Dispose();
        }
    }

    [Fact]
    public async Task OnActivePlayerChanged_InEndPhase_ShouldMakeDecision()
    {
        // Arrange - Simulate End Phase (client-driven)
        var endPhaseEngine = Substitute.For<IBotDecisionEngine>();
        endPhaseEngine.MakeDecision(Arg.Any<IPlayer>()).Returns(Task.CompletedTask);
        _decisionEngineProvider.GetEngineForPhase(PhaseNames.End).Returns(endPhaseEngine);
        _clientGame.TurnPhase.Returns(PhaseNames.End);

        // Act - Phase changes to End, then active player is set (client-driven)
        _phaseSubject.OnNext(PhaseNames.End);
        _phaseStepChanges.OnNext(new PhaseStepState(PhaseNames.End, _player, 0));

        // Assert - Bot should act in the End Phase
        // wait for bg task to complete
        await Task.Delay(100);
        await endPhaseEngine.Received(1).MakeDecision(_player);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _commandSubject.Dispose();
        _phaseStepChanges.Dispose();
        _phaseSubject.Dispose();
    }
}

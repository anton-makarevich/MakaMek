using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Bots.Services;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.Models;

public class BotTests : IDisposable
{
    private readonly IClientGame _clientGame= Substitute.For<IClientGame>();
    private readonly IPlayer _player = Substitute.For<IPlayer>();
    private readonly Subject<IGameCommand> _commandSubject;
    private readonly Subject<PhaseStepState?> _phaseStepChanges;
    private readonly Subject<PhaseNames> _phaseSubject;
    private readonly IDecisionEngineProvider _decisionEngineProvider = Substitute.For<IDecisionEngineProvider>();
    private readonly Bot _sut;
    private readonly IBotDecisionEngine _movementEngine = Substitute.For<IBotDecisionEngine>();

    public BotTests()
    {
        _commandSubject = new Subject<IGameCommand>();
        _phaseStepChanges = new Subject<PhaseStepState?>();
        _phaseSubject = new Subject<PhaseNames>();

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
        _movementEngine.MakeDecision(Arg.Any<IPlayer>(), Arg.Any<ITurnState>()).Returns(Task.CompletedTask);
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
        await _movementEngine.Received(1).MakeDecision(_player, Arg.Any<ITurnState>());
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
        await _movementEngine.DidNotReceive().MakeDecision(Arg.Any<IPlayer>(), Arg.Any<ITurnState>());
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
        _movementEngine.DidNotReceive().MakeDecision(Arg.Any<IPlayer>(), Arg.Any<ITurnState>());
    }

    [Fact]
    public void OnActivePlayerChanged_WhenActivePlayerIsNull_ShouldNotMakeDecision()
    {
        // Act
        _phaseStepChanges.OnNext(null);

        // Assert
        _movementEngine.DidNotReceive().MakeDecision(Arg.Any<IPlayer>(), Arg.Any<ITurnState>());
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
        _movementEngine.DidNotReceive().MakeDecision(Arg.Any<IPlayer>(), Arg.Any<ITurnState>());
    }

    [Fact]
    public async Task OnActivePlayerChanged_WhenPlayerNotFound_ShouldWriteToLog()
    {
        // Arrange
        var clientGame = Substitute.For<IClientGame>();
        var logger = Substitute.For<ILogger<ClientGame>>();
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
        clientGame.Logger.Returns(logger);

        decisionEngineProvider.GetEngineForPhase(PhaseNames.Movement).Returns(movementEngine);

        try
        {
            using var bot = new Bot(playerId, clientGame, decisionEngineProvider);

            // Set up the decision engine
            phaseSubject.OnNext(PhaseNames.Movement);

            // Act - Trigger decision-making when a player is not found
            phaseStepChanges.OnNext(new PhaseStepState(PhaseNames.Movement, player, 1));

            // Give async operation time to complete
            await Task.Delay(100);

            // Assert
            logger.Received(1).Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains($"Bot with PlayerId {playerId} not found in game players")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
        }
        finally
        {
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
        endPhaseEngine.MakeDecision(Arg.Any<IPlayer>(), Arg.Any<ITurnState>()).Returns(Task.CompletedTask);
        _decisionEngineProvider.GetEngineForPhase(PhaseNames.End).Returns(endPhaseEngine);
        _clientGame.TurnPhase.Returns(PhaseNames.End);

        // Act - Phase changes to End, then active player is set (client-driven)
        _phaseSubject.OnNext(PhaseNames.End);
        _phaseStepChanges.OnNext(new PhaseStepState(PhaseNames.End, _player, 0));

        // Assert - Bot should act in the End Phase
        // wait for bg task to complete
        await Task.Delay(100);
        await endPhaseEngine.Received(1).MakeDecision(_player, Arg.Any<ITurnState>());
    }

    [Fact]
    public async Task OnWeaponConfigurationCommand_WhenActivePlayerMatches_ShouldMakeDecision()
    {
        // Arrange
        var weaponsEngine = Substitute.For<IBotDecisionEngine>();
        weaponsEngine.MakeDecision(Arg.Any<IPlayer>(), Arg.Any<ITurnState>()).Returns(Task.CompletedTask);
        _decisionEngineProvider.GetEngineForPhase(PhaseNames.WeaponsAttack).Returns(weaponsEngine);
        
        _clientGame.TurnPhase.Returns(PhaseNames.WeaponsAttack);
        _clientGame.PhaseStepState.Returns(new PhaseStepState(PhaseNames.WeaponsAttack, _player, 1));

        var weaponConfigCommand = new WeaponConfigurationCommand
        {
            PlayerId = _player.Id,
            UnitId = Guid.NewGuid(),
            Configuration = new WeaponConfiguration
            {
                Type = WeaponConfigurationType.TorsoRotation,
                Value = 1
            },
            GameOriginId = _clientGame.Id,
            Timestamp = DateTime.UtcNow
        };

        // Set up the decision engine
        _phaseSubject.OnNext(PhaseNames.WeaponsAttack);

        // Act
        _commandSubject.OnNext(weaponConfigCommand);

        // Assert
        // wait for bg task to complete
        await Task.Delay(100);
        await weaponsEngine.Received(1).MakeDecision(_player, Arg.Is<ITurnState>(ts => ts.PhaseActiveUnitId == weaponConfigCommand.UnitId));
    }

    [Fact]
    public async Task OnWeaponConfigurationCommand_WhenActivePlayerIsDifferent_ShouldNotMakeDecision()
    {
        // Arrange
        var weaponsEngine = Substitute.For<IBotDecisionEngine>();
        weaponsEngine.MakeDecision(Arg.Any<IPlayer>(), Arg.Any<ITurnState>()).Returns(Task.CompletedTask);
        _decisionEngineProvider.GetEngineForPhase(PhaseNames.WeaponsAttack).Returns(weaponsEngine);
        
        var otherPlayer = Substitute.For<IPlayer>();
        otherPlayer.Id.Returns(Guid.NewGuid());
        
        _clientGame.TurnPhase.Returns(PhaseNames.WeaponsAttack);
        _clientGame.PhaseStepState.Returns(new PhaseStepState(PhaseNames.WeaponsAttack, otherPlayer, 1));

        var weaponConfigCommand = new WeaponConfigurationCommand
        {
            PlayerId = _player.Id,
            UnitId = Guid.NewGuid(),
            Configuration = new WeaponConfiguration
            {
                Type = WeaponConfigurationType.TorsoRotation,
                Value = 1
            },
            GameOriginId = _clientGame.Id,
            Timestamp = DateTime.UtcNow
        };

        // Set up the decision engine
        _phaseSubject.OnNext(PhaseNames.WeaponsAttack);

        // Act
        _commandSubject.OnNext(weaponConfigCommand);

        // Assert
        // wait for bg task to complete
        await Task.Delay(100);
        await weaponsEngine.DidNotReceive().MakeDecision(Arg.Any<IPlayer>(), Arg.Any<ITurnState>());
    }

    [Fact]
    public async Task MakeDecision_ShouldPassTurnStateWithCorrectGameIdAndTurnNumber()
    {
        // Arrange
        ITurnState? capturedTurnState = null;
        _movementEngine.MakeDecision(Arg.Any<IPlayer>(),
            Arg.Do<ITurnState>(ts => capturedTurnState = ts)).Returns(Task.CompletedTask);
        
        // Set up game properties
        var expectedGameId = Guid.NewGuid();
        const int expectedTurnNumber = 5;
        _clientGame.Id.Returns(expectedGameId);
        _clientGame.Turn.Returns(expectedTurnNumber);

        // Act - Trigger decision-making
        _phaseSubject.OnNext(PhaseNames.Movement);
        _phaseStepChanges.OnNext(new PhaseStepState(PhaseNames.Movement, _player, 1));

        // wait for bg task to complete
        await Task.Delay(100);

        // Assert
        capturedTurnState.ShouldNotBeNull();
        capturedTurnState.PhaseActiveUnitId.ShouldBeNull();
        capturedTurnState.GameId.ShouldBe(expectedGameId);
        capturedTurnState.TurnNumber.ShouldBe(expectedTurnNumber);
    }

    [Fact]
    public async Task OnTurnIncrementedCommand_ShouldResetTurnStateWithNewTurnNumber()
    {
        // Arrange
        ITurnState? capturedTurnState = null;
        _movementEngine.MakeDecision(Arg.Any<IPlayer>(), 
            Arg.Do<ITurnState>(ts => capturedTurnState = ts)).Returns(Task.CompletedTask);
        
        var expectedGameId = Guid.NewGuid();
        const int initialTurnNumber = 3;
        const int newTurnNumber = 4;
        _clientGame.Id.Returns(expectedGameId);
        _clientGame.Turn.Returns(initialTurnNumber);

        // Set up the initial phase and make the first decision to establish the initial state
        _phaseSubject.OnNext(PhaseNames.Movement);
        _phaseStepChanges.OnNext(new PhaseStepState(PhaseNames.Movement, _player, 1));
        await Task.Delay(100);
        
        // Reset captured state for next test
        capturedTurnState = null;
        _clientGame.Turn.Returns(newTurnNumber);

        // Act - Send TurnIncrementedCommand and trigger a new decision
        var turnIncrementedCommand = new TurnIncrementedCommand
        {
            GameOriginId = expectedGameId,
            TurnNumber = newTurnNumber,
            Timestamp = DateTime.UtcNow
        };
        _commandSubject.OnNext(turnIncrementedCommand);
        _phaseStepChanges.OnNext(new PhaseStepState(PhaseNames.Movement, _player, 1));

        // wait for bg task to complete
        await Task.Delay(100);

        // Assert
        capturedTurnState.ShouldNotBeNull();
        capturedTurnState.GameId.ShouldBe(expectedGameId);
        capturedTurnState.TurnNumber.ShouldBe(newTurnNumber);
    }

    [Fact]
    public async Task OnMechStandUpCommand_WhenUnitBelongsToBot_ShouldMakeDecision()
    {
        // Arrange
        var movementEngine = Substitute.For<IBotDecisionEngine>();
        movementEngine.MakeDecision(Arg.Any<IPlayer>(), Arg.Any<ITurnState>()).Returns(Task.CompletedTask);
        _decisionEngineProvider.GetEngineForPhase(PhaseNames.Movement).Returns(movementEngine);

        _clientGame.TurnPhase.Returns(PhaseNames.Movement);
        _clientGame.PhaseStepState.Returns(new PhaseStepState(PhaseNames.Movement, _player, 1));
        
        // Setup unit belonging to bot
        var unitId = Guid.NewGuid();
        var botUnit = Substitute.For<IUnit>();
        botUnit.Id.Returns(unitId);
        _player.Units.Returns(new List<IUnit> { botUnit });

        var standUpCommand = new MechStandUpCommand
        {
            GameOriginId = _clientGame.Id,
            UnitId = unitId,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollType = PilotingSkillRollType.GyroHit,
                DiceResults = [],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown
                {
                    BasePilotingSkill = 0,
                    Modifiers = []
                }
            }, // Dummy data
            NewFacing = HexDirection.Top,
            Timestamp = DateTime.UtcNow
        };

        // Set up the decision engine
        _phaseSubject.OnNext(PhaseNames.Movement);

        // Act
        _commandSubject.OnNext(standUpCommand);

        // Assert
        // wait for bg task to complete
        await Task.Delay(100);
        await movementEngine.Received(1).MakeDecision(_player, Arg.Is<ITurnState>(ts => ts.PhaseActiveUnitId == unitId));
    }

    [Fact]
    public async Task OnMechStandUpCommand_WhenUnitDoesNotBelongToBot_ShouldNotMakeDecision()
    {
        // Arrange
        var movementEngine = Substitute.For<IBotDecisionEngine>();
        movementEngine.MakeDecision(Arg.Any<IPlayer>(), Arg.Any<ITurnState>()).Returns(Task.CompletedTask);
        _decisionEngineProvider.GetEngineForPhase(PhaseNames.Movement).Returns(movementEngine);

        _clientGame.TurnPhase.Returns(PhaseNames.Movement);
        _clientGame.PhaseStepState.Returns(new PhaseStepState(PhaseNames.Movement, _player, 1));

        // Set up a unit NOT belonging to bot
        var unitId = Guid.NewGuid(); // Unit ID
        _player.Units.Returns(new List<IUnit>()); // Bot has no units

        var standUpCommand = new MechStandUpCommand
        {
            GameOriginId = _clientGame.Id, 
            UnitId = unitId, // Some other unit
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollType = PilotingSkillRollType.GyroHit,
                DiceResults = [],
                IsSuccessful = false,
                PsrBreakdown = new PsrBreakdown
                {
                    BasePilotingSkill = 0,
                    Modifiers = []
                }
            },
            NewFacing = HexDirection.Top,
            Timestamp = DateTime.UtcNow
        };

        // Set up the decision engine
        _phaseSubject.OnNext(PhaseNames.Movement);

        // Act
        _commandSubject.OnNext(standUpCommand);

        // Assert
        // wait for bg task to complete
        await Task.Delay(100);
        await movementEngine.DidNotReceive().MakeDecision(Arg.Any<IPlayer>(), Arg.Any<ITurnState>());
    }
    
    [Fact]
    public async Task OnMechFallCommand_WhenUnitBelongsToBot_ShouldMakeDecision()
    {
        // Arrange
        var movementEngine = Substitute.For<IBotDecisionEngine>();
        movementEngine.MakeDecision(Arg.Any<IPlayer>(), Arg.Any<ITurnState>()).Returns(Task.CompletedTask);
        _decisionEngineProvider.GetEngineForPhase(PhaseNames.Movement).Returns(movementEngine);

        _clientGame.TurnPhase.Returns(PhaseNames.Movement);
        _clientGame.PhaseStepState.Returns(new PhaseStepState(PhaseNames.Movement, _player, 1));
        
        // Setup unit belonging to bot
        var unitId = Guid.NewGuid();
        var botUnit = Substitute.For<IUnit>();
        botUnit.Id.Returns(unitId);
        _player.Units.Returns(new List<IUnit> { botUnit });

        var fallCommand = new MechFallCommand
        {
            GameOriginId = _clientGame.Id,
            UnitId = unitId,
            Timestamp = DateTime.UtcNow,
            DamageData = null
        };

        // Set up the decision engine
        _phaseSubject.OnNext(PhaseNames.Movement);

        // Act
        _commandSubject.OnNext(fallCommand);

        // Assert
        // wait for bg task to complete
        await Task.Delay(100);
        await movementEngine.Received(1).MakeDecision(_player, Arg.Is<ITurnState>(ts => ts.PhaseActiveUnitId == unitId));
    }

    [Fact]
    public async Task OnMechFallCommand_WhenUnitDoesNotBelongToBot_ShouldNotMakeDecision()
    {
        // Arrange
        var movementEngine = Substitute.For<IBotDecisionEngine>();
        movementEngine.MakeDecision(Arg.Any<IPlayer>(), Arg.Any<ITurnState>()).Returns(Task.CompletedTask);
        _decisionEngineProvider.GetEngineForPhase(PhaseNames.Movement).Returns(movementEngine);

        _clientGame.TurnPhase.Returns(PhaseNames.Movement);
        _clientGame.PhaseStepState.Returns(new PhaseStepState(PhaseNames.Movement, _player, 1));

        // Set up a unit NOT belonging to bot
        var unitId = Guid.NewGuid(); // Unit ID
        _player.Units.Returns(new List<IUnit>()); // Bot has no units

        var fallCommand = new MechFallCommand
        {
            GameOriginId = _clientGame.Id,
            UnitId = unitId, // Some other unit
            Timestamp = DateTime.UtcNow,
            DamageData = null
        };

        // Set up the decision engine
        _phaseSubject.OnNext(PhaseNames.Movement);

        // Act
        _commandSubject.OnNext(fallCommand);

        // Assert
        // wait for bg task to complete
        await Task.Delay(100);
        await movementEngine.DidNotReceive().MakeDecision(Arg.Any<IPlayer>(), Arg.Any<ITurnState>());
    }
    
    [Fact]
    public async Task OnMechFallCommand_WhenNotMovementPhase_ShouldNotMakeDecision()
    {
        // Arrange
        const PhaseNames phase = PhaseNames.WeaponsAttack;
        var decisionEngine = Substitute.For<IBotDecisionEngine>();
        decisionEngine.MakeDecision(Arg.Any<IPlayer>(), Arg.Any<ITurnState>()).Returns(Task.CompletedTask);
        _decisionEngineProvider.GetEngineForPhase(phase).Returns(decisionEngine);

        _clientGame.TurnPhase.Returns(phase);
        _clientGame.PhaseStepState.Returns(new PhaseStepState(phase, _player, 1));
        
        // Setup unit belonging to bot
        var unitId = Guid.NewGuid();
        var botUnit = Substitute.For<IUnit>();
        botUnit.Id.Returns(unitId);
        _player.Units.Returns(new List<IUnit> { botUnit });

        var fallCommand = new MechFallCommand
        {
            GameOriginId = _clientGame.Id,
            UnitId = unitId,
            Timestamp = DateTime.UtcNow,
            DamageData = null
        };

        // Set up the decision engine
        _phaseSubject.OnNext(phase);

        // Act
        _commandSubject.OnNext(fallCommand);

        // Assert
        // wait for bg task to complete
        await Task.Delay(100);
        await decisionEngine.DidNotReceive().MakeDecision(Arg.Any<IPlayer>(), Arg.Any<ITurnState>());
    }

    public void Dispose()
    {
        _sut.Dispose();
        _commandSubject.Dispose();
        _phaseStepChanges.Dispose();
        _phaseSubject.Dispose();
    }
}

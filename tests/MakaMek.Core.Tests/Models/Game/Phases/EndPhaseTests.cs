using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Phases;

public class EndPhaseTests : GamePhaseTestsBase
{
    private readonly EndPhase _sut;
    private readonly Guid _player1Id = Guid.NewGuid();
    private readonly Guid _player2Id = Guid.NewGuid();
    private readonly Guid _player3Id = Guid.NewGuid();
    private readonly IGamePhase _mockNextPhase;

    public EndPhaseTests()
    {
        // Create mock next phase and configure the phase manager
        _mockNextPhase = Substitute.For<IGamePhase>();
        MockPhaseManager.GetNextPhase(PhaseNames.End, Game).Returns(_mockNextPhase);
        
        // Add three players
        Game.HandleCommand(CreateJoinCommand(_player1Id, "Player 1"));
        Game.HandleCommand(CreateJoinCommand(_player2Id, "Player 2"));
        Game.HandleCommand(CreateJoinCommand(_player3Id, "Player 3"));
        
        // Set all players to Playing status
        Game.HandleCommand(CreateStatusCommand(_player1Id, PlayerStatus.Ready));
        Game.HandleCommand(CreateStatusCommand(_player2Id, PlayerStatus.Ready));
        Game.HandleCommand(CreateStatusCommand(_player3Id, PlayerStatus.Ready));
        
        // Set initiative order
        var players = Game.Players.ToList();
        Game.SetInitiativeOrder(new List<IPlayer> { players[0], players[1], players[2] });
        
        // Clear any commands published during setup
        CommandPublisher.ClearReceivedCalls();
        
        // Create the EndPhase
        _sut = new EndPhase(Game);
    }
    
    private static LocationHitData CreateHitDataForLocation(PartLocation partLocation,
        int damage,
        int[]? aimedShotRoll = null,
        int[]? locationRoll = null)
    {
        return new LocationHitData(
        [
            new LocationDamageData(partLocation,
                damage-1,
                1,
                false)
        ], aimedShotRoll??[], locationRoll??[], partLocation);
    }
    
    [Fact]
    public void HandleCommand_ShouldIncrementTurnAndTransitionToNextPhase_WhenAllPlayersEndTurn()
    {
        // Arrange
        _sut.Enter();
        var initialTurn = Game.Turn;
        
        // First player ends turn
        _sut.HandleCommand(new TurnEndedCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            Timestamp = DateTime.UtcNow
        });
        
        // Second player ends turn
        _sut.HandleCommand(new TurnEndedCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player2Id,
            Timestamp = DateTime.UtcNow
        });
        
        CommandPublisher.ClearReceivedCalls();
        
        // Act - Last player ends turn
        _sut.HandleCommand(new TurnEndedCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player3Id,
            Timestamp = DateTime.UtcNow
        });
        
        // Assert
        Game.Turn.ShouldBe(initialTurn + 1);
        
        // Verify the phase manager was called to get the next phase
        MockPhaseManager.Received(1).GetNextPhase(PhaseNames.End, Game);
        
        // Verify the mock next phase was entered
        _mockNextPhase.Received(1).Enter();
        
        // Verify TurnIncrementedCommand was published
        CommandPublisher.Received(1).PublishCommand(
            Arg.Is<TurnIncrementedCommand>(cmd => 
                cmd.TurnNumber == initialTurn + 1 && 
                cmd.GameOriginId == Game.Id));
    }
    
    [Fact]
    public void HandleCommand_ShouldIgnoreNonTurnEndedCommands()
    {
        // Arrange
        _sut.Enter();
        CommandPublisher.ClearReceivedCalls();
        
        // Act - Send a different command type
        _sut.HandleCommand(new MoveUnitCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = Guid.NewGuid(),
            MovementType = MovementType.Walk,
            MovementPath = []
        });
        
        // Assert - No changes should occur
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<ChangeActivePlayerCommand>());
    }
    
    [Fact]
    public void HandleCommand_ShouldBroadcastTurnEndedCommand_WhenPlayerEndsTurn()
    {
        // Arrange
        _sut.Enter();
        CommandPublisher.ClearReceivedCalls();
        
        var turnEndedCommand = new TurnEndedCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            Timestamp = DateTime.UtcNow
        };
        
        // Act
        _sut.HandleCommand(turnEndedCommand);
        
        // Assert
        // Verify the command was broadcasted back to clients with the game ID
        CommandPublisher.Received(1).PublishCommand(
            Arg.Is<TurnEndedCommand>(cmd => 
                cmd.PlayerId == _player1Id && 
                cmd.GameOriginId == Game.Id));
        
        // Verify we didn't transition to the next phase yet (since not all players ended their turn)
        MockPhaseManager.DidNotReceive().GetNextPhase(Arg.Is(PhaseNames.End), Arg.Any<ServerGame>());
    }
    
    [Fact]
    public void HandleCommand_ShouldCallOnTurnEnded_WhenPlayerEndsTurn()
    {
        // Arrange
        _sut.Enter();
        
        // Add a unit to the player
        var unit = Game.Players.First(p => p.Id == _player1Id).Units[0];
        unit.Deploy(new HexPosition(new HexCoordinates(1,1), HexDirection.Bottom));
        unit.Move(MovementType.Walk, [new PathSegmentData
            {
                From = new HexPositionData
                {
                    Coordinates = new HexCoordinateData(1,
                        1),
                    Facing = 3
                },
                To =  new HexPositionData
                {
                    Coordinates = new HexCoordinateData(1,
                        2),
                    Facing = 3
                },
                Cost = 1
            }
        ]);
        
        unit.MovementTypeUsed.ShouldBe(MovementType.Walk);
        
        var turnEndedCommand = new TurnEndedCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            Timestamp = DateTime.UtcNow
        };
        
        // Act
        _sut.HandleCommand(turnEndedCommand);
        
        // Assert
        // Verify the unit's turn state was reset
        unit.MovementTypeUsed.ShouldBeNull();
    }

    [Fact]
    public void Enter_ShouldRecoverConsciousness_AndPublishCommand_WhenTheRollIsSuccessful()
    {
        // Arrange
        var unit = Game.Players.First(p => p.Id == _player1Id).Units[0];
        var pilot = unit.Pilot;
        pilot!.KnockUnconscious(0);
        var consciousnessCommand = new PilotConsciousnessRollCommand
        {
            GameOriginId = Guid.NewGuid(),
            PilotId = pilot.Id,
            UnitId = unit.Id,
            IsRecoveryAttempt = true,
            ConsciousnessNumber = 4,
            DiceResults = [7, 2],
            IsSuccessful = true 
        };
        MockConsciousnessCalculator.MakeRecoveryConsciousnessRoll(pilot).Returns(consciousnessCommand);
        // Act
        _sut.Enter();
        
        // Assert
        pilot.IsConscious.ShouldBeTrue();
        CommandPublisher.Received(1).PublishCommand(
            Arg.Is<PilotConsciousnessRollCommand>(cmd => 
                cmd.GameOriginId == Game.Id &&
                cmd.IsRecoveryAttempt == true &&
                cmd.IsSuccessful == true));
    }
    
    [Fact]
    public void Enter_ShouldNotRecoverConsciousness_AndDontPublishCommand_WhenTheRollIsNotHappening()
    {
        // Arrange
        var unit = Game.Players.First(p => p.Id == _player1Id).Units[0];
        var pilot = unit.Pilot;
        pilot!.KnockUnconscious(0);
        
        PilotConsciousnessRollCommand? consciousnessCommand = null;
        
        MockConsciousnessCalculator.MakeRecoveryConsciousnessRoll(pilot).Returns(consciousnessCommand);
        // Act
        _sut.Enter();
        
        // Assert
        pilot.IsConscious.ShouldBeFalse();
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<PilotConsciousnessRollCommand>());
    }

    [Fact]
    public void HandleCommand_ShouldShutdownUnit_WhenShutdownUnitCommandReceived()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.Players.First(p => p.Id == _player1Id).Units[0];
        unit.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));

        CommandPublisher.ClearReceivedCalls();

        var shutdownCommand = new ShutdownUnitCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = unit.Id,
            Timestamp = DateTime.UtcNow
        };

        // Act
        _sut.HandleCommand(shutdownCommand);

        // Assert
        CommandPublisher.Received(1).PublishCommand(
            Arg.Is<UnitShutdownCommand>(cmd =>
                cmd.UnitId == unit.Id &&
                cmd.GameOriginId == Game.Id &&
                cmd.ShutdownData.Reason == ShutdownReason.Voluntary &&
                cmd.ShutdownData.Turn == Game.Turn &&
                cmd.AvoidShutdownRoll == null &&
                cmd.IsAutomaticShutdown == false));

        unit.IsShutdown.ShouldBeTrue();
        unit.CurrentShutdownData.ShouldNotBeNull();
        unit.CurrentShutdownData!.Value.Reason.ShouldBe(ShutdownReason.Voluntary);
    }

    [Fact]
    public void HandleCommand_ShouldIgnoreShutdownCommand_WhenPlayerNotFound()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.Players.First(p => p.Id == _player1Id).Units[0];
        unit.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        CommandPublisher.ClearReceivedCalls();

        var shutdownCommand = new ShutdownUnitCommand
        {
            GameOriginId = Game.Id,
            PlayerId = Guid.NewGuid(), // Non-existent player
            UnitId = unit.Id,
            Timestamp = DateTime.UtcNow
        };

        // Act
        _sut.HandleCommand(shutdownCommand);

        // Assert
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<UnitShutdownCommand>());
    }

    [Fact]
    public void HandleCommand_ShouldIgnoreShutdownCommand_WhenUnitNotFound()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.Players.First(p => p.Id == _player1Id).Units[0];
        unit.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        CommandPublisher.ClearReceivedCalls();

        var shutdownCommand = new ShutdownUnitCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = Guid.NewGuid(), // Non-existent unit
            Timestamp = DateTime.UtcNow
        };

        // Act
        _sut.HandleCommand(shutdownCommand);

        // Assert
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<UnitShutdownCommand>());
    }

    [Fact]
    public void HandleCommand_ShouldIgnoreShutdownCommand_WhenUnitAlreadyShutdown()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.Players.First(p => p.Id == _player1Id).Units[0];
        unit.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));

        // Shutdown the unit first
        unit.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = Game.Turn });

        CommandPublisher.ClearReceivedCalls();

        var shutdownCommand = new ShutdownUnitCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = unit.Id,
            Timestamp = DateTime.UtcNow
        };

        // Act
        _sut.HandleCommand(shutdownCommand);

        // Assert
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<UnitShutdownCommand>());
    }

    [Fact]
    public void HandleCommand_ShouldIgnoreShutdownCommand_WhenUnitDestroyed()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.Players.First(p => p.Id == _player1Id).Units[0];
        unit.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));

        // Destroy the unit
        unit.ApplyDamage([
            CreateHitDataForLocation(PartLocation.CenterTorso, 100, [],[])
        ], HitDirection.Front);

        CommandPublisher.ClearReceivedCalls();

        var shutdownCommand = new ShutdownUnitCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = unit.Id,
            Timestamp = DateTime.UtcNow
        };

        // Act
        _sut.HandleCommand(shutdownCommand);

        // Assert
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<UnitShutdownCommand>());
    }

    [Fact]
    public void HandleCommand_ShouldProcessStartupCommand_WhenValidRequest()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.Players.First(p => p.Id == _player1Id).Units[0];
        unit.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));

        // Shutdown the unit first
        unit.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = Game.Turn - 1 });

        // Mock the heat effects calculator to return a startup command
        var mockStartupCommand = new UnitStartupCommand
        {
            UnitId = unit.Id,
            IsAutomaticRestart = false,
            IsRestartPossible = true,
            AvoidShutdownRoll = new AvoidShutdownRollData
            {
                HeatLevel = 15,
                DiceResults = [4, 5],
                AvoidNumber = 8,
                IsSuccessful = true
            },
            GameOriginId = Guid.Empty
        };

        MockHeatEffectsCalculator.AttemptRestart(Arg.Any<Mech>(), Arg.Any<int>())
            .Returns(mockStartupCommand);

        CommandPublisher.ClearReceivedCalls();

        var startupCommand = new StartupUnitCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = unit.Id
        };

        // Act
        _sut.HandleCommand(startupCommand);

        // Assert
        CommandPublisher.Received(1).PublishCommand(
            Arg.Is<UnitStartupCommand>(cmd =>
                cmd.UnitId == unit.Id &&
                cmd.GameOriginId == Game.Id));
        // Verify the calculator was invoked with the correct mech and turn
        MockHeatEffectsCalculator.Received(1).AttemptRestart(
            Arg.Is<Mech>(m => m.Id == unit.Id),
            Game.Turn);

        // Verify the unit has actually started up (successful restart)
        unit.IsShutdown.ShouldBeFalse();
    }

    [Fact]
    public void HandleCommand_ShouldIgnoreStartupCommand_WhenPlayerNotFound()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.Players.First(p => p.Id == _player1Id).Units[0];
        unit.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        unit.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = Game.Turn - 1 });
        CommandPublisher.ClearReceivedCalls();

        var startupCommand = new StartupUnitCommand
        {
            GameOriginId = Game.Id,
            PlayerId = Guid.NewGuid(), // Non-existent player
            UnitId = unit.Id
        };

        // Act
        _sut.HandleCommand(startupCommand);

        // Assert
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<UnitStartupCommand>());
        MockHeatEffectsCalculator.DidNotReceive().AttemptRestart(Arg.Any<Mech>(), Arg.Any<int>());
    }

    [Fact]
    public void HandleCommand_ShouldIgnoreStartupCommand_WhenUnitNotFound()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.Players.First(p => p.Id == _player1Id).Units[0];
        unit.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        unit.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = Game.Turn - 1 });
        CommandPublisher.ClearReceivedCalls();

        var startupCommand = new StartupUnitCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = Guid.NewGuid() // Non-existent unit
        };

        // Act
        _sut.HandleCommand(startupCommand);

        // Assert
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<UnitStartupCommand>());
        MockHeatEffectsCalculator.DidNotReceive().AttemptRestart(Arg.Any<Mech>(), Arg.Any<int>());
    }

    [Fact]
    public void HandleCommand_ShouldIgnoreStartupCommand_WhenUnitNotShutdown()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.Players.First(p => p.Id == _player1Id).Units[0];
        unit.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        // Unit is not shutdown
        CommandPublisher.ClearReceivedCalls();

        var startupCommand = new StartupUnitCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = unit.Id
        };

        // Act
        _sut.HandleCommand(startupCommand);

        // Assert
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<UnitStartupCommand>());
        MockHeatEffectsCalculator.DidNotReceive().AttemptRestart(Arg.Any<Mech>(), Arg.Any<int>());
    }

    [Fact]
    public void HandleCommand_ShouldIgnoreStartupCommand_WhenUnitDestroyed()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.Players.First(p => p.Id == _player1Id).Units[0];
        unit.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));

        // Destroy the unit
        unit.ApplyDamage([new LocationHitData(
            PartLocation.CenterTorso,
            100,
            [],
            [])], HitDirection.Front);

        CommandPublisher.ClearReceivedCalls();

        var startupCommand = new StartupUnitCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = unit.Id
        };

        // Act
        _sut.HandleCommand(startupCommand);

        // Assert
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<UnitStartupCommand>());
        MockHeatEffectsCalculator.DidNotReceive().AttemptRestart(Arg.Any<Mech>(), Arg.Any<int>());
    }

    [Fact]
    public void HandleCommand_ShouldIgnoreStartupCommand_WhenHeatEffectsCalculatorReturnsNull()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.Players.First(p => p.Id == _player1Id).Units[0];
        unit.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        unit.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = Game.Turn - 1 });

        // Mock the heat effects calculator to return null (startup not possible)
        MockHeatEffectsCalculator.AttemptRestart(Arg.Any<Mech>(), Arg.Any<int>())
            .Returns((UnitStartupCommand?)null);

        CommandPublisher.ClearReceivedCalls();

        var startupCommand = new StartupUnitCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            UnitId = unit.Id
        };

        // Act
        _sut.HandleCommand(startupCommand);

        // Assert
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<UnitStartupCommand>());
        MockHeatEffectsCalculator.Received(1).AttemptRestart(
            Arg.Is<Mech>(m => m.Id == unit.Id),
            Game.Turn);
    }
}

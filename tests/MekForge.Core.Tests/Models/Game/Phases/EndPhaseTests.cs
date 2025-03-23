using NSubstitute;
using Sanet.MekForge.Core.Data.Game;
using Sanet.MekForge.Core.Data.Map;
using Sanet.MekForge.Core.Models.Game;
using Sanet.MekForge.Core.Models.Game.Commands.Client;
using Sanet.MekForge.Core.Models.Game.Commands.Server;
using Sanet.MekForge.Core.Models.Game.Phases;
using Sanet.MekForge.Core.Models.Game.Players;
using Sanet.MekForge.Core.Models.Map;
using Sanet.MekForge.Core.Models.Units;
using Shouldly;

namespace Sanet.MekForge.Core.Tests.Models.Game.Phases;

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
        Game.HandleCommand(CreateStatusCommand(_player1Id, PlayerStatus.Playing));
        Game.HandleCommand(CreateStatusCommand(_player2Id, PlayerStatus.Playing));
        Game.HandleCommand(CreateStatusCommand(_player3Id, PlayerStatus.Playing));
        
        // Set initiative order
        var players = Game.Players.ToList();
        Game.SetInitiativeOrder(new List<IPlayer> { players[0], players[1], players[2] });
        
        // Clear any commands published during setup
        CommandPublisher.ClearReceivedCalls();
        
        // Create the EndPhase
        _sut = new EndPhase(Game);
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
        var unit = Game.Players.First(p => p.Id == _player1Id).Units.First();
        unit.Deploy(new HexPosition(new HexCoordinates(1,1), HexDirection.Bottom));
        unit.Move(MovementType.Walk, [new PathSegmentData
            {
                From = new HexPositionData
                {
                    Coordinates = new HexCoordinateData(1,
                        1),
                    Facing = 3,
                },
                To =  new HexPositionData
                {
                    Coordinates = new HexCoordinateData(1,
                        2),
                    Facing = 3,
                },
                Cost = 1
            }
        ]);
        
        unit.MovementTypeUsed.ShouldBe(MovementType.Walk);
        
        var turnEndedCommand = new TurnEndedCommand
        {
            PlayerId = _player1Id,
            Timestamp = DateTime.UtcNow
        };
        
        // Act
        _sut.HandleCommand(turnEndedCommand);
        
        // Assert
        // Verify the unit's turn state was reset
        unit.MovementTypeUsed.ShouldBeNull();
    }
}

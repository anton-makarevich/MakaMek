using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Phases;

public class StartPhaseTests : GamePhaseTestsBase
{
    private readonly StartPhase _sut;
    private readonly IGamePhase _mockNextPhase;

    public StartPhaseTests()
    {
        // Create mock next phase and configure the phase manager
        _mockNextPhase = Substitute.For<IGamePhase>();
        MockPhaseManager.GetNextPhase(PhaseNames.Start, Game).Returns(_mockNextPhase);
        
        _sut = new StartPhase(Game);
    }

    [Fact]
    public void Name_ShouldBeStart()
    {
        _sut.Name.ShouldBe(PhaseNames.Start);
    }

    [Fact]
    public void HandleCommand_WhenPlayerJoins_ShouldAddPlayerToGame()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var joinCommand = CreateJoinCommand(playerId, "Player 1");

        // Act
        _sut.HandleCommand(joinCommand);

        // Assert
        Game.Players.Count.ShouldBe(1);
        Game.Players[0].Id.ShouldBe(playerId);
        Game.Players[0].Name.ShouldBe("Player 1");
        Game.Players[0].Units.Count.ShouldBe(1);
    }

    [Fact]
    public void HandleCommand_WhenAllPlayersReady_ShouldTransitionToNextPhase()
    {
        // Arrange
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        
        // Add two players
        _sut.HandleCommand(CreateJoinCommand(player1Id, "Player 1"));
        _sut.HandleCommand(CreateJoinCommand(player2Id, "Player 2"));
        SetMap();

        // Act
        // Set both players ready
        _sut.HandleCommand(CreateStatusCommand(player1Id, PlayerStatus.Ready));
        _sut.HandleCommand(CreateStatusCommand(player2Id, PlayerStatus.Ready));

        // Assert
        MockPhaseManager.Received(1).GetNextPhase(PhaseNames.Start, Game);
        _mockNextPhase.Received(1).Enter();
    }

    [Fact]
    public void HandleCommand_WhenNotAllPlayersReady_ShouldStayInStartPhase()
    {
        // Arrange
        // Add two players
        _sut.HandleCommand(CreateJoinCommand(Guid.NewGuid(), "Player 1"));
        _sut.HandleCommand(CreateJoinCommand(Guid.NewGuid(), "Player 2"));

        // Act
        // Set only one player ready
        _sut.HandleCommand(CreateStatusCommand(Guid.NewGuid(), PlayerStatus.Ready));

        // Assert
        MockPhaseManager.DidNotReceive().GetNextPhase(PhaseNames.Start, Game);
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<ChangePhaseCommand>());
        Game.ActivePlayer.ShouldBeNull();
    }

    [Fact]
    public void HandleCommand_WhenNoPlayers_ShouldStayInStartPhase()
    {
        // Act
        _sut.HandleCommand(CreateStatusCommand(Guid.NewGuid(), PlayerStatus.Ready));

        // Assert
        MockPhaseManager.DidNotReceive().GetNextPhase(PhaseNames.Start, Game);
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<ChangePhaseCommand>());
        Game.ActivePlayer.ShouldBeNull();
    }
    
    [Fact]
    public void HandleCommand_WhenRequestingLobbyStatus_ShouldSendJoinCommandsForAllPlayers()
    {
        // Arrange
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        
        // Add two players
        _sut.HandleCommand(CreateJoinCommand(player1Id, "Player 1"));
        _sut.HandleCommand(CreateJoinCommand(player2Id, "Player 2"));
        
        // Reset the command publisher to clear previous calls
        CommandPublisher.ClearReceivedCalls();
        
        // Create the request command
        var requestCommand = new RequestGameLobbyStatusCommand
        {
            GameOriginId = Game.Id,
            Timestamp = DateTime.UtcNow
        };
        
        // Act
        _sut.HandleCommand(requestCommand);
        
        // Assert
        // Verify that a JoinGameCommand was published for each player
        CommandPublisher.Received(2).PublishCommand(Arg.Is<JoinGameCommand>(cmd => 
            cmd.GameOriginId == Game.Id && 
            (cmd.PlayerId == player1Id || cmd.PlayerId == player2Id)));
        
        // Verify specific details for each player
        CommandPublisher.Received(1).PublishCommand(Arg.Is<JoinGameCommand>(cmd => 
            cmd.PlayerId == player1Id && 
            cmd.PlayerName == "Player 1" && 
            cmd.Units.Count == 1));
        
        CommandPublisher.Received(1).PublishCommand(Arg.Is<JoinGameCommand>(cmd => 
            cmd.PlayerId == player2Id && 
            cmd.PlayerName == "Player 2" && 
            cmd.Units.Count == 1));
    }
    
    [Fact]
    public void HandleCommand_WhenRequestingLobbyStatusWithNoPlayers_ShouldNotSendAnyJoinCommands()
    {
        // Arrange
        // Reset the command publisher to clear previous calls
        CommandPublisher.ClearReceivedCalls();
        
        // Create the request command
        var requestCommand = new RequestGameLobbyStatusCommand
        {
            GameOriginId = Game.Id,
            Timestamp = DateTime.UtcNow
        };
        
        // Act
        _sut.HandleCommand(requestCommand);
        
        // Assert
        // Verify that no JoinGameCommand was published since there are no players
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<JoinGameCommand>());
    }
}

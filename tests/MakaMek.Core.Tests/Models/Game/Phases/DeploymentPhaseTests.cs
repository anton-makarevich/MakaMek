using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Phases;

public class DeploymentPhaseTests : GamePhaseTestsBase
{
    private readonly DeploymentPhase _sut;
    private readonly IGamePhase _mockNextPhase ;

    public DeploymentPhaseTests()
    {
        // Create mock next phase and configure the phase manager
        _mockNextPhase = Substitute.For<IGamePhase>();
        MockPhaseManager.GetNextPhase(PhaseNames.Deployment, Game).Returns(_mockNextPhase);
        
        _sut = new DeploymentPhase(Game);
    }

    [Fact]
    public void Name_ShouldBeDeployment()
    {
        _sut.Name.ShouldBe(PhaseNames.Deployment);
    }

    [Fact]
    public void Enter_ShouldSetRandomPlayerAsActive()
    {
        // Arrange
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();

        // Add two players
        Game.HandleCommand(CreateJoinCommand(player1Id, "Player 1"));
        Game.HandleCommand(CreateJoinCommand(player2Id, "Player 2"));
        Game.HandleCommand(CreateStatusCommand(player1Id, PlayerStatus.Ready));
        Game.HandleCommand(CreateStatusCommand(player2Id, PlayerStatus.Ready));

        // Act
        _sut.Enter();

        // Assert
        Game.ActivePlayer.ShouldNotBeNull();
        var playerNames = Game.Players.Select(p => p.Name).ToArray();
        playerNames.ShouldContain(Game.ActivePlayer.Name);
    }

    [Fact]
    public void HandleCommand_WhenUnitDeployed_ShouldUpdateUnitPosition()
    {
        // Arrange
        Game.IsAutoRoll = false;
        var playerId = Guid.NewGuid();

        // Add a player with a unit
        Game.HandleCommand(CreateJoinCommand(playerId, "Player 1"));
        Game.HandleCommand(CreateStatusCommand(playerId, PlayerStatus.Ready));
        _sut.Enter();

        // Act
        var player = Game.Players[0];
        _sut.HandleCommand(CreateDeployCommand(playerId, player.Units[0].Id, 1, 1, 0));

        // Assert
        var unit = player.Units[0];
        unit.IsDeployed.ShouldBeTrue();
        unit.Position.ShouldNotBeNull();
        unit.Position.Coordinates.Q.ShouldBe(1);
        unit.Position.Coordinates.R.ShouldBe(1);
    }

    [Fact]
    public void HandleCommand_WhenAllUnitsDeployed_ShouldTransitionToNextPhase()
    {
        // Arrange
        Game.IsAutoRoll = false;
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();

        // Add two players with one unit each
        Game.HandleCommand(CreateJoinCommand(player1Id, "Player 1"));
        Game.HandleCommand(CreateJoinCommand(player2Id, "Player 2"));
        Game.HandleCommand(CreateStatusCommand(player1Id, PlayerStatus.Ready));
        Game.HandleCommand(CreateStatusCommand(player2Id, PlayerStatus.Ready));
        _sut.Enter();

        // Deploy first player's unit
        _sut.HandleCommand(CreateDeployCommand(Game.ActivePlayer!.Id, Game.ActivePlayer.Units[0].Id, 1, 1, 0));

        // Act
        // Deploy second player's unit
        _sut.HandleCommand(CreateDeployCommand(Game.ActivePlayer.Id, Game.ActivePlayer.Units[0].Id, 2, 2, 0));
        
        // Assert
        MockPhaseManager.Received(1).GetNextPhase(PhaseNames.Deployment, Game);
        _mockNextPhase.Received(1).Enter();
    }

    [Fact]
    public void HandleCommand_WhenActivePlayerHasMoreUnits_ShouldKeepSameActivePlayer()
    {
        // Arrange
       var playerId = Guid.NewGuid();
       var unit1 = MechFactoryTests.CreateDummyMechData();
       unit1.Id = Guid.NewGuid();
       var unit2 = MechFactoryTests.CreateDummyMechData();
       unit2.Id = Guid.NewGuid();

        // Add a player with two units
        var joinCommand = new JoinGameCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            PlayerName = "Player 1",
            Units = [unit1, unit2],
            Tint = "#FF0000",
            PilotAssignments = []
        };
        Game.HandleCommand(joinCommand);
        Game.HandleCommand(CreateStatusCommand(playerId, PlayerStatus.Ready));
        _sut.Enter();

        var initialActivePlayer = Game.ActivePlayer;
        
        // Clear previous command publications
        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.HandleCommand(CreateDeployCommand(playerId, unit1.Id.Value, 1, 1, 0));

        // Assert
        Game.ActivePlayer.ShouldBe(initialActivePlayer);
        MockPhaseManager.DidNotReceive().GetNextPhase(PhaseNames.Deployment, Game);
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<ChangePhaseCommand>());
    }

    [Fact]
    public void HandleCommand_WhenActivePlayerFinishesDeployment_ShouldSwitchToNextPlayer()
    {
        // Arrange
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();

        // Add two players
        Game.HandleCommand(CreateJoinCommand(player1Id, "Player 1"));
        Game.HandleCommand(CreateJoinCommand(player2Id, "Player 2"));
        Game.HandleCommand(CreateStatusCommand(player1Id, PlayerStatus.Ready));
        Game.HandleCommand(CreateStatusCommand(player2Id, PlayerStatus.Ready));
        _sut.Enter();

        var initialActivePlayer = Game.ActivePlayer;

        // Clear previous command publications
        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.HandleCommand(CreateDeployCommand(initialActivePlayer!.Id, initialActivePlayer.Units[0].Id, 1, 1, 0));

        // Assert
        Game.ActivePlayer.ShouldNotBe(initialActivePlayer);
        MockPhaseManager.DidNotReceive().GetNextPhase(PhaseNames.Deployment, Game);
    }
}

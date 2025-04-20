using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Phases;

public class MovementPhaseTests : GamePhaseTestsBase
{
    private readonly MovementPhase _sut;
    private readonly Guid _player1Id = Guid.NewGuid();
    private readonly Guid _player2Id = Guid.NewGuid();
    private readonly Guid _unit1Id;
    private readonly IGamePhase _mockNextPhase;

    public MovementPhaseTests()
    {
        // Create mock next phase and configure the phase manager
        _mockNextPhase = Substitute.For<IGamePhase>();
        MockPhaseManager.GetNextPhase(PhaseNames.Movement, Game).Returns(_mockNextPhase);
        
        _sut = new MovementPhase(Game);

        // Add two players with units
        Game.HandleCommand(CreateJoinCommand(_player1Id, "Player 1",2));
        Game.HandleCommand(CreateJoinCommand(_player2Id, "Player 2"));
        Game.HandleCommand(CreateStatusCommand(_player1Id, PlayerStatus.Ready));
        Game.HandleCommand(CreateStatusCommand(_player2Id, PlayerStatus.Ready));

        // Add units to players
        var player1 = Game.Players[0];
        _unit1Id=player1.Units[0].Id;

        var player2 = Game.Players[1];

        // Set initiative order (player2 won, player1 lost)
        Game.SetInitiativeOrder(new List<IPlayer> { player2, player1 });
    }

    [Fact]
    public void Enter_ShouldSetFirstPlayerActive()
    {
        // Act
        _sut.Enter();
    
        // Assert
        Game.ActivePlayer.ShouldBe(Game.Players[0]); // Player who lost initiative moves first
    }

    [Fact]
    public void HandleCommand_WhenValidMove_ShouldPublishAndUpdateTurn()
    {
        // Arrange
        _sut.Enter();
        var unit = Game.ActivePlayer!.Units.Single(u => u.Id == _unit1Id);
        unit.Deploy(new HexPosition(1,2,HexDirection.Top));
        
        // Act
        _sut.HandleCommand(new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = Game.ActivePlayer!.Id,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(3, 1, HexDirection.Bottom), 1)
                    .ToData()
            ]
        });
    
        // Assert
        unit.Position?.Coordinates.ToString().ShouldBe("0301");
    }

    [Fact]
    public void HandleCommand_WhenWrongPlayer_ShouldIgnoreCommand()
    {
        // Arrange
        _sut.Enter();
        var wrongPlayerId = Guid.NewGuid();
    
        // Act
        _sut.HandleCommand(new MoveUnitCommand
        {
            MovementType = MovementType.Walk,
            GameOriginId = Game.Id,
            PlayerId = wrongPlayerId,
            UnitId = _unit1Id,
            MovementPath =
            [
                new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(1, 1, HexDirection.Bottom), 1)
                    .ToData()
            ]
        });
    
        // Assert
        foreach (var unit in Game.ActivePlayer!.Units)
        {
            unit.Position.ShouldBeNull();
        }
    }
    
    [Fact]
    public void HandleCommand_WhenAllUnitsOfPlayerMoved_ShouldActivateNextPlayer()
    {
        // Arrange
        _sut.Enter();
        var player2 = Game.Players[1];
        CommandPublisher.ClearReceivedCalls();
    
        // Move all units of first player
        foreach (var unit in Game.ActivePlayer!.Units)
        {
            unit.Deploy(new HexPosition(1,2,HexDirection.Top));
            _sut.HandleCommand(new MoveUnitCommand
            {
                MovementType = MovementType.Walk,
                GameOriginId = Game.Id,
                PlayerId = Game.ActivePlayer!.Id,
                UnitId = unit.Id,
                MovementPath =
                [
                    new PathSegment(new HexPosition(1, 2, HexDirection.Top), new HexPosition(1, 1, HexDirection.Bottom),
                        1).ToData()
                ]
            });
        }

        // Assert
        CommandPublisher.Received().PublishCommand(Arg.Is<ChangeActivePlayerCommand>(cmd =>
        cmd.GameOriginId == Game.Id &&
        cmd.PlayerId == player2.Id ));
    }

    [Fact]
    public void HandleCommand_WhenAllUnitsMoved_ShouldTransitionToNextPhase()
    {
        // Arrange
        _sut.Enter();
    
        // Move all units
        foreach (var player in Game.Players)
        {
            foreach (var unit in player.Units)
            {
                unit.Deploy(new HexPosition(1,2,HexDirection.Top));
                _sut.HandleCommand(new MoveUnitCommand
                {
                    MovementType = MovementType.Walk,
                    GameOriginId = Game.Id,
                    PlayerId = Game.ActivePlayer!.Id,
                    UnitId = unit.Id,
                    MovementPath =
                    [
                        new PathSegment(new HexPosition(1, 2, HexDirection.Top),
                            new HexPosition(1, 1, HexDirection.Bottom), 1).ToData()
                    ]
                });
            }
        }
    
        // Assert
        MockPhaseManager.Received(1).GetNextPhase(PhaseNames.Movement, Game);
        _mockNextPhase.Received(1).Enter();
    }
}

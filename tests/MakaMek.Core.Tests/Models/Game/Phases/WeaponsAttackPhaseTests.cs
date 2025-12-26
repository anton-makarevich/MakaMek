using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Phases;

public class WeaponsAttackPhaseTests : GamePhaseTestsBase
{
    private readonly WeaponsAttackPhase _sut;
    private readonly Guid _player1Id = Guid.NewGuid();
    private readonly Guid _player2Id = Guid.NewGuid();
    private readonly Guid _unit1Id;
    private readonly IGamePhase _mockNextPhase;

    public WeaponsAttackPhaseTests()
    {
        // Create a mock next phase and configure the phase manager
        _mockNextPhase = Substitute.For<IGamePhase>();
        MockPhaseManager.GetNextPhase(PhaseNames.WeaponsAttack, Game).Returns(_mockNextPhase);
        
        _sut = new WeaponsAttackPhase(Game);

        // Add two players with units
        Game.HandleCommand(CreateJoinCommand(_player1Id, "Player 1", 2));
        Game.HandleCommand(CreateJoinCommand(_player2Id, "Player 2"));
        Game.HandleCommand(CreateStatusCommand(_player1Id, PlayerStatus.Ready));
        Game.HandleCommand(CreateStatusCommand(_player2Id, PlayerStatus.Ready));

        // Get unit IDs
        var player1 = Game.Players[0];
        _unit1Id = player1.Units[0].Id;

        var player2 = Game.Players[1];

        // Set initiative order (player2 won, player1 lost)
        Game.SetInitiativeOrder(new List<IPlayer> { player2, player1 });

        // Deploy units
        foreach (var unit in player1.Units.Concat(player2.Units))
        {
            unit.Deploy(new HexPosition(1, 1, HexDirection.Top));
        }
    }

    [Fact]
    public void Enter_ShouldSetFirstPlayerActive()
    {
        // Act
        _sut.Enter();
    
        // Assert
        Game.PhaseStepState?.ActivePlayer.ShouldBe(Game.Players[0]); // Player who lost initiative attacks first
    }

    [Fact]
    public void HandleCommand_WhenWeaponConfiguration_ShouldPublishCommand()
    {
        // Arrange
        _sut.Enter();
        
        // Act
        _sut.HandleCommand(new WeaponConfigurationCommand
        {
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            Configuration = new WeaponConfiguration
            {

                Type = WeaponConfigurationType.TorsoRotation,
                Value = 1
            }
        });
    
        // Assert
        CommandPublisher.Received(1).PublishCommand(Arg.Is<WeaponConfigurationCommand>(cmd => 
            cmd.UnitId == _unit1Id && 
            cmd.Configuration.Value == 1));
    }

    [Fact]
    public void HandleCommand_WhenWeaponsAttack_ShouldPublishAndUpdateTurn()
    {
        // Arrange
        _sut.Enter();
        
        // Act
        _sut.HandleCommand(new WeaponAttackDeclarationCommand
        {
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _unit1Id,
            WeaponTargets = [],
        });
    
        // Assert
        CommandPublisher.Received(1).PublishCommand(Arg.Is<WeaponAttackDeclarationCommand>(cmd => 
            cmd.UnitId == _unit1Id));
    }

    [Fact]
    public void HandleCommand_WhenWrongPlayer_ShouldIgnoreCommand()
    {
        // Arrange
        _sut.Enter();
        var wrongPlayerId = Guid.NewGuid();
    
        // Act
        _sut.HandleCommand(new WeaponAttackDeclarationCommand
        {
            GameOriginId = Game.Id,
            PlayerId = wrongPlayerId,
            UnitId = _unit1Id,
            WeaponTargets = []
        });
    
        // Assert
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<WeaponAttackDeclarationCommand>());
    }

    [Fact]
    public void HandleCommand_WhenAllUnitsAttacked_ShouldTransitionToNextPhase()
    {
        // Arrange
        _sut.Enter();
        var firstPlayer = Game.PhaseStepState!.Value.ActivePlayer;
        
        // Act - First player attacks with all units
        foreach (var unit in firstPlayer.Units)
        {
            _sut.HandleCommand(new WeaponAttackDeclarationCommand
            {
                GameOriginId = Game.Id,
                PlayerId = firstPlayer.Id,
                UnitId = unit.Id,
                WeaponTargets = []
            });
        }

        // The second player should be active now
        Game.PhaseStepState!.Value.ActivePlayer.ShouldNotBe(firstPlayer);
        var secondPlayer = Game.PhaseStepState.Value.ActivePlayer;
        
        // Second player attacks
        foreach (var unit in secondPlayer.Units)
        {
            _sut.HandleCommand(new WeaponAttackDeclarationCommand
            {
                GameOriginId = Game.Id,
                PlayerId = secondPlayer.Id,
                UnitId = unit.Id,
                WeaponTargets = []
            });
        }
    
        // Assert
        MockPhaseManager.Received(1).GetNextPhase(PhaseNames.WeaponsAttack, Game);
        _mockNextPhase.Received(1).Enter();
    }
}

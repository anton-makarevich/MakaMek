using Shouldly;
using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.UiStates;
using Sanet.MakaMek.Core.Utils.Generators;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Core.ViewModels;

namespace Sanet.MakaMek.Core.Tests.UiStates;

public class StartStateTests
{
    private readonly StartState _sut;
    private readonly ClientGame _game;
    private readonly Player _localPlayer1;
    private readonly BattleMapViewModel _battleMapViewModel;
    private readonly ICommandPublisher _commandPublisher;

    public StartStateTests()
    {
        var imageService = Substitute.For<IImageService>();
        var localizationService = Substitute.For<ILocalizationService>();

        // Mock localization service responses
        localizationService.GetString("StartPhase_ActionLabel").Returns("Ready to play");
        localizationService.GetString("StartPhase_PlayerActionLabel").Returns("Ready to play");
        
        _battleMapViewModel = new BattleMapViewModel(imageService, localizationService);
        
        var rules = new ClassicBattletechRulesProvider();
        
        _localPlayer1 = new Player(Guid.NewGuid(), "LocalPlayer1") { Status = PlayerStatus.Joining };
        var localPlayer2 = new Player(Guid.NewGuid(), "LocalPlayer2") { Status = PlayerStatus.Joining };

        _commandPublisher = Substitute.For<ICommandPublisher>();
        
        _game = new ClientGame(
           rules,
            _commandPublisher, 
            Substitute.For<IToHitCalculator>());
        _game.JoinGameWithUnits(_localPlayer1,[]);
        _game.JoinGameWithUnits(localPlayer2,[]);
        _game.SetBattleMap(BattleMap.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        
        _battleMapViewModel.Game = _game;
        
        // Set the phase to Start
        _game.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.Start
        });
        
        _sut = new StartState(_battleMapViewModel);
    }

    [Fact]
    public void InitialState_HasCorrectActionLabel()
    {
        // Assert
        _sut.ActionLabel.ShouldBe("Ready to play");
    }

    [Fact]
    public void IsActionRequired_ReturnsTrue_WhenActivePlayerIsLocal()
    {
        // Arrange - localPlayer1 should be active by default
        
        // Assert
        _sut.IsActionRequired.ShouldBeTrue();
    }
    
    [Fact]
    public void InitialState_CanExecutePlayerAction()
    {
        // Assert
        _sut.CanExecutePlayerAction.ShouldBeTrue();
    }

    [Fact]
    public void IsActionRequired_ReturnsFalse_WhenActivePlayerIsNotLocal()
    {
        // Arrange
        var remotePlayer = new Player(Guid.NewGuid(), "RemotePlayer") { Status = PlayerStatus.Joining };
        _game.HandleCommand(new JoinGameCommand
        {
            PlayerId = remotePlayer.Id,
            PlayerName = remotePlayer.Name,
            GameOriginId = Guid.NewGuid(),
            Units = [],
            Tint = "#FF0000"
        });
        
        // Set remote player as active
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = remotePlayer.Id,
            UnitsToPlay = 0
        });
        
        // Assert
        _sut.IsActionRequired.ShouldBeFalse();
    }

    [Fact]
    public void ExecutePlayerAction_ShouldPublishReadyCommand_ForActiveLocalPlayerOnly()
    {
        // Arrange
        _game.HandleCommand(new JoinGameCommand
        {
            PlayerId = _localPlayer1.Id,
            PlayerName = _localPlayer1.Name,
            GameOriginId = Guid.NewGuid(),
            Units = [],
            Tint = "#FF0000"
        });
        
        // Act
        _sut.ExecutePlayerAction();
        
        // Assert
        _commandPublisher.Received().PublishCommand(Arg.Is<UpdatePlayerStatusCommand>(cmd => 
            cmd.PlayerId == _localPlayer1.Id && 
            cmd.PlayerStatus == PlayerStatus.Playing &&
            cmd.GameOriginId == _game.Id
        ));
    }

    [Fact]
    public void ExecutePlayerAction_ShouldNotPublishCommand_WhenActivePlayerIsNotLocal()
    {
        // Arrange
        var remotePlayer = new Player(Guid.NewGuid(), "RemotePlayer") { Status = PlayerStatus.Joining };
        _game.HandleCommand(new JoinGameCommand
        {
            PlayerId = remotePlayer.Id,
            PlayerName = remotePlayer.Name,
            GameOriginId = Guid.NewGuid(),
            Units = [],
            Tint = "#FF0000"
        });
        
        // Set remote player as active
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = remotePlayer.Id,
            UnitsToPlay = 0
        });
        
        // Act
        _sut.ExecutePlayerAction();
        
        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<UpdatePlayerStatusCommand>());
    }

    [Fact]
    public void ExecutePlayerAction_ShouldNotPublishCommand_WhenGameIsNull()
    {
        // Arrange
        _battleMapViewModel.Game = null;
        
        // Act
        _sut.ExecutePlayerAction();
        
        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<UpdatePlayerStatusCommand>());
    }

    [Fact]
    public void ExecutePlayerAction_ShouldNotPublishCommand_WhenActivePlayerIsNull()
    {
        // Arrange
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = Guid.Empty, // Invalid player ID to set active player to null
            UnitsToPlay = 0
        });
        
        // Act
        _sut.ExecutePlayerAction();
        
        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<UpdatePlayerStatusCommand>());
    }

    [Fact]
    public void PlayerActionLabel_ReturnsCorrectLabel()
    {   
        // Act
        var result = _sut.PlayerActionLabel;
        
        // Assert
        result.ShouldBe("Ready to play");
    }
}

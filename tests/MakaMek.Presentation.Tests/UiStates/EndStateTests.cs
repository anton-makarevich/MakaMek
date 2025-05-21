using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Tests.Models.Map;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.Generators;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Presentation.UiStates;
using Sanet.MakaMek.Presentation.ViewModels;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.UiStates;

public class EndStateTests
{
    private readonly EndState _sut;
    private readonly ClientGame _game;
    private readonly Unit _unit1;
    private readonly Player _player;
    private readonly BattleMapViewModel _battleMapViewModel;
    private readonly ICommandPublisher _commandPublisher;

    public EndStateTests()
    {
        var imageService = Substitute.For<IImageService>();
        var localizationService = Substitute.For<ILocalizationService>();
        
        // Mock localization service responses
        localizationService.GetString("EndPhase_ActionLabel").Returns("End your turn");
        localizationService.GetString("EndPhase_PlayerActionLabel").Returns("End your turn");
        
        _battleMapViewModel = new BattleMapViewModel(imageService, localizationService,Substitute.For<IDispatcherService>());
        var playerId = Guid.NewGuid();
        
        var rules = new ClassicBattletechRulesProvider();
        var unitData = MechFactoryTests.CreateDummyMechData();
        
        _player = new Player(playerId, "Player1");
        _commandPublisher = Substitute.For<ICommandPublisher>();
        
        _game = new ClientGame(
            rules,
            new MechFactory(rules,localizationService),
            _commandPublisher, 
            Substitute.For<IToHitCalculator>(),
            Substitute.For<IBattleMapFactory>());
        _game.JoinGameWithUnits(_player,[]);
        _game.SetBattleMap(BattleMapTests.BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        
        _battleMapViewModel.Game = _game;
        
        _game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player1",
            Units = [unitData],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = _player.Id
        });
        _unit1 = _battleMapViewModel.Units.First();
    
        SetPhase(PhaseNames.End);
        _sut = new EndState(_battleMapViewModel);
    }

    [Fact]
    public void InitialState_HasEndTurnAction()
    {
        // Assert
        _sut.ActionLabel.ShouldBe("End your turn");
    }
    
    
    [Fact]
    public void InitialState_CanExecutePlayerAction()
    {
        // Assert
        _sut.CanExecutePlayerAction.ShouldBeTrue();
    }
    
    [Fact]
    public void CanExecutePlayerAction_ShouldBeFalse_WhenNotActivePlayer()
    {
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(), //doesn't exist
            UnitsToPlay = 0
        });
        // Assert
        _sut.CanExecutePlayerAction.ShouldBeFalse();
    }

    [Fact]
    public void HandleHexSelection_SelectsUnitAtHex()
    {
        // Arrange
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        _unit1.Deploy(position);
        var hex = new Hex(position.Coordinates);

        // Act
        _sut.HandleHexSelection(hex);

        // Assert
        _battleMapViewModel.SelectedUnit.ShouldBe(_unit1);
    }

    [Fact]
    public void HandleHexSelection_DeselectsUnit_WhenNoUnitAtHex()
    {
        // Arrange
        _battleMapViewModel.SelectedUnit = _unit1;
        var hex = new Hex(new HexCoordinates(2, 2));

        // Act
        _sut.HandleHexSelection(hex);

        // Assert
        _battleMapViewModel.SelectedUnit.ShouldBeNull();
    }

    [Fact]
    public void ExecutePlayerAction_SendsTurnEndedCommand_WhenActivePlayer()
    {
        // Arrange
        SetActivePlayer();
        
        // Act
        _sut.ExecutePlayerAction();

        // Assert
        _commandPublisher.Received(1).PublishCommand(Arg.Is<TurnEndedCommand>(cmd =>
            cmd.PlayerId == _player.Id &&
            cmd.GameOriginId == _game.Id));
    }

    [Fact]
    public void ExecutePlayerAction_DoesNotSendCommand_WhenNotActivePlayer()
    {
        // Arrange
        var otherPlayerId = Guid.NewGuid();
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = otherPlayerId,
            UnitsToPlay = 0
        });

        // Act
        _sut.ExecutePlayerAction();

        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<TurnEndedCommand>());
    }

    [Fact]
    public void ExecutePlayerAction_DoesNotSendCommand_WhenGameIsNull()
    {
        // Arrange
        _battleMapViewModel.Game = null;

        // Act
        _sut.ExecutePlayerAction();

        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<TurnEndedCommand>());
    }
    
    [Fact]
    public void PlayerActionLabel_ReturnsCorrectLabel()
    {   
        // Act
        var result = _sut.PlayerActionLabel;
        
        // Assert
        result.ShouldBe("End your turn");
    }
    
    private void SetActivePlayer()
    {
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = _player.Id,
            UnitsToPlay = 0
        });
    }
    
    private void SetPhase(PhaseNames phase)
    {
        _game.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = phase,
        });
    }
}

using Shouldly;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.UiStates;
using Sanet.MakaMek.Core.ViewModels;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.Tests.UiStates;

public class DeploymentStateTests
{
    private DeploymentState _sut;
    private readonly ClientGame _game;
    private readonly Unit _unit;
    private readonly Hex _hex1;
    private readonly Hex _hex2;
    private readonly BattleMapViewModel _battleMapViewModel;

    public DeploymentStateTests()
    {
        var imageService = Substitute.For<IImageService>();
        var localizationService = Substitute.For<ILocalizationService>();
        
        _battleMapViewModel = new BattleMapViewModel(imageService, localizationService);

        var rules = new ClassicBattletechRulesProvider();
        var unitData = MechFactoryTests.CreateDummyMechData();
        
        // Create two adjacent hexes
        _hex1 = new Hex(new HexCoordinates(1, 1));
        _hex2 = new Hex(new HexCoordinates(1, 2)); 
        
        var battleMap = new BattleMap(1, 1);
        var player = new Player(Guid.NewGuid(), "Player1");
        _game = new ClientGame(
            rules,
            Substitute.For<ICommandPublisher>(),
            Substitute.For<IToHitCalculator>());
        _game.JoinGameWithUnits(player,[]);
        _game.SetBattleMap(battleMap);
        
        _battleMapViewModel.Game = _game;
        SetActivePlayer(player, unitData);
        _unit = _battleMapViewModel.Units.First();
        _sut = new DeploymentState(_battleMapViewModel);

        localizationService.GetString("Action_SelectUnitToDeploy").Returns("Select Unit");
        localizationService.GetString("Action_SelectDeploymentHex").Returns("Select Hex");
        localizationService.GetString("Action_SelectFacingDirection").Returns("Select facing direction");
    }

    [Fact]
    public void InitialState_HasSelectUnitAction()
    {
        // Assert
        _sut.ActionLabel.ShouldBe("Select Unit");
        _sut.IsActionRequired.ShouldBeTrue();
    }
    
    [Fact]
    public void InitialState_CannotExecutePlayerAction()
    {
        // Assert
        ((IUiState)_sut).CanExecutePlayerAction.ShouldBeFalse();
        ((IUiState)_sut).PlayerActionLabel.ShouldBe("");
    }

    private void SetActivePlayer(Player player, UnitData unitData)
    {
        _game.HandleCommand(new JoinGameCommand
        {
            PlayerName = player.Name,
            Units = [unitData],
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            Tint = "#FF0000"
        });
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 1
        });
    }

    [Fact]
    public void HandleUnitSelection_TransitionsToHexSelection()
    {
        // Act
        _sut.HandleUnitSelection(_unit);

        // Assert
        _sut.ActionLabel.ShouldBe("Select Hex");
    }

    [Fact]
    public void HandleHexSelection_ForDeployment_UpdatesStepToSelectDirection()
    {
        // Arrange
        _sut.HandleUnitSelection(_unit);
        
        // Act
        _sut.HandleHexSelection(_hex1);

        // Assert
        _sut.ActionLabel.ShouldBe("Select facing direction");
    }
    
    [Fact]
    public void Constructor_ShouldThrow_IfGameNull()
    {
        // Arrange
        _battleMapViewModel.Game=null;
        // Act & Assert
        Should.Throw<InvalidOperationException>(() => new DeploymentState(_battleMapViewModel));
    }
    
    [Fact]
    public void Constructor_ShouldThrow_IfActivePlayerNull()
    {
        // Arrange
        _game.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.WeaponsAttack,
        });
        // Act & Assert
        Should.Throw<InvalidOperationException>(() => new DeploymentState(_battleMapViewModel));
    }

    [Fact]
    public void HandleHexSelection_WhenSelectingHex_ShowsDirectionSelector()
    {
        // Arrange
        _sut.HandleUnitSelection(_unit);
        
        // Act
        _sut.HandleHexSelection(_hex1);

        // Assert
        _battleMapViewModel.DirectionSelectorPosition.ShouldBe(_hex1.Coordinates);
        _battleMapViewModel.IsDirectionSelectorVisible.ShouldBeTrue();
        _battleMapViewModel.AvailableDirections!.ToList().Count.ShouldBe(6);
    }
    
    [Fact]
    public void HandleHexSelection_WhenSelectingHexTwice_ShouldSelectSecondHex()
    {
        // Arrange
        _sut.HandleUnitSelection(_unit);
        
        // Act
        _sut.HandleHexSelection(_hex1);
        _sut.HandleHexSelection(_hex2);

        // Assert
        _battleMapViewModel.DirectionSelectorPosition.ShouldBe(_hex2.Coordinates);
    }
    
    [Fact]
    public void HandleFacingSelection_WhenDirectionSelected_CompletesDeployment()
    {
        // Arrange
        _sut.HandleUnitSelection(_unit);
        _sut.HandleHexSelection(_hex1);
    
        // Act
        _sut.HandleFacingSelection(HexDirection.Top);
    
        // Assert
        _sut.ActionLabel.ShouldBe("");
        _battleMapViewModel.IsDirectionSelectorVisible.ShouldBeFalse();
    }

    [Fact]
    public void HandleFacingSelection_AfterSelection_HidesDirectionSelector()
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(1, 1));
        _sut.HandleHexSelection(hex);

        // Act
        _sut.HandleFacingSelection(HexDirection.Top);

        // Assert
        _battleMapViewModel.IsDirectionSelectorVisible.ShouldBeFalse();
    }

    [Fact]
    public void HandleHexSelection_WhenHexIsOccupied_ShouldNotShowDirectionSelector()
    {
        // Arrange
        _sut.HandleUnitSelection(_unit);
        // Deploy first unit
        _sut.HandleHexSelection(_hex1);
        _sut.HandleFacingSelection(HexDirection.Top);
        _unit.Deploy(new HexPosition(_hex1.Coordinates,HexDirection.Top));
        
        // Try to deploy second unit to the same hex
        var secondUnit = new MechFactory(new ClassicBattletechRulesProvider()).Create(MechFactoryTests.CreateDummyMechData());
        _sut = new DeploymentState(_battleMapViewModel);
        _sut.HandleUnitSelection(secondUnit);
        
        // Act
        _sut.HandleHexSelection(_hex1);

        // Assert
        _battleMapViewModel.IsDirectionSelectorVisible.ShouldBeFalse();
    }
}

using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Events;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Tests.Models.Map;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.Generators;
using Sanet.MakaMek.Presentation.UiStates;
using Sanet.MakaMek.Presentation.ViewModels;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class BattleMapViewModelTests
{
    private readonly BattleMapViewModel _sut;
    private ClientGame _game;
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IBattleMapFactory _mapFactory = Substitute.For<IBattleMapFactory>();

    private readonly IMechFactory _mechFactory;

    public BattleMapViewModelTests()
    {
        var imageService = Substitute.For<IImageService>();
        var dispatcherService = Substitute.For<IDispatcherService>();
        _sut = new BattleMapViewModel(imageService, _localizationService,dispatcherService);
        
        // Configure the dispatcher to execute actions immediately
        dispatcherService.RunOnUIThread(Arg.InvokeDelegate<Action>());
        var rules = new ClassicBattletechRulesProvider();
        
        _localizationService.GetString("Action_SelectTarget").Returns("Select Target");
        _localizationService.GetString("Action_SelectUnitToFire").Returns("Select unit to fire weapons");
        _localizationService.GetString("Action_SelectUnitToMove").Returns("Select unit to move");
        _localizationService.GetString("Action_SelectUnitToDeploy").Returns("Select Unit");
        _localizationService.GetString("EndPhase_PlayerActionLabel").Returns("End turn");
        _localizationService.GetString("Action_MovementPoints").Returns("{0} | MP: {1}");
        _localizationService.GetString("MovementType_Walk").Returns("Walk");
        _localizationService.GetString("MovementType_Run").Returns("Run");
        _mechFactory = new MechFactory(rules, _localizationService);
        _game = CreateClientGame();
        _sut.Game = _game;
    }

    [Fact]
    public void GameUpdates_RaiseNotifyPropertyChanged()
    {

        // Act and Assert
        _game.HandleCommand(new TurnIncrementedCommand
        {
            GameOriginId = Guid.NewGuid(),
            TurnNumber = 1
        });
        _sut.Turn.ShouldBe(1);
        _game.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.Deployment
        });
        _sut.TurnPhaseName.ShouldBe(PhaseNames.Deployment);
        var player = new Player(Guid.NewGuid(), "Player1", "#FF0000");
        _game.JoinGameWithUnits(player, [],[]);
        _game.HandleCommand(new JoinGameCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            PlayerName = player.Name,
            Units = [],
            Tint = player.Tint,
            PilotAssignments = []
        });

        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 0
        });
        _sut.ActivePlayerName.ShouldBe("Player1");
        _sut.ActivePlayerTint.ShouldBe("#FF0000");
    }

    [Theory]
    [InlineData(1, "Select Unit",true)]
    [InlineData(0, "", false)]
    public void UnitsToDeploy_ShouldBeVisible_WhenItsPlayersTurnToDeploy_AndThereAreUnitsToDeploy(
        int unitsToMove, 
        string actionLabel,
        bool expectedVisible)
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var player = new Player(playerId, "Player1");
        var unitData = MechFactoryTests.CreateDummyMechData();

        _game = CreateClientGame();
        _game.JoinGameWithUnits(player,[unitData],[]);
        _game.SetBattleMap(BattleMapTests.BattleMapFactory.GenerateMap(2, 2,
                                                         new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        _sut.Game = _game;

        _game.HandleCommand(new ChangePhaseCommand()
        {
            Phase = PhaseNames.Deployment,
            GameOriginId = Guid.NewGuid()
        });
        _game.HandleCommand(new JoinGameCommand()
        {
            PlayerId = player.Id,
            Units = [unitData],
            PlayerName = player.Name,
            GameOriginId = Guid.NewGuid(),
            Tint = "#FF0000",
            PilotAssignments = []
        });

        // Act
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            UnitsToPlay = unitsToMove
        });
        
        // Assert
        if (expectedVisible)
        {
            _sut.UnitsToDeploy.ShouldHaveSingleItem();
        }
        else
        {
            _sut.UnitsToDeploy.ShouldBeEmpty();
        }
        _sut.AreUnitsToDeployVisible.ShouldBe(expectedVisible);
        _sut.ActionInfoLabel.ShouldBe(actionLabel);
        _sut.IsUserActionLabelVisible.ShouldBe(expectedVisible);
    }
    
    [Fact]
    public void Units_ReturnsAllUnitsFromPlayers()
    {
        // Arrange
        var player1 = new Player(Guid.NewGuid(), "Player1");
        var player2 = new Player(Guid.NewGuid(), "Player2");

        var mechData = MechFactoryTests.CreateDummyMechData();
    
        _game.JoinGameWithUnits(player1, [mechData],[]);
        _game.HandleCommand(new JoinGameCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player1.Id,
            PlayerName = player1.Name,
            Units = [mechData],
            Tint = player1.Tint,
            PilotAssignments = []
        });
        _game.JoinGameWithUnits(player2, [mechData,mechData],[]);
        _game.HandleCommand(new JoinGameCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player2.Id,
            PlayerName = player2.Name,
            Units = [mechData,mechData],
            Tint = player2.Tint,
            PilotAssignments = []
        });

        // Act
        var units = _sut.Units.ToList();

        // Assert
        units.Count.ShouldBe(3);
    }

    [Fact]
    public void CommandLog_ShouldBeEmpty_WhenGameIsNotClientGame()
    {
        // Assert
        _sut.CommandLog.ShouldBeEmpty();
    }

    [Fact]
    public void CommandLog_ShouldUpdateWithNewCommands_WhenClientGameReceivesCommands()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var player = new Player(playerId, "Player1");
        var clientGame = CreateClientGame();
        clientGame.JoinGameWithUnits(player, [],[]);
        clientGame.SetBattleMap(BattleMapTests.BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())) );
        _sut.Game = clientGame;

        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player2",
            GameOriginId = Guid.NewGuid(),
            Units = [],
            Tint = "#FF0000",
            PilotAssignments = []
        };

        // Act
        clientGame.HandleCommand(joinCommand);

        // Assert
        _sut.CommandLog.Count.ShouldBe(1);
        _sut.CommandLog.First().ShouldBeEquivalentTo(joinCommand.Render(_localizationService, clientGame));
    }

    [Fact]
    public void CommandLog_ShouldPreserveCommandOrder_WhenMultipleCommandsReceived()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var player = new Player(playerId, "Player1");
        var clientGame = CreateClientGame();
        clientGame.JoinGameWithUnits(player,[],[]);
        clientGame.SetBattleMap(BattleMapTests.BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        _sut.Game = clientGame;

        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player2",
            GameOriginId = Guid.NewGuid(),
            Units = [],
            Tint = "#FF0000",
            PilotAssignments = []
        };

        var phaseCommand = new ChangePhaseCommand
        {
            Phase = PhaseNames.Deployment,
            GameOriginId = Guid.NewGuid()
        };

        // Act
        clientGame.HandleCommand(joinCommand);
        clientGame.HandleCommand(phaseCommand);

        // Assert
        _sut.CommandLog.Count.ShouldBe(2);
        _sut.CommandLog.First().ShouldBeEquivalentTo(joinCommand.Render(_localizationService,clientGame));
        _sut.CommandLog.Last().ShouldBeEquivalentTo(phaseCommand.Render(_localizationService,clientGame));
    }

    [Fact]
    public void IsCommandLogExpanded_ShouldBeFalse_ByDefault()
    {
        // Assert
        _sut.IsCommandLogExpanded.ShouldBeFalse();
    }

    [Fact]
    public void ToggleCommandLog_ShouldToggleIsCommandLogExpanded()
    {
        // Arrange
        var propertyChangedEvents = new List<string>();
        _sut.PropertyChanged += (_, e) => propertyChangedEvents.Add(e.PropertyName ?? string.Empty);

        // Act & Assert - First toggle
        _sut.ToggleCommandLog();
        _sut.IsCommandLogExpanded.ShouldBeTrue();
        propertyChangedEvents.ShouldContain(nameof(BattleMapViewModel.IsCommandLogExpanded));

        // Clear events for the second test
        propertyChangedEvents.Clear();

        // Act & Assert - Second toggle
        _sut.ToggleCommandLog();
        _sut.IsCommandLogExpanded.ShouldBeFalse();
        propertyChangedEvents.ShouldContain(nameof(BattleMapViewModel.IsCommandLogExpanded));
    }

    [Fact]
    public void MovementPhase_WithActivePlayer_ShouldShowCorrectActionLabel()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1");
        _game = CreateClientGame();
        _game.JoinGameWithUnits(player,[],[]);
        _game.SetBattleMap(BattleMapTests.BattleMapFactory.GenerateMap(2, 2,
                                                         new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        _sut.Game = _game;

        _game.HandleCommand(new ChangePhaseCommand()
        {
            Phase = PhaseNames.Movement,
            GameOriginId = Guid.NewGuid()
        });
        _game.HandleCommand(new JoinGameCommand()
        {
            PlayerId = player.Id,
            Units = [MechFactoryTests.CreateDummyMechData()],
            PlayerName = player.Name,
            GameOriginId = Guid.NewGuid(),
            Tint = "#FF0000",
            PilotAssignments = []
        });
        
        // Act
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            UnitsToPlay = 1
        });
        
        // Assert
        _sut.ActionInfoLabel.ShouldBe("Select unit to move");
        _sut.IsUserActionLabelVisible.ShouldBeTrue();
    }

    [Fact]
    public void WeaponsAttackPhase_WithUnitsToPlay_ShouldShowCorrectActionLabel()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1");
        _game = CreateClientGame();
        _game.JoinGameWithUnits(player,[],[]);
        _game.SetBattleMap(BattleMapTests.BattleMapFactory.GenerateMap(2, 2,
            new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        _sut.Game = _game;
        _game.HandleCommand(new JoinGameCommand()
        {
            PlayerId = player.Id,
            Units = [MechFactoryTests.CreateDummyMechData()],
            PlayerName = player.Name,
            GameOriginId = Guid.NewGuid(),
            Tint = "#FF0000",
            PilotAssignments = []
        });
        _game.HandleCommand(new ChangePhaseCommand()
        {
            Phase = PhaseNames.WeaponsAttack,
            GameOriginId = Guid.NewGuid()
        });
        
        
        // Act
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            UnitsToPlay = 1
        });
        
        // Assert
        _sut.ActionInfoLabel.ShouldBe("Select unit to fire weapons");
        _sut.IsUserActionLabelVisible.ShouldBeTrue();
    }

    [Fact]
    public void WeaponsAttackPhase_WithNoUnitsToPlay_ShouldBeIdle()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1");
        _game = CreateClientGame();
        _game.JoinGameWithUnits(player,[],[]);
        _game.SetBattleMap(BattleMapTests.BattleMapFactory.GenerateMap(2, 2,
            new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        _sut.Game = _game;

        _game.HandleCommand(new ChangePhaseCommand()
        {
            Phase = PhaseNames.WeaponsAttack,
            GameOriginId = Guid.NewGuid()
        });
        _game.HandleCommand(new JoinGameCommand()
        {
            PlayerId = player.Id,
            Units = [],
            PlayerName = player.Name,
            GameOriginId = Guid.NewGuid(),
            Tint = "#FF0000",
            PilotAssignments = []
        });
        
        // Act
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            UnitsToPlay = 0
        });
        
        // Assert
        _sut.ActionInfoLabel.ShouldBe("Wait");
        _sut.IsUserActionLabelVisible.ShouldBeFalse();
    }

    [Fact]
    public void ShowDirectionSelector_SetsPositionAndDirections()
    {
        // Arrange
        var position = new HexCoordinates(1, 1);
        var directions = new[] { HexDirection.Top, HexDirection.Bottom };

        // Act
        _sut.ShowDirectionSelector(position, directions);

        // Assert
        _sut.DirectionSelectorPosition.ShouldBe(position);
        _sut.IsDirectionSelectorVisible.ShouldBeTrue();
        _sut.AvailableDirections.ShouldBeEquivalentTo(directions);
    }

    [Fact]
    public void HideDirectionSelector_ClearsDirectionsAndVisibility()
    {
        // Arrange
        var position = new HexCoordinates(1, 1);
        var directions = new[] { HexDirection.Top, HexDirection.Bottom };
        _sut.ShowDirectionSelector(position, directions);

        // Act
        _sut.HideDirectionSelector();

        // Assert
        _sut.IsDirectionSelectorVisible.ShouldBeFalse();
        _sut.AvailableDirections.ShouldBeNull();
    }

    [Fact]
    public void IsRecordSheetButtonVisible_NoSelectedUnit_ReturnsFalse()
    {
        // Arrange
        _sut.SelectedUnit = null;
        _sut.IsRecordSheetExpanded = false;

        // Act & Assert
        _sut.IsRecordSheetButtonVisible.ShouldBeFalse();
    }
    
    [Fact]
    public void IsRecordSheetPanelVisible_NoSelectedUnit_ReturnsFalse()
    {
        // Arrange
        _sut.SelectedUnit = null;
        _sut.IsRecordSheetExpanded = false;

        // Act & Assert
        _sut.IsRecordSheetPanelVisible.ShouldBeFalse();
    }

    [Fact]
    public void IsRecordSheetButtonVisible_HasSelectedUnitButExpanded_ReturnsFalse()
    {
        // Arrange
        var unit = new Mech("Mech", "MK1",20,6,[]);
        _sut.SelectedUnit = unit;
        _sut.IsRecordSheetExpanded = true;

        // Act & Assert
        _sut.IsRecordSheetButtonVisible.ShouldBeFalse();
    }

    [Fact]
    public void IsRecordSheetButtonVisible_HasSelectedUnitNotExpanded_ReturnsTrue()
    {
        // Arrange
        var unit = new Mech("Mech", "MK1",20,6,[]);
        _sut.SelectedUnit = unit;
        _sut.IsRecordSheetExpanded = false;

        // Act & Assert
        _sut.IsRecordSheetButtonVisible.ShouldBeTrue();
    }
    
    [Fact]
    public void IsRecordSheetPanelVisible_HasSelectedUnitButExpanded_ReturnsTrue()
    {
        // Arrange
        var unit = new Mech("Mech", "MK1",20,6,[]);
        _sut.SelectedUnit = unit;
        _sut.IsRecordSheetExpanded = true;

        // Act & Assert
        _sut.IsRecordSheetPanelVisible.ShouldBeTrue();
    }

    [Fact]
    public void IsRecordSheetPanelVisible_HasSelectedUnitNotExpanded_ReturnsFalse()
    {
        // Arrange
        var unit = new Mech("Mech", "MK1",20,6,[]);
        _sut.SelectedUnit = unit;
        _sut.IsRecordSheetExpanded = false;

        // Act & Assert
        _sut.IsRecordSheetPanelVisible.ShouldBeFalse();
    }

    [Fact]
    public void ToggleRecordSheet_TogglesIsRecordSheetExpanded()
    {
        // Arrange
        _sut.IsRecordSheetExpanded = false;

        // Act
        _sut.ToggleRecordSheet();

        // Assert
        _sut.IsRecordSheetExpanded.ShouldBeTrue();

        // Act again
        _sut.ToggleRecordSheet();

        // Assert
        _sut.IsRecordSheetExpanded.ShouldBeFalse();
    }

    [Fact]
    public void ShowMovementPath_SetsMovementPathProperty()
    {
        // Arrange
        var path = new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom),
                1)
        };

        // Act
        _sut.ShowMovementPath(path);

        // Assert
        _sut.MovementPath.ShouldNotBeNull();
        _sut.MovementPath[0].From.ShouldBe(path[0].From);
        _sut.MovementPath[0].To.ShouldBe(path[0].To);
    }

    [Fact]
    public void ShowMovementPath_WithEmptyPath_ClearsMovementPath()
    {
        // Arrange
        var path = new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom),
                1)
        };
        _sut.ShowMovementPath(path);

        // Act
        _sut.ShowMovementPath([]);

        // Assert
        _sut.MovementPath.ShouldBeNull();
    }

    [Fact]
    public void ShowMovementPath_NotifiesPropertyChanged()
    {
        // Arrange
        var path = new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom),
                1)
        };
        var propertyChanged = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BattleMapViewModel.MovementPath))
                propertyChanged = true;
        };

        // Act
        _sut.ShowMovementPath(path);

        // Assert
        propertyChanged.ShouldBeTrue();
    }

    [Fact]
    public void HideMovementPath_ClearsPathAndNotifiesChange()
    {
        // Arrange
        var path = new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom),
                1)
        };
        _sut.ShowMovementPath(path);
        var propertyChanged = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BattleMapViewModel.MovementPath))
                propertyChanged = true;
        };

        // Act
        _sut.HideMovementPath();

        // Assert
        _sut.MovementPath.ShouldBeNull();
        propertyChanged.ShouldBeTrue();
    }

    [Fact]
    public void WeaponSelectionItems_WhenNotInWeaponsAttackState_ReturnsEmptyList()
    {
        // Act
        var items = _sut.WeaponSelectionItems;

        // Assert
        items.ShouldBeEmpty();
    }

    [Fact]
    public void WeaponSelectionItems_WhenInWeaponsAttackState_ReturnsWeaponsFromState()
    {
        // Arrange
        var mechData = MechFactoryTests.CreateDummyMechData();
        var battleMap = BattleMapTests.BattleMapFactory.GenerateMap(
            2, 11,
            new SingleTerrainGenerator(2, 11, new ClearTerrain()));
        var player = new Player(Guid.NewGuid(), "Player1");
        var game = CreateClientGame();
        game.JoinGameWithUnits(player,[],[]);
        game.SetBattleMap(battleMap);
        _sut.Game = game;
        game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player1",
            Units = [mechData],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            PilotAssignments = []
        });
        game.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerStatus = PlayerStatus.Ready,
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id
        });
        game.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.WeaponsAttack
        });
        game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 1
        });


        // Place unit
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var unit = _sut.Units.First();
        unit.Deploy(position);
        
        // Select unit
        _sut.HandleHexSelection(game.BattleMap!.GetHexes().First(h=>h.Coordinates==position.Coordinates));

        // Act
        var items = _sut.WeaponSelectionItems.ToList();

        // Assert
        items.ShouldNotBeEmpty();
        items.Count.ShouldBe(unit.Parts.Sum(p => p.GetComponents<Weapon>().Count()));
    }

    [Fact]
    public void IsWeaponSelectionVisible_WhenNotInWeaponsAttackState_ReturnsFalse()
    {
        // Act & Assert
        _sut.IsWeaponSelectionVisible.ShouldBeFalse();
    }

    [Fact]
    public void IsWeaponSelectionVisible_WhenInWeaponsAttackStateWithoutTarget_ReturnsFalse()
    {
        // Arrange
        var mechData = MechFactoryTests.CreateDummyMechData();
        var battleMap = BattleMapTests.BattleMapFactory.GenerateMap(
            2, 11,
            new SingleTerrainGenerator(2, 11, new ClearTerrain()));
        var player = new Player(Guid.NewGuid(), "Player1");
        var game = CreateClientGame();
        game.JoinGameWithUnits(player,[],[]);
        game.SetBattleMap(battleMap);
        _sut.Game = game;
        game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player1",
            Units = [mechData],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            PilotAssignments = []
        });
        game.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerStatus = PlayerStatus.Ready,
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id
        });
        game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 1
        });
        game.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.WeaponsAttack
        });

        // Place unit
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var unit = _sut.Units.First();
        unit.Deploy(position);
        
        // Select unit
        _sut.HandleHexSelection(game.BattleMap!.GetHexes().First(h=>h.Coordinates==position.Coordinates));
        
        // Act & Assert
        _sut.IsWeaponSelectionVisible.ShouldBeFalse();
    }

    [Fact]
    public void IsWeaponSelectionVisible_WhenInWeaponsAttackStateWithTarget_ReturnsTrue()
    {
        // Arrange
        var mechData = MechFactoryTests.CreateDummyMechData();
        var battleMap = BattleMapTests.BattleMapFactory.GenerateMap(
            2, 11,
            new SingleTerrainGenerator(2, 11, new ClearTerrain()));
        var player1 = new Player(Guid.NewGuid(), "Player1");
        var player2 = new Player(Guid.NewGuid(), "Player2");
        var game = CreateClientGame();
        game.JoinGameWithUnits(player1,[],[]);
        game.JoinGameWithUnits(player2,[],[]);
        game.SetBattleMap(battleMap);
        
        _sut.Game = game;
        game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player1",
            Units = [mechData],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = player1.Id,
            PilotAssignments = []
        });
        game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player2",
            Units = [mechData],
            Tint = "#00FF00",
            GameOriginId = Guid.NewGuid(),
            PlayerId = player2.Id,
            PilotAssignments = []
        });
        game.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerStatus = PlayerStatus.Ready,
            GameOriginId = Guid.NewGuid(),
            PlayerId = player1.Id
        });
        game.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerStatus = PlayerStatus.Ready,
            GameOriginId = Guid.NewGuid(),
            PlayerId = player2.Id
        });
        game.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.WeaponsAttack
        });
        game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player1.Id,
            UnitsToPlay = 1
        });


        // Place units
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var attacker = _sut.Units.First(u => u.Owner!.Id == player1.Id);
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        attacker.AssignPilot(pilot);
        attacker.Deploy(attackerPosition);
        
        var targetPosition = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        var target = _sut.Units.First(u => u.Owner!.Id == player2.Id);
        target.Deploy(targetPosition);
        
        // Select attacker
        _sut.HandleHexSelection(game.BattleMap!.GetHexes().First(h=>h.Coordinates==attackerPosition.Coordinates));
        
        // Select target
        var selectTargetAction = _sut.CurrentState.GetAvailableActions().First(a => a.Label == "Select Target");
        selectTargetAction.OnExecute();
        _sut.HandleHexSelection(game.BattleMap.GetHexes().First(h=>h.Coordinates==targetPosition.Coordinates));
        
        // Act & Assert
        _sut.IsWeaponSelectionVisible.ShouldBeTrue();
    }

    [Fact]
    public void IsWeaponSelectionVisible_CanBeClosedAndReopened()
    {
        // Arrange
        var mechData = MechFactoryTests.CreateDummyMechData();
        var battleMap = BattleMapTests.BattleMapFactory.GenerateMap(
            2, 11,
            new SingleTerrainGenerator(2, 11, new ClearTerrain()));
        var player1 = new Player(Guid.NewGuid(), "Player1");
        var player2 = new Player(Guid.NewGuid(), "Player2");
        var game = CreateClientGame();
        game.JoinGameWithUnits(player1,[],[]);
        game.JoinGameWithUnits(player2,[],[]);
        game.SetBattleMap(battleMap);
        
        _sut.Game = game;
        game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player1",
            Units = [mechData],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = player1.Id,
            PilotAssignments = []
        });
        game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player2",
            Units = [mechData],
            Tint = "#00FF00",
            GameOriginId = Guid.NewGuid(),
            PlayerId = player2.Id,
            PilotAssignments = []
        });
        game.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerStatus = PlayerStatus.Ready,
            GameOriginId = Guid.NewGuid(),
            PlayerId = player1.Id
        });
        game.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerStatus = PlayerStatus.Ready,
            GameOriginId = Guid.NewGuid(),
            PlayerId = player2.Id
        });
        game.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.WeaponsAttack
        });
        game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player1.Id,
            UnitsToPlay = 1
        });


        // Place units
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var attacker = _sut.Units.First(u => u.Owner!.Id == player1.Id);
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        attacker.AssignPilot(pilot);
        attacker.Deploy(attackerPosition);
        
        var targetPosition = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        var target = _sut.Units.First(u => u.Owner!.Id == player2.Id);
        target.Deploy(targetPosition);
        
        // Select attacker
        _sut.HandleHexSelection(game.BattleMap!.GetHexes().First(h=>h.Coordinates==attackerPosition.Coordinates));
        
        // Select target
        var selectTargetAction = _sut.CurrentState.GetAvailableActions().First(a => a.Label == "Select Target");
        selectTargetAction.OnExecute();
        _sut.HandleHexSelection(game.BattleMap.GetHexes().First(h=>h.Coordinates==targetPosition.Coordinates));
        
        // Act & Assert - Initially visible
        _sut.IsWeaponSelectionVisible.ShouldBeTrue();

        // Act & Assert - Can be closed
        _sut.CloseWeaponSelectionCommand();
        _sut.IsWeaponSelectionVisible.ShouldBeFalse();

        // Act & Assert - Can be reopened
        _sut.HandleHexSelection(game.BattleMap.GetHexes().First(h=>h.Coordinates==targetPosition.Coordinates));
        _sut.IsWeaponSelectionVisible.ShouldBeTrue();

        // Act & Assert - Stays closed when changing phase
        _sut.IsWeaponSelectionVisible = false;
        game.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.Movement
        });
        _sut.IsWeaponSelectionVisible.ShouldBeFalse();
    }

    [Fact]
    public void Attacker_ShouldBeStateAttacker_DuringWeaponsAttack()
    {
        // Arrange
        var mechData = MechFactoryTests.CreateDummyMechData();
        var battleMap = BattleMapTests.BattleMapFactory.GenerateMap(
            2, 11,
            new SingleTerrainGenerator(2, 11, new ClearTerrain()));
        var player1 = new Player(Guid.NewGuid(), "Player1");
        var game = CreateClientGame();
        game.JoinGameWithUnits(player1,[],[]);
        game.SetBattleMap(battleMap);

        _sut.Game = game;
        game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player1",
            Units = [mechData],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = player1.Id,
            PilotAssignments = []
        });
        game.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerStatus = PlayerStatus.Ready,
            GameOriginId = Guid.NewGuid(),
            PlayerId = player1.Id
        });
        game.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.WeaponsAttack
        });
        game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player1.Id,
            UnitsToPlay = 1
        });

        // Place units
        var attackerPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var attacker = _sut.Units.First(u => u.Owner!.Id == player1.Id);
        attacker.Deploy(attackerPosition);

        // Act Select attacker
        _sut.HandleHexSelection(game.BattleMap!.GetHexes()
            .First(h => h.Coordinates == attackerPosition.Coordinates));
        
        // Assert
        _sut.Attacker.ShouldBe(attacker);
    }
    
    [Fact]
    public void Attacker_ShouldBeNull_WhenNotInWeaponsAttack()
    {
        // Arrange
        var mechData = MechFactoryTests.CreateDummyMechData();
        var battleMap = BattleMapTests.BattleMapFactory.GenerateMap(
            2, 11,
            new SingleTerrainGenerator(2, 11, new ClearTerrain()));
        var player1 = new Player(Guid.NewGuid(), "Player1");
        var game = CreateClientGame();
        game.JoinGameWithUnits(player1, [],[]);
        game.SetBattleMap(battleMap);

        _sut.Game = game;
        game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player1",
            Units = [mechData],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = player1.Id,
            PilotAssignments = []
        });
        game.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerStatus = PlayerStatus.Ready,
            GameOriginId = Guid.NewGuid(),
            PlayerId = player1.Id
        });
        
        // Assert
        _sut.Attacker.ShouldBeNull();
    }

    [Fact]
    public void WeaponAttacks_ShouldBePopulated_WhenWeaponAttackDeclarationCommandReceived()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var player = new Player(playerId, "Player1");
        var targetPlayerId = Guid.NewGuid();
        var targetPlayer = new Player(targetPlayerId, "Player2");
        
        var mechData = MechFactoryTests.CreateDummyMechData();
        mechData.Id = Guid.NewGuid();
        // Create a game with the players
        var game = CreateClientGame();
        game.JoinGameWithUnits(player, [],[]);
        game.JoinGameWithUnits(targetPlayer, [],[]);
        game.SetBattleMap(BattleMapTests.BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new ClearTerrain())));
        
        _sut.Game = game;
        game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player1",
            Units = [mechData],
            Tint = "#ffffff",
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            PilotAssignments = []
        });
        mechData.Id = Guid.NewGuid();
        game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player2",
            Units = [mechData],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = targetPlayerId,
            PilotAssignments = []
        });
        game.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerStatus = PlayerStatus.Ready,
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId
        });
        game.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerStatus = PlayerStatus.Ready,
            GameOriginId = Guid.NewGuid(),
            PlayerId = targetPlayerId
        });
        
        // Deploy units to positions
        var attackerPosition = new HexPosition(new HexCoordinates(2, 2), HexDirection.Top);
        var targetPosition = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var attacker = _sut.Units.First(u => u.Owner!.Id == playerId);
        var target = _sut.Units.First(u => u.Owner!.Id == targetPlayerId);
        attacker.Deploy(attackerPosition);
        target.Deploy(targetPosition);
        
        // Get a weapon from the attacker to use in the command
        var weapon = attacker.Parts.SelectMany(p => p.GetComponents<Weapon>()).First();
        
        // Create weapon target data
        var weaponTargetData = new WeaponTargetData
        {
            TargetId = target.Id,
            Weapon = new WeaponData
            {
                Location = weapon.MountedOn!.Location,
                Slots = weapon.MountedAtSlots,
                Name = weapon.Name
            },
            IsPrimaryTarget = true
        };
        
        // Create the weapon attack declaration command
        var weaponAttackCommand = new WeaponAttackDeclarationCommand
        {
            PlayerId = playerId,
            AttackerId = attacker.Id,
            WeaponTargets = [weaponTargetData],
            GameOriginId = Guid.NewGuid()
        };
        
        // Act
        game.HandleCommand(weaponAttackCommand);
        
        // Assert
        _sut.WeaponAttacks.ShouldNotBeNull();
        _sut.WeaponAttacks.Count.ShouldBe(1);
        
        var attack = _sut.WeaponAttacks.First();
        attack.From.ShouldBe(attackerPosition.Coordinates);
        attack.To.ShouldBe(targetPosition.Coordinates);
        attack.Weapon.ShouldBe(weapon);
        attack.AttackerTint.ShouldBe(player.Tint);
        attack.LineOffset.ShouldBe(5);
    }
    
    [Fact]
    public void WeaponAttacks_ShouldAccumulate_WhenMultipleWeaponAttackDeclarationCommandsReceived()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var activePlayer = new Player(playerId, "Player1");
        var targetPlayerId = Guid.NewGuid();
        var targetPlayer = new Player(targetPlayerId, "Player2");
        
        var mechData = MechFactoryTests.CreateDummyMechData();
        mechData.Id = Guid.NewGuid();
        
        // Create a game with the players
        var game = CreateClientGame();
        game.JoinGameWithUnits(activePlayer, [],[]);
        game.JoinGameWithUnits(targetPlayer, [],[]);
        game.SetBattleMap(BattleMapTests.BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new ClearTerrain())));
        _sut.Game = game;
        
        // Create a second attacker for the same player
        var mechData2 = MechFactoryTests.CreateDummyMechData();
        mechData2.Id = Guid.NewGuid();
        // Add units to the game via commands
        game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player1",
            Units = [mechData,mechData2],
            Tint = "#ffffff",
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            PilotAssignments = []
        });
        
        // Create a target unit
        var targetMechData = MechFactoryTests.CreateDummyMechData();
        targetMechData.Id = Guid.NewGuid();
        game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player2",
            Units = [targetMechData],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = targetPlayerId,
            PilotAssignments = []
        });
        
        // Set player statuses
        game.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerStatus = PlayerStatus.Ready,
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId
        });
        game.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerStatus = PlayerStatus.Ready,
            GameOriginId = Guid.NewGuid(),
            PlayerId = targetPlayerId
        });
        
        // Deploy units to positions
        var attacker1Position = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        var attacker2Position = new HexPosition(new HexCoordinates(0, 1), HexDirection.Top);
        var targetPosition = new HexPosition(new HexCoordinates(1, 0), HexDirection.Top);
        
        // Get the units from the game
        var attackers = _sut.Units.Where(u => u.Owner!.Id == playerId).ToList();
        var attacker1 = attackers[0];
        var attacker2 = attackers[1];
        var target = _sut.Units.First(u => u.Owner!.Id == targetPlayerId);
        
        // Deploy the units
        attacker1.Deploy(attacker1Position);
        attacker2.Deploy(attacker2Position);
        target.Deploy(targetPosition);
        
        // Get weapons from the attackers
        var weapon1 = attacker1.Parts.SelectMany(p => p.GetComponents<Weapon>()).First();
        var weapon2 = attacker2.Parts.SelectMany(p => p.GetComponents<Weapon>()).First();
        
        // Create weapon target data
        var weaponTargetData1 = new WeaponTargetData
        {
            TargetId = target.Id,
            Weapon = new WeaponData
            {
                Location = weapon1.MountedOn!.Location,
                Slots = weapon1.MountedAtSlots,
                Name = weapon1.Name
            },
            IsPrimaryTarget = true
        };
        
        var weaponTargetData2 = new WeaponTargetData
        {
            TargetId = target.Id,
            Weapon = new WeaponData
            {
                Location = weapon2.MountedOn!.Location,
                Slots = weapon2.MountedAtSlots,
                Name = weapon2.Name
            },
            IsPrimaryTarget = true
        };
        
        // Create the weapon attack declaration commands
        var weaponAttackCommand1 = new WeaponAttackDeclarationCommand
        {
            PlayerId = playerId,
            AttackerId = attacker1.Id,
            WeaponTargets = [weaponTargetData1],
            GameOriginId = Guid.NewGuid()
        };
        
        var weaponAttackCommand2 = new WeaponAttackDeclarationCommand
        {
            PlayerId = playerId,
            AttackerId = attacker2.Id,
            WeaponTargets = [weaponTargetData2],
            GameOriginId = Guid.NewGuid()
        };
        
        // Act
        game.HandleCommand(weaponAttackCommand1);
        game.HandleCommand(weaponAttackCommand2);
        
        // Assert
        _sut.WeaponAttacks.ShouldNotBeNull();
        _sut.WeaponAttacks.Count.ShouldBe(2);
        
        var attack1 = _sut.WeaponAttacks[0];
        attack1.From.ShouldBe(attacker1Position.Coordinates);
        attack1.To.ShouldBe(targetPosition.Coordinates);
        attack1.Weapon.ShouldBe(weapon1);
        attack1.AttackerTint.ShouldBe(activePlayer.Tint);
        attack1.LineOffset.ShouldBe(5);
        
        var attack2 = _sut.WeaponAttacks[1];
        attack2.From.ShouldBe(attacker2Position.Coordinates);
        attack2.To.ShouldBe(targetPosition.Coordinates);
        attack2.Weapon.ShouldBe(weapon2);
        attack2.AttackerTint.ShouldBe(activePlayer.Tint);
        attack2.LineOffset.ShouldBe(5);
    }
    
    [Fact]
    public void WeaponAttacks_ShouldRemoveSpecificAttack_WhenWeaponAttackResolutionCommandReceived()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var player = new Player(playerId, "Player1");
        var targetPlayerId = Guid.NewGuid();
        var targetPlayer = new Player(targetPlayerId, "Player2");
        
        var mechData = MechFactoryTests.CreateDummyMechData();
        mechData.Id = Guid.NewGuid();
        
        // Create a game with the players
        var game = CreateClientGame();
        game.JoinGameWithUnits(player, [mechData],[]);
        game.JoinGameWithUnits(targetPlayer, [],[]);
        game.SetBattleMap(BattleMapTests.BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new ClearTerrain())));
        _sut.Game = game;
        
        // Add units to the game via commands
        game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player1",
            Units = [mechData],
            Tint = "#ffffff",
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            PilotAssignments = []
        });
        
        // Create a target unit
        var targetMechData = MechFactoryTests.CreateDummyMechData();
        targetMechData.Id = Guid.NewGuid();
        game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player2",
            Units = [targetMechData],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = targetPlayerId,
            PilotAssignments = []
        });
        game.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerStatus = PlayerStatus.Ready,
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId
        });
        game.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerStatus = PlayerStatus.Ready,
            GameOriginId = Guid.NewGuid(),
            PlayerId = targetPlayerId
        });
        
        // Deploy units to positions
        var attackerPosition = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        var targetPosition = new HexPosition(new HexCoordinates(1, 0), HexDirection.Top);
        
        // Get the units from the game
        var attacker = _sut.Units.First(u => u.Owner!.Id == playerId);
        var target = _sut.Units.First(u => u.Owner!.Id == targetPlayerId);
        
        // Deploy the units
        attacker.Deploy(attackerPosition);
        target.Deploy(targetPosition);
        
        // Get two weapons from the attacker
        var weapons = attacker.Parts.SelectMany(p => p.GetComponents<Weapon>()).Take(2).ToList();
        var weapon1 = weapons[0];
        var weapon2 = weapons.Count > 1 ? weapons[1] : weapons[0]; // Fallback in case there's only one weapon
        
        // Create weapon target data for two weapons
        var weaponTargetData1 = new WeaponTargetData
        {
            TargetId = target.Id,
            Weapon = new WeaponData
            {
                Location = weapon1.MountedOn!.Location,
                Slots = weapon1.MountedAtSlots,
                Name = weapon1.Name
            },
            IsPrimaryTarget = true
        };
        
        var weaponTargetData2 = new WeaponTargetData
        {
            TargetId = target.Id,
            Weapon = new WeaponData
            {
                Location = weapon2.MountedOn!.Location,
                Slots = weapon2.MountedAtSlots,
                Name = weapon2.Name
            },
            IsPrimaryTarget = true
        };
        
        // Create and handle the weapon attack declaration command with two weapons
        var weaponAttackCommand = new WeaponAttackDeclarationCommand
        {
            PlayerId = playerId,
            AttackerId = attacker.Id,
            WeaponTargets = [weaponTargetData1, weaponTargetData2],
            GameOriginId = Guid.NewGuid()
        };
        
        game.HandleCommand(weaponAttackCommand);
        
        // Verify both attacks are present
        _sut.WeaponAttacks.ShouldNotBeNull();
        _sut.WeaponAttacks.Count.ShouldBe(2);
        
        // Create a resolution command for the first weapon
        var resolutionCommand = new WeaponAttackResolutionCommand
        {
            PlayerId = playerId,
            AttackerId = attacker.Id,
            TargetId = target.Id,
            WeaponData = weaponTargetData1.Weapon,
            ResolutionData = new AttackResolutionData(
                ToHitNumber: 7,
                AttackDirection: HitDirection.Front,
                AttackRoll: [new DiceResult(6)],
                IsHit: true),
            GameOriginId = Guid.NewGuid()
        };
        
        // Act
        game.HandleCommand(resolutionCommand);
        
        // Assert
        _sut.WeaponAttacks.Count.ShouldBe(1); // Only one attack should remain
        
        // The remaining attack should be for weapon2
        var remainingAttack = _sut.WeaponAttacks.First();
        remainingAttack.Weapon.ShouldBe(weapon2);
        remainingAttack.TargetId.ShouldBe(target.Id);
    }
    
    [Fact]
    public void UpdateGamePhase_ShouldTransitionToEndState_WhenPhaseIsEnd()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var player = new Player(playerId, "Player1");
        var clientGame = CreateClientGame();
        clientGame.JoinGameWithUnits(player, [],[]);
        clientGame.SetBattleMap(BattleMapTests.BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        _sut.Game = clientGame;

        _localizationService.GetString("EndPhase_ActionLabel").Returns("End your turn");
        clientGame.HandleCommand(new JoinGameCommand
        {
            PlayerName = player.Name,
            Units = [],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            PilotAssignments = []
        });

        // Act
        clientGame.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.End
        });
        // Set the player as an active player
        clientGame.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            UnitsToPlay = 1
        });

        // Assert
        _sut.CurrentState.ShouldBeOfType<EndState>();
        _sut.ActionInfoLabel.ShouldBe("End your turn");
    }

    [Fact]
    public void UpdateGamePhase_ShouldPersistWeaponAttacksBetweenWeaponsAttackAndResolutionPhases_AndClearOnEndPhase()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var player = new Player(playerId, "Player1");
        var targetPlayerId = Guid.NewGuid();
        var targetPlayer = new Player(targetPlayerId, "Player2");

        var mechData = MechFactoryTests.CreateDummyMechData();
        mechData.Id = Guid.NewGuid();

        // Create a game with the players
        var game = CreateClientGame();
        game.JoinGameWithUnits(player, [],[]);
        game.JoinGameWithUnits(targetPlayer, [],[]);
        game.SetBattleMap(BattleMapTests.BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new ClearTerrain())));
        _sut.Game = game;

        // Add units to the game via commands
        game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player1",
            Units = [mechData],
            Tint = "#ffffff",
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            PilotAssignments = []
        });

        var targetMechData = MechFactoryTests.CreateDummyMechData();
        targetMechData.Id = Guid.NewGuid();
        game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player2",
            Units = [targetMechData],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = targetPlayerId,
            PilotAssignments = []
        });

        game.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerStatus = PlayerStatus.Ready,
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId
        });
        game.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerStatus = PlayerStatus.Ready,
            GameOriginId = Guid.NewGuid(),
            PlayerId = targetPlayerId
        });

        // Deploy units to positions
        var attackerPosition = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        var targetPosition = new HexPosition(new HexCoordinates(1, 0), HexDirection.Top);

        var attacker = _sut.Units.First(u => u.Owner!.Id == playerId);
        var target = _sut.Units.First(u => u.Owner!.Id == targetPlayerId);

        attacker.Deploy(attackerPosition);
        target.Deploy(targetPosition);

        // Get a weapon from the attacker
        var weapon = attacker.Parts.SelectMany(p => p.GetComponents<Weapon>()).First();

        // Create weapon target data
        var weaponTargetData = new WeaponTargetData
        {
            TargetId = target.Id,
            Weapon = new WeaponData
            {
                Location = weapon.MountedOn!.Location,
                Slots = weapon.MountedAtSlots,
                Name = weapon.Name
            },
            IsPrimaryTarget = true
        };

        // Start in WeaponsAttack phase and declare an attack
        game.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.WeaponsAttack
        });

        var weaponAttackCommand = new WeaponAttackDeclarationCommand
        {
            PlayerId = playerId,
            AttackerId = attacker.Id,
            WeaponTargets = [weaponTargetData],
            GameOriginId = Guid.NewGuid()
        };

        game.HandleCommand(weaponAttackCommand);

        // Verify weapon attack is present
        _sut.WeaponAttacks.ShouldNotBeNull();
        _sut.WeaponAttacks.Count.ShouldBe(1);

        // Act - Transition to WeaponAttackResolution phase
        game.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.WeaponAttackResolution
        });

        // Assert - Weapon attacks should persist during resolution phase
        _sut.WeaponAttacks.ShouldNotBeNull();
        _sut.WeaponAttacks.Count.ShouldBe(1, "Weapon attacks should persist between WeaponsAttack and WeaponAttackResolution phases");

        // Act - Transition to End phase
        game.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.End
        });

        // Assert - Weapon attacks should be cleared when transitioning to End phase
        _sut.WeaponAttacks.ShouldBeEmpty("Weapon attacks should be cleared when transitioning to End phase");
    }

    [Fact]
    public void IsPlayerActionButtonVisible_ShouldReturnTrue_WhenInEndPhaseWithActivePlayer()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var player = new Player(playerId, "Player1");
        var clientGame = CreateClientGame();
        clientGame.JoinGameWithUnits(player, [],[]);
        clientGame.SetBattleMap(BattleMapTests.BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        _sut.Game = clientGame;

        
        // Join player
        clientGame.HandleCommand(new JoinGameCommand
        {
            PlayerName = player.Name,
            Units = [MechFactoryTests.CreateDummyMechData()],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            PilotAssignments = []
        });
        // Set up the game state for the End phase
        clientGame.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.End
        });
        
        // Set the player as an active player
        clientGame.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            UnitsToPlay = 0
        });

        // Assert
        _sut.IsPlayerActionButtonVisible.ShouldBeTrue();
    }

    [Fact]
    public void IsPlayerActionButtonVisible_ShouldReturnFalse_WhenNotInEndOrStartPhase()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var player = new Player(playerId, "Player1");
        var clientGame = CreateClientGame();
        clientGame.JoinGameWithUnits(player, [],[]);
        clientGame.SetBattleMap(BattleMapTests.BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        _sut.Game = clientGame;

        _localizationService.GetString("EndPhase_ActionLabel").Returns("End your turn");
        clientGame.HandleCommand(new JoinGameCommand
        {
            PlayerName = player.Name,
            Units = [],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            PilotAssignments = []
        });
        // Set up the game state for the End phase
        clientGame.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.Movement
        });
        
        // Set the player as an active player
        clientGame.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            UnitsToPlay = 1
        });

        // Act
        _sut.HandlePlayerAction();

        // Assert
        _sut.IsPlayerActionButtonVisible.ShouldBeFalse();
    }

    [Fact]
    public void PlayerActionLabel_ReturnsCurrentStatePlayerActionLabel()
    {
        // Arrange
        var clientGame = CreateClientGame();
        var player = new Player(Guid.NewGuid(), "Player1");
        clientGame.JoinGameWithUnits(player,[],[]);
        clientGame.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            PlayerName = player.Name,
            Units = [],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PilotAssignments = []
        });
        clientGame.SetBattleMap(BattleMapTests.BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        _sut.Game = clientGame;
        clientGame.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitsToPlay = 1
        });
        clientGame.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.End
        });
        
        // Act
        var result = _sut.PlayerActionLabel;
        
        // Assert - we expect action label for End phase
        result.ShouldBe("End turn");
    }

    [Fact]
    public void SelectedUnit_WhenSetWithEvents_ShouldUpdateSelectedUnitEvents()
    {
        // Arrange
        var unitId = Guid.NewGuid();
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = unitId;
        
        var player = new Player(Guid.NewGuid(), "Player1");
        _game.JoinGameWithUnits(player, [unitData],[]);
        _game.HandleCommand(new JoinGameCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            PlayerName = player.Name,
            Units = [unitData],
            Tint = player.Tint,
            PilotAssignments = []
        });
        
        var unit = _game.Players[0].Units[0];
        
        // Add some events to the unit
        var damageEvent = new UiEvent(UiEventType.ArmorDamage, "10");
        var explosionEvent = new UiEvent(UiEventType.Explosion, "Ammo");
        unit.AddEvent(damageEvent);
        unit.AddEvent(explosionEvent);
        
        // Act
        _sut.SelectedUnit = unit;
        
        // Assert
        _sut.SelectedUnitEvents.ShouldNotBeNull();
        _sut.SelectedUnitEvents.Count.ShouldBe(2);
        _sut.SelectedUnitEvents[0].Type.ShouldBe(UiEventType.ArmorDamage);
        _sut.SelectedUnitEvents[1].Type.ShouldBe(UiEventType.Explosion);
    }

    [Fact]
    public void ProcessMechStandUp_ShouldCallResumeMovementAfterStandup_WhenInMovementStateWithMatchingUnit()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var player = new Player(playerId, "Player1");
        var mechData = MechFactoryTests.CreateDummyMechData();
        mechData.Id = Guid.NewGuid();
        var game = CreateClientGame();
        game.JoinGameWithUnits(player, [mechData], []);
        game.SetBattleMap(BattleMapTests.BattleMapFactory.GenerateMap(2, 2,
            new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        _sut.Game = game;

        game.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            Units = [mechData],
            PlayerName = player.Name,
            GameOriginId = Guid.NewGuid(),
            Tint = "#FF0000",
            PilotAssignments = []
        });

        game.HandleCommand(new ChangePhaseCommand
        {
            Phase = PhaseNames.Movement,
            GameOriginId = Guid.NewGuid()
        });

        game.HandleCommand(new ChangeActivePlayerCommand
        {
            PlayerId = player.Id,
            GameOriginId = Guid.NewGuid(),
            UnitsToPlay = 1
        });

        // Deploy and select a unit to enter MovementState
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom);
        var unit = _sut.Units.First() as Mech;
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        unit!.AssignPilot(pilot);
        unit.Deploy(position);
        unit.SetProne();
        _sut.HandleHexSelection(game.BattleMap!.GetHexes().First(h => h.Coordinates == position.Coordinates));
        
        game.PilotingSkillCalculator.GetPsrBreakdown(unit, PilotingSkillRollType.StandupAttempt)
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = []
            });

        // Verify we're in MovementState
        var movementState = _sut.CurrentState as MovementState;
        movementState.ShouldNotBeNull();
        _sut.SelectedUnit.ShouldBe(unit);
        var action = movementState.GetAvailableActions()
            .First(a=> a.Label.StartsWith("Walk"));
        action.OnExecute();

        var standUpCommand = new MechStandUpCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = unit.Id,
            PilotingSkillRoll = new PilotingSkillRollData
            {
                RollType = PilotingSkillRollType.StandupAttempt,
                DiceResults = [5, 6],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown
                {
                    BasePilotingSkill = 4,
                    Modifiers = []
                }
            },
            NewFacing = HexDirection.Top
        };

        // Act
        game.HandleCommand(standUpCommand);

        // Assert
        var highlightedHexes = game.BattleMap!.GetHexes().Where(h => h.IsHighlighted).ToList();
        highlightedHexes.ShouldNotBeEmpty();
    }

    [Fact]
    public void ShowAimedShotLocationSelector_SetsUnitPartSelectorAndVisibility()
    {
        // Arrange
        var target = new Mech("Test Mech", "TM-1", 20, 6, []);
        var headBreakdown = CreateTestBreakdown(8);
        var otherBreakdown = CreateTestBreakdown(5);
        var aimedShotSelector = new AimedShotLocationSelectorViewModel(
            target, headBreakdown, otherBreakdown, _ => { }, _localizationService);

        // Act
        _sut.ShowAimedShotLocationSelector(aimedShotSelector);

        // Assert
        _sut.UnitPartSelector.ShouldBe(aimedShotSelector);
        _sut.IsUnitPartSelectorVisible.ShouldBeTrue();
    }

    [Fact]
    public void HideAimedShotLocationSelector_ClearsUnitPartSelectorAndVisibility()
    {
        // Arrange
        var target = new Mech("Test Mech", "TM-1", 20, 6, []);
        var headBreakdown = CreateTestBreakdown(8);
        var otherBreakdown = CreateTestBreakdown(5);
        var aimedShotSelector = new AimedShotLocationSelectorViewModel(
            target, headBreakdown, otherBreakdown, _ => { }, _localizationService);
        _sut.ShowAimedShotLocationSelector(aimedShotSelector);

        // Act
        _sut.HideAimedShotLocationSelector();

        // Assert
        _sut.UnitPartSelector.ShouldBeNull();
        _sut.IsUnitPartSelectorVisible.ShouldBeFalse();
    }

    [Fact]
    public void HideBodyPartSelectorCommand_ShouldHideAimedShotLocationSelector()
    {
        // Arrange
        var target = new Mech("Test Mech", "TM-1", 20, 6, []);
        var headBreakdown = CreateTestBreakdown(8);
        var otherBreakdown = CreateTestBreakdown(5);
        var aimedShotSelector = new AimedShotLocationSelectorViewModel(
            target, headBreakdown, otherBreakdown, _ => { }, _localizationService);
        _sut.ShowAimedShotLocationSelector(aimedShotSelector);

        // Act
        _sut.HideBodyPartSelectorCommand.Execute(null);

        // Assert
        _sut.UnitPartSelector.ShouldBeNull();
        _sut.IsUnitPartSelectorVisible.ShouldBeFalse();
    }

    [Fact]
    public void IsUnitPartSelectorVisible_DefaultsToFalse()
    {
        // Act & Assert
        _sut.IsUnitPartSelectorVisible.ShouldBeFalse();
        _sut.UnitPartSelector.ShouldBeNull();
    }

    private ToHitBreakdown CreateTestBreakdown(int total)
    {
        return new ToHitBreakdown
        {
            HasLineOfSight = true,
            GunneryBase = new GunneryRollModifier { Value = total },
            AttackerMovement = new AttackerMovementModifier
            {
                MovementType = MovementType.StandingStill,
                Value = 0
            },
            TargetMovement = new TargetMovementModifier
            {
                HexesMoved = 0,
                Value = 0
            },
            OtherModifiers = [],
            RangeModifier = new RangeRollModifier
            {
                Value = 0,
                Range = WeaponRange.Long,
                Distance = 5,
                WeaponName = "Test"
            },
            TerrainModifiers = []
        };
    }

    private ClientGame CreateClientGame()
    {
        return new ClientGame(
            new ClassicBattletechRulesProvider(),
            _mechFactory,
            Substitute.For<ICommandPublisher>(),
            Substitute.For<IToHitCalculator>(),
            Substitute.For<IPilotingSkillCalculator>(),
            Substitute.For<IConsciousnessCalculator>(),
            Substitute.For<IHeatEffectsCalculator>(),
            _mapFactory);
    }
}
using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;
using NSubstitute;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Events;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Highlights;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Map.Services;
using Sanet.MakaMek.Presentation.UiStates;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MakaMek.Services;
using Sanet.MVVM.Core.Models;
using Sanet.MVVM.Core.Services;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class BattleMapViewModelTests
{
    private readonly BattleMapViewModel _sut;
    private ClientGame _game;
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IBattleMapFactory _mapFactory = Substitute.For<IBattleMapFactory>();
    private readonly ICommandPublisher _commandPublisher = Substitute.For<ICommandPublisher>();
    private readonly IHashService _hashService = Substitute.For<IHashService>();
    private readonly IMechFactory _mechFactory;
    private static readonly IBattleMapFactory BattleMapFactory = new BattleMapFactory();
    
    private readonly Guid _idempotencyKey = Guid.NewGuid();

    public BattleMapViewModelTests()
    {
        _hashService.ComputeCommandIdempotencyKey(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Type>(),
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<Guid?>(),
            Arg.Any<int>(),
            Arg.Any<string?>())
            .Returns(_idempotencyKey);
        var imageService = Substitute.For<IImageService>();
        var dispatcherService = Substitute.For<IDispatcherService>();
        _sut = new BattleMapViewModel(imageService,
            Substitute.For<ITerrainAssetService>(),
            _localizationService,
            dispatcherService,
            Substitute.For<IRulesProvider>(),
            Substitute.For<IPlatformService>());
        
        // Configure the dispatcher to execute actions immediately
        dispatcherService.RunOnUIThread(Arg.InvokeDelegate<Action>());
        dispatcherService.Scheduler.Returns(Scheduler.Immediate);
        var rules = new TotalWarfareRulesProvider();
        
        _localizationService.GetString("Action_SelectTarget").Returns("Select Target");
        _localizationService.GetString("Action_SelectUnitToFire").Returns("Select unit to fire weapons");
        _localizationService.GetString("Action_SelectUnitToMove").Returns("Select unit to move");
        _localizationService.GetString("Action_SelectUnitToDeploy").Returns("Select Unit");
        _localizationService.GetString("EndPhase_PlayerActionLabel").Returns("End turn");
        _localizationService.GetString("Action_MovementPoints").Returns("{0} | MP: {1}");
        _localizationService.GetString("MovementType_Walk").Returns("Walk");
        _localizationService.GetString("MovementType_Run").Returns("Run");
        _localizationService.GetString("Phase_Deployment").Returns("Deployment");
        _mechFactory = new MechFactory(
            rules,
            new ClassicBattletechComponentProvider(),
            _localizationService);
        _game = CreateClientGame();
        _sut.Game = _game;
    }

    private static void SetCurrentState(BattleMapViewModel sut, IUiState state)
    {
        var property = typeof(BattleMapViewModel).GetProperty(nameof(BattleMapViewModel.CurrentState))
            ?? throw new MissingMemberException(nameof(BattleMapViewModel), nameof(BattleMapViewModel.CurrentState));
        var setter = property.GetSetMethod(true)
            ?? throw new MissingMethodException(nameof(BattleMapViewModel), "set_CurrentState");
        setter.Invoke(sut, [state]);
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
        _sut.TurnPhaseName.ShouldBe("Deployment");
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human, "#FF0000");
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

    [Fact]
    public void ProcessCommand_GameEndedCommand_SetsPropertiesAndNotifies()
    {
        // Arrange
        var propertyChangedEvents = new List<string>();
        _sut.PropertyChanged += (_, e) => propertyChangedEvents.Add(e.PropertyName ?? string.Empty);
        var game = CreateClientGame();
        game.SetBattleMap(BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        _sut.Game = game;
        var command = new GameEndedCommand { GameOriginId = Guid.NewGuid(), Reason = GameEndReason.Victory };

        // Act
        game.HandleCommand(command);

        // Assert
        _sut.IsGameOver.ShouldBeTrue();
        _sut.GameEndReason.ShouldBe(GameEndReason.Victory);
        propertyChangedEvents.ShouldContain(nameof(_sut.Turn)); // NotifyStateChanged is called
    }

    [Fact]
    public void ProcessCommand_BridgeCollapsedCommand_LogsAndUpdatesTerrain()
    {
        // Arrange
        var game = CreateClientGame();
        game.SetBattleMap(BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        _sut.Game = game;
        var hex = game.BattleMap!.GetHexes().First();
        hex.AddTerrain(new BridgeTerrain(2, 40));
        hex.HasTerrain(MakaMekTerrains.Bridge).ShouldBeTrue();

        var command = new BridgeCollapsedCommand
        {
            GameOriginId = Guid.NewGuid(),
            Coordinates = hex.Coordinates.ToData(),
            ConstructionFactor = 40,
            TotalTonnage = 100,
            TriggeringUnitId = Guid.NewGuid()
        };

        // Act
        game.HandleCommand(command);

        // Assert - hex terrain updated
        hex.HasTerrain(MakaMekTerrains.Bridge).ShouldBeFalse();
        hex.HasTerrain(MakaMekTerrains.Rubble).ShouldBeTrue();
        // Assert - command is logged
        _sut.CommandLog.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task NavigateToEndGame_NavigatesToRoot_WhenNotVictory()
    {
        // Arrange
        var navigationService = Substitute.For<INavigationService>();
        var game = CreateClientGame();
        game.SetBattleMap(BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        _sut.Game = game;
        _sut.SetNavigationService(navigationService);
        game.HandleCommand(new GameEndedCommand { GameOriginId = Guid.NewGuid(), Reason = GameEndReason.PlayersLeft });

        // Act
        await _sut.NavigateToEndGame();

        // Assert
        await navigationService.Received(1).NavigateToRootAsync();
    }
    
    [Fact]
    public async Task NavigateToEndGame_GetsEndGameViewModel_WhenVictory()
    {
        // Arrange
        var navigationService = Substitute.For<INavigationService>();
        var endGameViewModel = new EndGameViewModel(_localizationService);
        navigationService.GetNewViewModel<EndGameViewModel>().Returns(endGameViewModel);
        var game = CreateClientGame();
        game.SetBattleMap(BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        _sut.Game = game;
        _sut.SetNavigationService(navigationService);
        game.HandleCommand(new GameEndedCommand { GameOriginId = Guid.NewGuid(), Reason = GameEndReason.Victory });

        // Act
        await _sut.NavigateToEndGame();

        // Assert
        navigationService.Received(1).GetNewViewModel<EndGameViewModel>();
        await navigationService.Received(1).NavigateToViewModelAsync(endGameViewModel);
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
        var player = new Player(playerId, "Player1", PlayerControlType.Human);
        var unitData = MechFactoryTests.CreateDummyMechData();

        _game = CreateClientGame();
        _game.JoinGameWithUnits(player,[unitData],[]);
        _game.SetBattleMap(BattleMapFactory.GenerateMap(2, 2,
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
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
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
        var player1 = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var player2 = new Player(Guid.NewGuid(), "Player2", PlayerControlType.Human);

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
        var player = new Player(playerId, "Player1", PlayerControlType.Human);
        var clientGame = CreateClientGame();
        clientGame.JoinGameWithUnits(player, [],[]);
        clientGame.SetBattleMap(BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())) );
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
        var player = new Player(playerId, "Player1", PlayerControlType.Human);
        var clientGame = CreateClientGame();
        clientGame.JoinGameWithUnits(player,[],[]);
        clientGame.SetBattleMap(BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())));
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
    public void HexConfiguration_ShouldNotBeNull_AfterConstruction()
    {
        // Assert
        _sut.HexConfiguration.ShouldNotBeNull();
    }

    [Fact]
    public void TerrainAssetService_ShouldBeInitialized_FromConstructor()
    {
        // Arrange & Act - _sut is already constructed in the test setup
        var terrainAssetService = _sut.TerrainAssetService;

        // Assert
        terrainAssetService.ShouldNotBeNull();
    }

    [Fact]
    public void TerrainBitmaskService_ShouldBeNull_WhenNotProvided()
    {
        // Arrange & Act - _sut is constructed without terrainBitmaskService parameter
        var terrainBitmaskService = _sut.TerrainBitmaskService;

        // Assert
        terrainBitmaskService.ShouldBeNull();
    }

    [Fact]
    public void TerrainBitmaskService_ShouldBeInitialized_WhenProvided()
    {
        // Arrange
        var imageService = Substitute.For<IImageService>();
        var terrainAssetService = Substitute.For<ITerrainAssetService>();
        var terrainBitmaskService = Substitute.For<ITerrainBitmaskService>();
        var dispatcherService = Substitute.For<IDispatcherService>();
        dispatcherService.RunOnUIThread(Arg.InvokeDelegate<Action>());
        dispatcherService.Scheduler.Returns(Scheduler.Immediate);

        // Act
        var viewModel = new BattleMapViewModel(
            imageService,
            terrainAssetService,
            _localizationService,
            dispatcherService,
            Substitute.For<IRulesProvider>(),
            Substitute.For<IPlatformService>(),
            terrainBitmaskService);

        // Assert
        viewModel.TerrainBitmaskService.ShouldNotBeNull();
        viewModel.TerrainBitmaskService.ShouldBe(terrainBitmaskService);
    }

    [Fact]
    public void HighlightCoordinates_ComputesBoundaryOutlinesForProvidedCoordinates()
    {
        // Arrange
        var viewModel = CreateViewModel(new TerrainBitmaskService());
        var game = CreateClientGame();
        game.SetBattleMap(BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new ClearTerrain())));
        viewModel.Game = game;

        var centerCoords = new HexCoordinates(1, 2);
        var topCoords = centerCoords.GetNeighbour(HexDirection.Top);
        var coordinates = new HashSet<HexCoordinates> { centerCoords, topCoords };

        // Act
        viewModel.HighlightCoordinates(coordinates, new MovementReachableHighlight(MovementType.Walk));

        // Assert
        viewModel.HighlightBoundaryOutlines.Count.ShouldBe(2);
        viewModel.HighlightBoundaryOutlines[centerCoords].EdgeMask.ShouldBe((byte)0b111110);
        viewModel.HighlightBoundaryOutlines[topCoords].EdgeMask.ShouldBe((byte)0b110111);
        viewModel.HighlightBoundaryOutlines[centerCoords].Color.ShouldBe("#00BFFF");
        game.BattleMap!.GetHex(centerCoords)!.HasHighlight<MovementReachableHighlight>().ShouldBeTrue();
        game.BattleMap.GetHex(topCoords)!.HasHighlight<MovementReachableHighlight>().ShouldBeTrue();
    }

    [Fact]
    public void ClearHighlights_ClearsBoundaryOutlines()
    {
        // Arrange
        var viewModel = CreateViewModel(new TerrainBitmaskService());
        var game = CreateClientGame();
        game.SetBattleMap(BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new ClearTerrain())));
        viewModel.Game = game;

        var centerCoords = new HexCoordinates(1, 1);
        viewModel.HighlightCoordinates(
            new HashSet<HexCoordinates> { centerCoords },
            new MovementReachableHighlight(MovementType.Walk));

        // Act
        viewModel.ClearHighlights();

        // Assert
        viewModel.HighlightBoundaryOutlines.ShouldBeEmpty();
    }

    [Fact]
    public void HighlightCoordinates_MergesBoundaryOutlines_WhenCalledWithDifferentHighlightTypes()
    {
        // Arrange
        var viewModel = CreateViewModel(new TerrainBitmaskService());
        var game = CreateClientGame();
        game.SetBattleMap(BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new ClearTerrain())));
        viewModel.Game = game;

        var movementCoords = new HexCoordinates(1, 1);
        var attackCoords = new HexCoordinates(2, 2);
        var attackTopCoords = attackCoords.GetNeighbour(HexDirection.Top);
        var movementSet = new HashSet<HexCoordinates> { movementCoords };
        var attackSet = new HashSet<HexCoordinates> { attackCoords, attackTopCoords };

        // Act
        viewModel.HighlightCoordinates(movementSet, new MovementReachableHighlight(MovementType.Walk));
        viewModel.HighlightCoordinates(attackSet, new AttackReachableHighlight(new List<string> { "Medium Laser" }));

        // Assert
        viewModel.HighlightBoundaryOutlines.Count.ShouldBe(3);
        viewModel.HighlightBoundaryOutlines[movementCoords].Color.ShouldBe("#00BFFF");
        viewModel.HighlightBoundaryOutlines[attackCoords].Color.ShouldBe("#FFB347");
        viewModel.HighlightBoundaryOutlines[attackTopCoords].Color.ShouldBe("#FFB347");
        game.BattleMap!.GetHex(movementCoords)!.HasHighlight<MovementReachableHighlight>().ShouldBeTrue();
        game.BattleMap.GetHex(attackCoords)!.HasHighlight<AttackReachableHighlight>().ShouldBeTrue();
        game.BattleMap.GetHex(attackTopCoords)!.HasHighlight<AttackReachableHighlight>().ShouldBeTrue();
    }

    [Fact]
    public void HighlightRegions_ComputesBoundaryOutlinesForProvidedRegions()
    {
        // Arrange
        var viewModel = CreateViewModel(new TerrainBitmaskService());
        var game = CreateClientGame();
        game.SetBattleMap(BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new ClearTerrain())));
        viewModel.Game = game;

        var movementCoords = new HexCoordinates(1, 1);
        var attackCoords = new HexCoordinates(2, 2);
        var attackNeighbour = attackCoords.GetNeighbour(HexDirection.Top);
        var regions = new Dictionary<HexCoordinates, IHexHighlightType>
        {
            { movementCoords, new MovementReachableHighlight(MovementType.Walk) },
            { attackCoords, new AttackReachableHighlight(new List<string> { "Medium Laser" }) },
            { attackNeighbour, new AttackReachableHighlight(new List<string> { "Medium Laser" }) }
        };

        // Act
        viewModel.HighlightRegions(regions);

        // Assert
        viewModel.HighlightBoundaryOutlines.Count.ShouldBe(3);
        viewModel.HighlightBoundaryOutlines[movementCoords].Color.ShouldBe("#00BFFF");
        viewModel.HighlightBoundaryOutlines[attackCoords].Color.ShouldBe("#FFB347");
        viewModel.HighlightBoundaryOutlines[attackNeighbour].Color.ShouldBe("#FFB347");
        game.BattleMap!.GetHex(movementCoords)!.HasHighlight<MovementReachableHighlight>().ShouldBeTrue();
        game.BattleMap.GetHex(attackCoords)!.HasHighlight<AttackReachableHighlight>().ShouldBeTrue();
        game.BattleMap.GetHex(attackNeighbour)!.HasHighlight<AttackReachableHighlight>().ShouldBeTrue();
    }

    [Fact]
    public void HighlightRegions_DoesNothing_WhenDictionaryIsEmpty()
    {
        // Arrange
        var viewModel = CreateViewModel(new TerrainBitmaskService());
        var game = CreateClientGame();
        game.SetBattleMap(BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new ClearTerrain())));
        viewModel.Game = game;

        viewModel.HighlightCoordinates(
            new HashSet<HexCoordinates> { new(1, 1) },
            new MovementReachableHighlight(MovementType.Walk));

        // Act
        viewModel.HighlightRegions(new Dictionary<HexCoordinates, IHexHighlightType>());

        // Assert
        viewModel.HighlightBoundaryOutlines.ShouldNotBeEmpty();
        game.BattleMap!.GetHex(new HexCoordinates(1, 1))!.HasHighlight<MovementReachableHighlight>().ShouldBeTrue();
    }

    [Fact]
    public void HighlightCoordinates_ClearsBoundaryOutlines_WhenTerrainBitmaskServiceIsNull()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var game = CreateClientGame();
        game.SetBattleMap(BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new ClearTerrain())));
        viewModel.Game = game;

        var coordinates = new HashSet<HexCoordinates> { new(1, 1) };

        // Act
        viewModel.HighlightCoordinates(
            coordinates,
            new MovementReachableHighlight(MovementType.Walk));

        // Assert
        viewModel.HighlightBoundaryOutlines.ShouldBeEmpty();
        game.BattleMap!.GetHex(new HexCoordinates(1, 1))!.HasHighlight<MovementReachableHighlight>().ShouldBeTrue();
    }

    [Fact]
    public void HighlightRegions_ClearsBoundaryOutlines_WhenTerrainBitmaskServiceIsNull()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var game = CreateClientGame();
        game.SetBattleMap(BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new ClearTerrain())));
        viewModel.Game = game;

        var regions = new Dictionary<HexCoordinates, IHexHighlightType>
        {
            { new HexCoordinates(1, 1), new MovementReachableHighlight(MovementType.Walk) }
        };

        // Act
        viewModel.HighlightRegions(regions);

        // Assert
        viewModel.HighlightBoundaryOutlines.ShouldBeEmpty();
        game.BattleMap!.GetHex(new HexCoordinates(1, 1))!.HasHighlight<MovementReachableHighlight>().ShouldBeTrue();
    }

    [Fact]
    public void RemoveHighlight_RemovesSpecificHighlightFromCoordinates()
    {
        // Arrange
        var viewModel = CreateViewModel(new TerrainBitmaskService());
        var game = CreateClientGame();
        game.SetBattleMap(BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new ClearTerrain())));
        viewModel.Game = game;

        var coords1 = new HexCoordinates(1, 1);
        var coords2 = new HexCoordinates(1, 2);
        viewModel.HighlightCoordinates(
            new HashSet<HexCoordinates> { coords1, coords2 },
            new MovementReachableHighlight(MovementType.Walk));

        // Act
        viewModel.RemoveHighlight<MovementReachableHighlight>(new HashSet<HexCoordinates> { coords1 });

        // Assert
        viewModel.HighlightBoundaryOutlines.Count.ShouldBe(1);
        viewModel.HighlightBoundaryOutlines.ContainsKey(coords1).ShouldBeFalse();
        viewModel.HighlightBoundaryOutlines.ContainsKey(coords2).ShouldBeTrue();
        game.BattleMap!.GetHex(coords1)!.HasHighlight<MovementReachableHighlight>().ShouldBeFalse();
        game.BattleMap.GetHex(coords2)!.HasHighlight<MovementReachableHighlight>().ShouldBeTrue();
    }

    [Fact]
    public void RemoveHighlight_DoesNothing_WhenCoordinatesAreNotHighlighted()
    {
        // Arrange
        var viewModel = CreateViewModel(new TerrainBitmaskService());
        var game = CreateClientGame();
        game.SetBattleMap(BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new ClearTerrain())));
        viewModel.Game = game;

        var coords = new HexCoordinates(1, 1);
        viewModel.HighlightCoordinates(
            new HashSet<HexCoordinates> { coords },
            new MovementReachableHighlight(MovementType.Walk));

        // Pre-assert
        viewModel.HighlightBoundaryOutlines.Count.ShouldBe(1);

        // Act
        viewModel.RemoveHighlight<MovementReachableHighlight>(new HashSet<HexCoordinates> { new(9, 9) });

        // Assert
        viewModel.HighlightBoundaryOutlines.Count.ShouldBe(1);
        game.BattleMap!.GetHex(coords)!.HasHighlight<MovementReachableHighlight>().ShouldBeTrue();
    }

    [Fact]
    public void Scheduler_ShouldReturnDispatcherServiceScheduler()
    {
        var scheduler = _sut.Scheduler;
        scheduler.ShouldBe(Scheduler.Immediate);
    }

    [Fact]
    public void IsMapSettingsPanelVisible_ShouldBeFalse_ByDefault()
    {
        // Assert
        _sut.IsMapSettingsPanelVisible.ShouldBeFalse();
    }

    [Fact]
    public void ToggleMapSettings_ShouldToggleIsMapSettingsPanelVisible()
    {
        // Arrange
        var propertyChangedEvents = new List<string>();
        _sut.PropertyChanged += (_, e) => propertyChangedEvents.Add(e.PropertyName ?? string.Empty);

        // Act & Assert - First toggle
        _sut.ToggleMapSettings();
        _sut.IsMapSettingsPanelVisible.ShouldBeTrue();
        propertyChangedEvents.ShouldContain(nameof(BattleMapViewModel.IsMapSettingsPanelVisible));

        // Clear events for the second test
        propertyChangedEvents.Clear();

        // Act & Assert - Second toggle
        _sut.ToggleMapSettings();
        _sut.IsMapSettingsPanelVisible.ShouldBeFalse();
        propertyChangedEvents.ShouldContain(nameof(BattleMapViewModel.IsMapSettingsPanelVisible));
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
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        _game = CreateClientGame();
        _game.JoinGameWithUnits(player,[],[]);
        _game.SetBattleMap(BattleMapFactory.GenerateMap(2, 2,
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
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
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
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        _game = CreateClientGame();
        _game.JoinGameWithUnits(player,[],[]);
        _game.SetBattleMap(BattleMapFactory.GenerateMap(2, 2,
            new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        _sut.Game = _game;
        _game.HandleCommand(new JoinGameCommand()
        {
            PlayerId = player.Id,
            Units = [MechFactoryTests.CreateDummyMechData()],
            PlayerName = player.Name,
            GameOriginId = Guid.NewGuid(),
            Tint = "#FF0000",
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
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
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        _game = CreateClientGame();
        _game.JoinGameWithUnits(player,[],[]);
        _game.SetBattleMap(BattleMapFactory.GenerateMap(2, 2,
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
    public void ShowSurfaceSelector_SetsPositionViewModelAndVisibility()
    {
        // Arrange
        var position = new HexCoordinates(2, 3);
        var surfaceSelector = new SurfaceSelectorViewModel(
            [],
            _ => { },
            _localizationService);

        // Act
        _sut.ShowSurfaceSelector(position, surfaceSelector);

        // Assert
        _sut.SurfaceSelectorPosition.ShouldBe(position);
        _sut.SurfaceSelector.ShouldBe(surfaceSelector);
        _sut.IsSurfaceSelectorVisible.ShouldBeTrue();
    }

    [Fact]
    public void HideSurfaceSelector_ClearsVisibilityViewModelAndPosition()
    {
        // Arrange
        var position = new HexCoordinates(2, 3);
        var surfaceSelector = new SurfaceSelectorViewModel(
            [],
            _ => { },
            _localizationService);
        _sut.ShowSurfaceSelector(position, surfaceSelector);

        // Act
        _sut.HideSurfaceSelector();

        // Assert
        _sut.IsSurfaceSelectorVisible.ShouldBeFalse();
        _sut.SurfaceSelector.ShouldBeNull();
        _sut.SurfaceSelectorPosition.ShouldBeNull();
    }

    [Fact]
    public void HideSurfaceSelectorCommand_ExecutesHideSurfaceSelector()
    {
        // Arrange
        var position = new HexCoordinates(2, 3);
        var surfaceSelector = new SurfaceSelectorViewModel(
            [],
            _ => { },
            _localizationService);
        _sut.ShowSurfaceSelector(position, surfaceSelector);

        // Act
        _sut.HideSurfaceSelectorCommand.Execute(null);

        // Assert
        _sut.IsSurfaceSelectorVisible.ShouldBeFalse();
        _sut.SurfaceSelector.ShouldBeNull();
        _sut.SurfaceSelectorPosition.ShouldBeNull();
    }

    [Fact]
    public void SurfaceSelectedCommand_CallsSelectSurfaceOnSelector()
    {
        // Arrange
        var hexSurface = HexSurface.Bridge;
        var selectedSurfaces = new List<HexSurface>();
        var surfaceSelector = new SurfaceSelectorViewModel(
            [],
            surface => selectedSurfaces.Add(surface),
            _localizationService);
        _sut.ShowSurfaceSelector(new HexCoordinates(1, 1), surfaceSelector);

        // Act
        _sut.SurfaceSelectedCommand.Execute(hexSurface);

        // Assert
        selectedSurfaces.ShouldHaveSingleItem();
        selectedSurfaces[0].ShouldBe(hexSurface);
    }

    [Theory]
    [InlineData(HexDirection.Top)]
    [InlineData(HexDirection.Bottom)]
    [InlineData(HexDirection.TopRight)]
    public void DirectionSelectedCommand_CallsHandleFacingSelectionOnCurrentState(HexDirection direction)
    {
        // Arrange
        var mockState = Substitute.For<IUiState>();
        SetCurrentState(_sut, mockState);

        // Act
        _sut.DirectionSelectedCommand.Execute(direction);

        // Assert
        mockState.Received(1).HandleFacingSelection(direction);
    }

    [Fact]
    public void IsSurfaceSelectorVisible_DefaultsToFalse()
    {
        // Act & Assert
        _sut.IsSurfaceSelectorVisible.ShouldBeFalse();
        _sut.SurfaceSelector.ShouldBeNull();
        _sut.SurfaceSelectorPosition.ShouldBeNull();
    }

    [Fact]
    public void IsRecordSheetButtonVisible_NoSelectedUnit_ReturnsFalse()
    {
        // Arrange
        _game.HandleCommand(new ChangePhaseCommand { GameOriginId = Guid.NewGuid(), Phase = PhaseNames.End });
        _sut.SelectedUnit = null;
        _sut.IsRecordSheetExpanded = false;

        // Act & Assert
        _sut.IsRecordSheetButtonVisible.ShouldBeFalse();
    }
    
    [Fact]
    public void IsRecordSheetPanelVisible_NoSelectedUnit_ReturnsFalse()
    {
        // Arrange
        _game.HandleCommand(new ChangePhaseCommand { GameOriginId = Guid.NewGuid(), Phase = PhaseNames.End });
        _sut.SelectedUnit = null;
        _sut.IsRecordSheetExpanded = false;

        // Act & Assert
        _sut.IsRecordSheetPanelVisible.ShouldBeFalse();
    }

    [Fact]
    public void IsRecordSheetButtonVisible_HasSelectedUnitButExpanded_ReturnsFalse()
    {
        var mockState = Substitute.For<IUiState>();
        var unit = new Mech("Mech", "MK1",20,[]);
        mockState.SelectedUnit.Returns(unit);
        SetCurrentState(_sut, mockState);
        _sut.IsRecordSheetExpanded = true;

        // Act & Assert
        _sut.IsRecordSheetButtonVisible.ShouldBeFalse();
    }

    [Fact]
    public void IsRecordSheetButtonVisible_HasSelectedUnitNotExpanded_ReturnsTrue()
    {
        var mockState = Substitute.For<IUiState>();
        var unit = new Mech("Mech", "MK1",20,[]);
        mockState.SelectedUnit.Returns(unit);
        SetCurrentState(_sut, mockState);
        _sut.IsRecordSheetExpanded = false;

        // Act & Assert
        _sut.IsRecordSheetButtonVisible.ShouldBeTrue();
    }
    
    [Fact]
    public void IsRecordSheetPanelVisible_HasSelectedUnitButExpanded_ReturnsTrue()
    {
        var mockState = Substitute.For<IUiState>();
        var unit = new Mech("Mech", "MK1",20,[]);
        mockState.SelectedUnit.Returns(unit);
        SetCurrentState(_sut, mockState);
        _sut.IsRecordSheetExpanded = true;

        // Act & Assert
        _sut.IsRecordSheetPanelVisible.ShouldBeTrue();
    }

    [Fact]
    public void IsRecordSheetPanelVisible_HasSelectedUnitNotExpanded_ReturnsFalse()
    {
        var mockState = Substitute.For<IUiState>();
        var unit = new Mech("Mech", "MK1",20,[]);
        mockState.SelectedUnit.Returns(unit);
        SetCurrentState(_sut, mockState);
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
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        };

        // Act
        _sut.ShowMovementPath(new MovementPath(path, MovementType.Walk));

        // Assert
        _sut.MovementPath.ShouldNotBeNull();
        _sut.MovementPath[0].From.ShouldBe(path[0].From);
        _sut.MovementPath[0].To.ShouldBe(path[0].To);
    }

    [Fact]
    public void ShowMovementPath_WithNull_ClearsMovementPath()
    {
        // Arrange
        var path = new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        };
        _sut.ShowMovementPath(new MovementPath(path, MovementType.Walk));

        // Act
        _sut.ShowMovementPath(null);

        // Assert
        _sut.MovementPath.ShouldBeNull();
    }
    
    [Fact]
    public void ShowMovementPath_WithCostLessPath_ClearsMovementPath()
    {
        // Arrange
        var path = new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        };
        _sut.ShowMovementPath(new MovementPath(path, MovementType.Walk));

        // Act
        _sut.ShowMovementPath(new MovementPath(new List<PathSegment>
        {
            new(
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
                [])
        }, MovementType.Walk));

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
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        };
        var propertyChanged = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BattleMapViewModel.MovementPath))
                propertyChanged = true;
        };

        // Act
        _sut.ShowMovementPath(new MovementPath(path, MovementType.Walk));

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
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        };
        _sut.ShowMovementPath(new MovementPath(path, MovementType.Jump));
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
        var battleMap = BattleMapFactory.GenerateMap(
            2, 11,
            new SingleTerrainGenerator(2, 11, new ClearTerrain()));
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
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
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
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
        unit.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        unit.Deploy(position, null);
        
        // Select unit
        _sut.HandleHexSelection(game.BattleMap!.GetHexes().First(h=>h.Coordinates==position.Coordinates));

        // Act
        var items = _sut.WeaponSelectionItems.ToList();

        // Assert
        items.ShouldNotBeEmpty();
        items.Count.ShouldBe(unit.Parts.Values.Sum(p => p.GetComponents<Weapon>().Count()));
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
        var battleMap = BattleMapFactory.GenerateMap(
            2, 11,
            new SingleTerrainGenerator(2, 11, new ClearTerrain()));
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
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
        unit.Deploy(position, null);
        
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
        var battleMap = BattleMapFactory.GenerateMap(
            2, 11,
            new SingleTerrainGenerator(2, 11, new ClearTerrain()));
        var player1 = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var player2 = new Player(Guid.NewGuid(), "Player2", PlayerControlType.Human);
        var game = CreateClientGame();
        game.JoinGameWithUnits(player1,[],[]);
        game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player1",
            Units = [mechData],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = player1.Id,
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
        });
        game.JoinGameWithUnits(player2,[],[]);
        game.SetBattleMap(battleMap);
        
        _sut.Game = game;
        
        game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player2",
            Units = [mechData],
            Tint = "#00FF00",
            GameOriginId = Guid.NewGuid(),
            PlayerId = player2.Id,
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
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
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        attacker.Deploy(attackerPosition, null);
        
        var targetPosition = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        var target = _sut.Units.First(u => u.Owner!.Id == player2.Id);
        target.Deploy(targetPosition, null);
        
        // Select attacker
        _sut.HandleHexSelection(game.BattleMap!.GetHexes().First(h=>h.Coordinates==attackerPosition.Coordinates));
        
        // Select target
        var selectTargetAction = _sut.AvailableActions.First(a => a.Label == "Select Target");
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
        var battleMap = BattleMapFactory.GenerateMap(
            2, 11,
            new SingleTerrainGenerator(2, 11, new ClearTerrain()));
        var player1 = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
        var player2 = new Player(Guid.NewGuid(), "Player2", PlayerControlType.Human);
        var game = CreateClientGame();
        game.JoinGameWithUnits(player1,[],[]);
        game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player1",
            Units = [mechData],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = player1.Id,
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
        });
        game.JoinGameWithUnits(player2,[],[]);
        game.SetBattleMap(battleMap);
        
        _sut.Game = game;
        
        game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player2",
            Units = [mechData],
            Tint = "#00FF00",
            GameOriginId = Guid.NewGuid(),
            PlayerId = player2.Id,
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
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
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        attacker.Deploy(attackerPosition, null);
        
        var targetPosition = new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom);
        var target = _sut.Units.First(u => u.Owner!.Id == player2.Id);
        target.Deploy(targetPosition, null);
        
        // Select attacker
        _sut.HandleHexSelection(game.BattleMap!.GetHexes().First(h=>h.Coordinates==attackerPosition.Coordinates));
        
        // Select target
        var selectTargetAction = _sut.AvailableActions.First(a => a.Label == "Select Target");
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
        var battleMap = BattleMapFactory.GenerateMap(
            2, 11,
            new SingleTerrainGenerator(2, 11, new ClearTerrain()));
        var player1 = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
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
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
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
        attacker.Deploy(attackerPosition, null);

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
        var battleMap = BattleMapFactory.GenerateMap(
            2, 11,
            new SingleTerrainGenerator(2, 11, new ClearTerrain()));
        var player1 = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
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
        var player = new Player(playerId, "Player1", PlayerControlType.Human);
        var targetPlayerId = Guid.NewGuid();
        var targetPlayer = new Player(targetPlayerId, "Player2", PlayerControlType.Human);
        
        var mechData = MechFactoryTests.CreateDummyMechData();
        mechData.Id = Guid.NewGuid();
        // Create a game with the players
        var game = CreateClientGame();
        game.JoinGameWithUnits(player, [],[]);
        game.JoinGameWithUnits(targetPlayer, [],[]);
        game.SetBattleMap(BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new ClearTerrain())));
        
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
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        var target = _sut.Units.First(u => u.Owner!.Id == targetPlayerId);
        attacker.Deploy(attackerPosition, null);
        target.Deploy(targetPosition, null);
        
        // Get a weapon from the attacker to use in the command
        var weapon = attacker.Parts.Values.SelectMany(p => p.GetComponents<Weapon>()).First();
        
        // Create weapon target data
        var weaponTargetData = new WeaponTargetData
        {
            TargetId = target.Id,
            Weapon = new ComponentData
            {
                Name = weapon.Name,
                Type = weapon.ComponentType,
                Assignments = [new LocationSlotAssignment(weapon.MountedOn[0].Location, weapon.MountedAtFirstLocationSlots.First(), weapon.MountedAtFirstLocationSlots.Length)]
            },
            IsPrimaryTarget = true
        };
        
        // Create the weapon attack declaration command
        var weaponAttackCommand = new WeaponAttackDeclarationCommand
        {
            PlayerId = playerId,
            UnitId = attacker.Id,
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
        var activePlayer = new Player(playerId, "Player1", PlayerControlType.Human);
        var targetPlayerId = Guid.NewGuid();
        var targetPlayer = new Player(targetPlayerId, "Player2", PlayerControlType.Human);
        
        var mechData = MechFactoryTests.CreateDummyMechData();
        mechData.Id = Guid.NewGuid();
        
        // Create a game with the players
        var game = CreateClientGame();
        game.JoinGameWithUnits(activePlayer, [],[]);
        game.JoinGameWithUnits(targetPlayer, [],[]);
        game.SetBattleMap(BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new ClearTerrain())));
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
        attacker1.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        var attacker2 = attackers[1];
        attacker2.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        var target = _sut.Units.First(u => u.Owner!.Id == targetPlayerId);
        
        // Deploy the units
        attacker1.Deploy(attacker1Position, null);
        attacker2.Deploy(attacker2Position, null);
        target.Deploy(targetPosition, null);
        
        // Get weapons from the attackers
        var weapon1 = attacker1.Parts.Values.SelectMany(p => p.GetComponents<Weapon>()).First();
        var weapon2 = attacker2.Parts.Values.SelectMany(p => p.GetComponents<Weapon>()).First();
        
        // Create weapon target data
        var weaponTargetData1 = new WeaponTargetData
        {
            TargetId = target.Id,
            Weapon = new ComponentData
            {
                Name = weapon1.Name,
                Type = weapon1.ComponentType,
                Assignments = [new LocationSlotAssignment(weapon1.MountedOn[0].Location, weapon1.MountedAtFirstLocationSlots.First(), weapon1.MountedAtFirstLocationSlots.Length)]
            },
            IsPrimaryTarget = true
        };
        
        var weaponTargetData2 = new WeaponTargetData
        {
            TargetId = target.Id,
            Weapon = new ComponentData
            {
                Name = weapon2.Name,
                Type = weapon2.ComponentType,
                Assignments = [new LocationSlotAssignment(weapon2.MountedOn[0].Location, weapon2.MountedAtFirstLocationSlots.First(), weapon2.MountedAtFirstLocationSlots.Length)]
            },
            IsPrimaryTarget = true
        };
        
        // Create the weapon attack declaration commands
        var weaponAttackCommand1 = new WeaponAttackDeclarationCommand
        {
            PlayerId = playerId,
            UnitId = attacker1.Id,
            WeaponTargets = [weaponTargetData1],
            GameOriginId = Guid.NewGuid()
        };
        
        var weaponAttackCommand2 = new WeaponAttackDeclarationCommand
        {
            PlayerId = playerId,
            UnitId = attacker2.Id,
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
        var player = new Player(playerId, "Player1", PlayerControlType.Human);
        var targetPlayerId = Guid.NewGuid();
        var targetPlayer = new Player(targetPlayerId, "Player2", PlayerControlType.Human);
        
        var mechData = MechFactoryTests.CreateDummyMechData();
        mechData.Id = Guid.NewGuid();
        
        // Create a game with the players
        var game = CreateClientGame();
        game.JoinGameWithUnits(player, [mechData],[]);
        game.JoinGameWithUnits(targetPlayer, [],[]);
        game.SetBattleMap(BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new ClearTerrain())));
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
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        attacker.Parts[PartLocation.RightTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        var target = _sut.Units.First(u => u.Owner!.Id == targetPlayerId);
        
        // Deploy the units
        attacker.Deploy(attackerPosition, null);
        target.Deploy(targetPosition, null);
        
        // Get two weapons from the attacker
        var weapons = attacker.Parts.Values.SelectMany(p => p.GetComponents<Weapon>()).Take(2).ToList();
        var weapon1 = weapons[0];
        var weapon2 = weapons.Count > 1 ? weapons[1] : weapons[0]; // Fallback in case there's only one weapon
        
        // Create weapon target data for two weapons
        var weaponTargetData1 = new WeaponTargetData
        {
            TargetId = target.Id,
            Weapon = new ComponentData
            {
                Name = weapon1.Name,
                Type = weapon1.ComponentType,
                Assignments = [new LocationSlotAssignment(weapon1.MountedOn[0].Location, weapon1.MountedAtFirstLocationSlots.First(), weapon1.MountedAtFirstLocationSlots.Length)]
            },
            IsPrimaryTarget = true
        };
        
        var weaponTargetData2 = new WeaponTargetData
        {
            TargetId = target.Id,
            Weapon = new ComponentData
            {
                Name = weapon2.Name,
                Type = weapon2.ComponentType,
                Assignments = [new LocationSlotAssignment(weapon2.MountedOn[0].Location, weapon2.MountedAtFirstLocationSlots.First(), weapon2.MountedAtFirstLocationSlots.Length)]
            },
            IsPrimaryTarget = true
        };
        
        // Create and handle the weapon attack declaration command with two weapons
        var weaponAttackCommand = new WeaponAttackDeclarationCommand
        {
            PlayerId = playerId,
            UnitId = attacker.Id,
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
                IsHit: true,
                ExternalHeat: 0),
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
        var player = new Player(playerId, "Player1", PlayerControlType.Human);
        var clientGame = CreateClientGame();
        clientGame.JoinGameWithUnits(player, [],[]);
        clientGame.SetBattleMap(BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())));
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
        var player = new Player(playerId, "Player1", PlayerControlType.Human);
        var targetPlayerId = Guid.NewGuid();
        var targetPlayer = new Player(targetPlayerId, "Player2", PlayerControlType.Human);

        var mechData = MechFactoryTests.CreateDummyMechData();
        mechData.Id = Guid.NewGuid();

        // Create a game with the players
        var game = CreateClientGame();
        game.JoinGameWithUnits(player, [],[]);
        game.JoinGameWithUnits(targetPlayer, [],[]);
        game.SetBattleMap(BattleMapFactory.GenerateMap(3, 3, new SingleTerrainGenerator(3, 3, new ClearTerrain())));
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

        attacker.Deploy(attackerPosition, null);
        attacker.Parts[PartLocation.LeftTorso].TryAddComponent(new MediumLaser(),[1]).ShouldBeTrue();
        target.Deploy(targetPosition, null);

        // Get a weapon from the attacker
        var weapon = attacker.Parts.Values.SelectMany(p => p.GetComponents<Weapon>()).First();

        // Create weapon target data
        var weaponTargetData = new WeaponTargetData
        {
            TargetId = target.Id,
            Weapon = new ComponentData
            {
                Name = weapon.Name,
                Type = weapon.ComponentType,
                Assignments = [new LocationSlotAssignment(weapon.MountedOn[0].Location, weapon.MountedAtFirstLocationSlots.First(), weapon.MountedAtFirstLocationSlots.Length)]
            },
            IsPrimaryTarget = true
        };

        // Start in the WeaponsAttack phase and declare an attack
        game.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.WeaponsAttack
        });

        var weaponAttackCommand = new WeaponAttackDeclarationCommand
        {
            PlayerId = playerId,
            UnitId = attacker.Id,
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

        // Assert - Weapon attacks should persist during the resolution phase
        _sut.WeaponAttacks.ShouldNotBeNull();
        _sut.WeaponAttacks.Count.ShouldBe(1, "Weapon attacks should persist between WeaponsAttack and WeaponAttackResolution phases");

        // Act - Transition to End phase (two-stage protocol)
        game.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.End
        });

        game.HandleCommand(new StartPhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.End
        });

        // Assert - Weapon attacks should be cleared when transitioning to the End phase
        _sut.WeaponAttacks.ShouldBeEmpty("Weapon attacks should be cleared when transitioning to End phase");
    }

    [Fact]
    public void IsPlayerActionButtonVisible_ShouldReturnTrue_WhenInEndPhaseWithActivePlayer()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var player = new Player(playerId, "Player1", PlayerControlType.Human);
        var clientGame = CreateClientGame();
        clientGame.JoinGameWithUnits(player, [],[]);
        clientGame.SetBattleMap(BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        _sut.Game = clientGame;

        
        // Join player
        clientGame.HandleCommand(new JoinGameCommand
        {
            PlayerName = player.Name,
            Units = [MechFactoryTests.CreateDummyMechData()],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
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
        var player = new Player(playerId, "Player1", PlayerControlType.Human);
        var clientGame = CreateClientGame();
        clientGame.JoinGameWithUnits(player, [],[]);
        clientGame.SetBattleMap(BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())));
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
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
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
        clientGame.SetBattleMap(BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())));
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
        
        // Assert - we expect an action label for the End phase
        result.ShouldBe("End turn");
    }

    [Fact]
    public void SelectedUnit_WhenSetWithEvents_ShouldUpdateSelectedUnitEvents()
    {
        // Arrange
        var unitId = Guid.NewGuid();
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = unitId;
        
        var player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);
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
        
        var mockState = Substitute.For<IUiState>();
        mockState.SelectedUnit.Returns(unit);
        SetCurrentState(_sut, mockState);

        // Act
        _sut.NotifySelectedUnitChanged();
        
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
        var player = new Player(playerId, "Player1", PlayerControlType.Human);
        var mechData = MechFactoryTests.CreateDummyMechData();
        mechData.Id = Guid.NewGuid();
        var game = CreateClientGame();
        game.JoinGameWithUnits(player, [mechData], []);
        game.SetBattleMap(BattleMapFactory.GenerateMap(2, 2,
            new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        _sut.Game = game;

        game.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            Units = [mechData],
            PlayerName = player.Name,
            GameOriginId = Guid.NewGuid(),
            Tint = "#FF0000",
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
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
        unit.Deploy(position, null);
        unit.SetProne();
        _sut.HandleHexSelection(game.BattleMap!.GetHexes().First(h => h.Coordinates == position.Coordinates));
        
        game.PilotingSkillCalculator.GetPsrBreakdown(unit, new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt))
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
                RollContext = new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt),
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
        var highlightedHexes = game.BattleMap!.GetHexes().Where(h => h.HasHighlight<MovementReachableHighlight>()).ToList();
        highlightedHexes.ShouldNotBeEmpty();
    }

    [Fact]
    public void ProcessMechFall_ShouldCallResumeMovementAfterFall_WhenInMovementStateWithMatchingUnit()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var player = new Player(playerId, "Player1", PlayerControlType.Human);
        var mechData = MechFactoryTests.CreateDummyMechData();
        mechData.Id = Guid.NewGuid();
        var game = CreateClientGame();
        game.JoinGameWithUnits(player, [mechData], []);
        game.SetBattleMap(BattleMapFactory.GenerateMap(2, 2,
            new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        _sut.Game = game;

        game.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            Units = [mechData],
            PlayerName = player.Name,
            GameOriginId = Guid.NewGuid(),
            Tint = "#FF0000",
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
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
        unit.Deploy(position, null);
        // Unit starts standing
        
        _sut.HandleHexSelection(game.BattleMap!.GetHexes().First(h => h.Coordinates == position.Coordinates));
        
        // Mock Psr for Standup (needed if we end up checking standup availability)
        game.PilotingSkillCalculator.GetPsrBreakdown(unit, new PilotingSkillRollContext(PilotingSkillRollType.StandupAttempt))
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = []
            });

        // Verify we're in MovementState
        var movementState = _sut.CurrentState as MovementState;
        movementState.ShouldNotBeNull();
        _sut.SelectedUnit.ShouldBe(unit);

        // Select Movement Type (e.g., Walk) to initialize _selectedPath
        var action = movementState.GetAvailableActions()
            .First(a => a.Label.StartsWith("Walk"));
        action.OnExecute();
        
        unit.Move(MovementPath.CreateSingleSegmentPath(position,MovementType.Walk), null, false);

        var fallCommand = new MechFallCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = unit.Id,
            LevelsFallen = 0,
            WasJumping = false,
            DamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 0),
                new DiceResult(1),
                HitDirection.Front
            )
        };

        // Act
        game.HandleCommand(fallCommand);

        // Assert
        _localizationService.GetString("Action_StayProne").Returns("StayProne");

        // Verify that there is only one option to continue movement
        var actions = movementState.GetAvailableActions().ToList();
        actions.Count.ShouldBe(3);
        actions.ShouldContain(a => a.Label.Contains("Walk"));
    }
    
    [Fact]
    public void ProcessMechFall_ShouldNotCallProcessMechStandUp_WhenDamageDataIsNull()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var player = new Player(playerId, "Player1", PlayerControlType.Human);
        var mechData = MechFactoryTests.CreateDummyMechData();
        mechData.Id = Guid.NewGuid();
        var game = CreateClientGame();
        game.JoinGameWithUnits(player, [mechData], []);
        game.SetBattleMap(BattleMapFactory.GenerateMap(2, 2,
            new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        _sut.Game = game;

        game.HandleCommand(new JoinGameCommand
        {
            PlayerId = player.Id,
            Units = [mechData],
            PlayerName = player.Name,
            GameOriginId = Guid.NewGuid(),
            Tint = "#FF0000",
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
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
        unit.Deploy(position, null);
        // Unit starts standing

        _sut.HandleHexSelection(game.BattleMap!.GetHexes().First(h => h.Coordinates == position.Coordinates));

        // Verify we're in MovementState
        var movementState = _sut.CurrentState as MovementState;
        movementState.ShouldNotBeNull();
        _sut.SelectedUnit.ShouldBe(unit);

        // Select Movement Type to initialize _selectedPath
        var action = movementState.GetAvailableActions()
            .First(a => a.Label.StartsWith("Walk"));
        action.OnExecute();

        // Create a MechFallCommand with null DamageData (PSR succeeded, mech did not actually fall)
        var fallCommand = new MechFallCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = unit.Id,
            LevelsFallen = 0,
            WasJumping = false,
            DamageData = null,
            FallPilotingSkillRoll = new PilotingSkillRollData
            {
                RollContext = new PilotingSkillRollContext(PilotingSkillRollType.GyroHit),
                DiceResults = [5, 6],
                IsSuccessful = true,
                PsrBreakdown = new PsrBreakdown
                {
                    BasePilotingSkill = 4,
                    Modifiers = []
                }
            }
        };

        // Act
        game.HandleCommand(fallCommand);

        // Assert
        // Verify that we do NOT have the "StayProne" action, which would indicate the unit is not prone
        _localizationService.GetString("Action_StayProne").Returns("StayProne");
        var actions = movementState.GetAvailableActions();
        actions.ShouldNotContain(a => a.Label.Contains("StayProne"));
    }

    [Fact]
    public void ShowAimedShotLocationSelector_SetsUnitPartSelectorAndVisibility()
    {
        // Arrange
        var target = new Mech("Test Mech", "TM-1", 20, []);
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
        var target = new Mech("Test Mech", "TM-1", 20, []);
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
        var target = new Mech("Test Mech", "TM-1", 20, []);
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
    
    [Fact]
    public void LeaveGameCommand_ShouldPublishPlayerLeftCommand_WhenPromptIsApproved()
    {
        // Arrange
        _localizationService.GetString("Dialog_Yes").Returns("Yes");
        _localizationService.GetString("Dialog_No").Returns("No");
        var playerId = Guid.NewGuid();
        _game.JoinGameWithUnits(new Player(playerId, "Player1", PlayerControlType.Human), [],[]);
        _commandPublisher.ClearReceivedCalls();
        var navigationService = Substitute.For<INavigationService>();
        _sut.SetNavigationService(navigationService);
        navigationService
            .AskForActionAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<UiAction>(), Arg.Any<UiAction>())!
            .Returns(ci =>
            {
                var actions = (UiAction[])ci.Args()[2]; // third argument - actions
                return Task.FromResult(actions[0]); // first action - yes
            });
        
        // Act
        _sut.LeaveGameCommand.Execute(null);
        
        // Assert
        var capturedCommand = (PlayerLeftCommand)_commandPublisher.ReceivedCalls() 
            .First().GetArguments()[0]!;
        
        capturedCommand.PlayerId.ShouldBe(playerId);
        capturedCommand.GameOriginId.ShouldBe(_game.Id);
        
        _game.HandleCommand(capturedCommand with { GameOriginId = Guid.NewGuid() });
    }
    
    [Fact]
    public void LeaveGameCommand_ShouldNotPublishPlayerLeftCommand_WhenPromptIsCancelled()
    {
        // Arrange
        _localizationService.GetString("Dialog_Yes").Returns("Yes");
        _localizationService.GetString("Dialog_No").Returns("No");
        var playerId = Guid.NewGuid();
        _game.JoinGameWithUnits(new Player(playerId, "Player1", PlayerControlType.Human), [],[]);
        _commandPublisher.ClearReceivedCalls();
        var navigationService = Substitute.For<INavigationService>();
        _sut.SetNavigationService(navigationService);
        navigationService
            .AskForActionAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<UiAction>(), Arg.Any<UiAction>())!
            .Returns(ci =>
            {
                var actions = (UiAction[])ci.Args()[2]; // third argument - actions
                return Task.FromResult(actions[1]); // second action - no
            });
        
        // Act
        _sut.LeaveGameCommand.Execute(null);
        
        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<PlayerLeftCommand>());
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
                Range = RangeBracket.Long,
                Distance = 5,
                WeaponName = "Test"
            },
            TerrainModifiers = []
        };
    }

    private ClientGame CreateClientGame()
    {
        return new ClientGame(new TotalWarfareRulesProvider(),
            _mechFactory,
            _commandPublisher,
            Substitute.For<IToHitCalculator>(),
            Substitute.For<IPilotingSkillCalculator>(),
            Substitute.For<IConsciousnessCalculator>(),
            Substitute.For<IHeatEffectsCalculator>(),
            _mapFactory,
            _hashService,
            Substitute.For<ILogger<ClientGame>>());
    }

    private BattleMapViewModel CreateViewModel(ITerrainBitmaskService? terrainBitmaskService = null)
    {
        var dispatcherService = Substitute.For<IDispatcherService>();
        dispatcherService.RunOnUIThread(Arg.InvokeDelegate<Action>());
        dispatcherService.Scheduler.Returns(Scheduler.Immediate);

        return new BattleMapViewModel(
            Substitute.For<IImageService>(),
            Substitute.For<ITerrainAssetService>(),
            _localizationService,
            dispatcherService,
            Substitute.For<IRulesProvider>(),
            Substitute.For<IPlatformService>(),
            terrainBitmaskService);
    }


    [Fact]
    public void DetachHandlers_ShouldStopProcessingCommands()
    {
        // Arrange - verify commands are processed initially
        _sut.CommandLog.ShouldBeEmpty();
        var command = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Test",
            Units = [],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PilotAssignments = []
        };
        _game.HandleCommand(command);
        _sut.CommandLog.ShouldNotBeEmpty();

        // Act
        _sut.DetachHandlers();
        var logCount = _sut.CommandLog.Count;

        // Send another command
        var command2 = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Test2",
            Units = [],
            Tint = "#00FF00",
            GameOriginId = Guid.NewGuid(),
            PilotAssignments = []
        };
        _game.HandleCommand(command2);

        // Assert - command log should not have grown
        _sut.CommandLog.Count.ShouldBe(logCount);
    }

    [Fact]
    public void Dispose_ShouldDisposeGame()
    {
        // Arrange
        _game.IsDisposed.ShouldBeFalse();

        // Act
        _sut.Dispose();

        // Assert
        _game.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task NavigateToEndGame_WithVictoryReason_ShouldInitializeEndGameViewModel()
    {
        // Arrange
        var game = CreateClientGame();
        game.SetBattleMap(BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        _sut.Game = game;
        var navigationService = Substitute.For<INavigationService>();
        var endGameViewModel = new EndGameViewModel(_localizationService);
        navigationService.GetNewViewModel<EndGameViewModel>().Returns(endGameViewModel);
        _sut.SetNavigationService(navigationService);

        var gameEndedCommand = new GameEndedCommand
        {
            GameOriginId = Guid.NewGuid(),
            Reason = GameEndReason.Victory
        };

        // Act
        game.HandleCommand(gameEndedCommand);
        await _sut.NavigateToEndGame();

        // Assert
        // Verify that the EndGameViewModel was initialized with the game
        endGameViewModel.Players.ShouldNotBeNull();
    }
}

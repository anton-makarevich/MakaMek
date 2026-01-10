using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Models.Map;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.Generators;
using Sanet.MakaMek.Presentation.UiStates;
using Sanet.MakaMek.Presentation.ViewModels;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.UiStates;

public class EndStateTests
{
    private readonly EndState _sut;
    private readonly ClientGame _game;
    private readonly IUnit _unit1;
    private readonly Player _player;
    private readonly BattleMapViewModel _battleMapViewModel;
    private readonly ICommandPublisher _commandPublisher = Substitute.For<ICommandPublisher>();
    private readonly IHeatEffectsCalculator _heatEffectsCalculator = Substitute.For<IHeatEffectsCalculator>();
    private readonly IHashService _hashService = Substitute.For<IHashService>();

    public EndStateTests()
    {
        var imageService = Substitute.For<IImageService>();
        var localizationService = Substitute.For<ILocalizationService>();
        
        // Mock localization service responses
        localizationService.GetString("EndPhase_ActionLabel").Returns("End your turn");
        localizationService.GetString("EndPhase_PlayerActionLabel").Returns("End your turn");
        localizationService.GetString("Action_Shutdown").Returns("Shutdown");
        localizationService.GetString("Action_Startup").Returns("Startup");
        
        _battleMapViewModel = new BattleMapViewModel(imageService,
            localizationService,
            Substitute.For<IDispatcherService>(),
            Substitute.For<IRulesProvider>());
        var playerId = Guid.NewGuid();
        
        var rules = new ClassicBattletechRulesProvider();
        var unitData = MechFactoryTests.CreateDummyMechData();
        
        _player = new Player(playerId, "Player1", PlayerControlType.Human);
        
        _game = new ClientGame(rules,
            new MechFactory(
                rules,
                new ClassicBattletechComponentProvider(),
                localizationService),
            _commandPublisher, 
            Substitute.For<IToHitCalculator>(),
            Substitute.For<IPilotingSkillCalculator>(),
            Substitute.For<IConsciousnessCalculator>(),
            _heatEffectsCalculator,
            Substitute.For<IBattleMapFactory>(),
            _hashService,
            Substitute.For<Microsoft.Extensions.Logging.ILogger<ClientGame>>());
        
        var idempotencyKey = Guid.NewGuid();
        _hashService.ComputeCommandIdempotencyKey(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Type>(),
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<Guid?>())
            .Returns(idempotencyKey);
        
        _game.JoinGameWithUnits(_player,[],[]);
        _game.SetBattleMap(BattleMapTests.BattleMapFactory.GenerateMap(2, 2, new SingleTerrainGenerator(2, 2, new ClearTerrain())));
        
        _battleMapViewModel.Game = _game;
        
        _game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player1",
            Units = [unitData],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = _player.Id,
            PilotAssignments = [],
            IdempotencyKey = idempotencyKey
        });
        _unit1 = _battleMapViewModel.Units.First();
    
        SetPhase(PhaseNames.End);
        _sut = new EndState(_battleMapViewModel);
    }
    
    private static LocationHitData CreateHitDataForLocation(PartLocation partLocation,
        int damage,
        int[]? aimedShotRoll = null,
        int[]? locationRoll = null)
    {
        return new LocationHitData(
        [
            new LocationDamageData(partLocation,
                damage-1,
                1,
                false)
        ], aimedShotRoll??[], locationRoll??[], partLocation);
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
    
    [Fact]
    public void IsActionRequired_ShouldBeTrue_WhenActivePlayerAndCanAct()
    {
        // Arrange
        // Act & Assert
        _sut.IsActionRequired.ShouldBeTrue();
    }
    
    [Fact]
    public void IsActionRequired_ShouldBeFalse_WhenNotActivePlayer()
    {
        // Arrange
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(), // Different player
            UnitsToPlay = 0
        });
        
        // Act & Assert
        _sut.IsActionRequired.ShouldBeFalse();
    }
    
    [Fact]
    public void IsActionRequired_ShouldBeFalse_WhenActivePlayerCannotAct()
    {
        // Arrange
        // Make the player unable to act by destroying their unit
        _unit1.ApplyDamage([CreateHitDataForLocation(
            PartLocation.CenterTorso,
            100,
            [],
            [])], HitDirection.Front);
        
        // Act & Assert
        _sut.IsActionRequired.ShouldBeFalse();
    }
    
    [Fact]
    public void CanExecutePlayerAction_ShouldBeFalse_WhenActivePlayerCannotAct()
    {
        // Arrange
        // Make the player unable to act by destroying their unit
        _unit1.ApplyDamage([CreateHitDataForLocation(
            PartLocation.CenterTorso,
            100,
            [],
            [])], HitDirection.Front);
        
        // Act & Assert
        _sut.CanExecutePlayerAction.ShouldBeFalse();
    }

    [Fact]
    public void GetAvailableActions_ShouldReturnShutdownAction_WhenUnitSelectedAndBelongsToActivePlayer()
    {
        // Arrange
        _battleMapViewModel.SelectedUnit = _unit1;

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.ShouldNotBeEmpty();
        actions.ShouldContain(a => a.Label == "Shutdown");
    }

    [Fact]
    public void GetAvailableActions_ShouldNotReturnShutdownAction_WhenNoUnitSelected()
    {
        // Arrange
        _battleMapViewModel.SelectedUnit = null;

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.ShouldBeEmpty();
    }

    [Fact]
    public void GetAvailableActions_ShouldNotReturnShutdownAction_WhenUnitBelongsToOtherPlayer()
    {
        // Arrange
        // Create another player and unit
        var otherPlayer = new Player(Guid.NewGuid(), "Other Player", PlayerControlType.Human);
        _game.HandleCommand(new JoinGameCommand
        {
            PlayerId = otherPlayer.Id,
            PlayerName = otherPlayer.Name,
            GameOriginId = Guid.NewGuid(), 
            Tint = "Blue",
            Units = [MechFactoryTests.CreateDummyMechData()],
            PilotAssignments = []
        });

        var otherUnit = _game.Players.First(p => p.Id == otherPlayer.Id).Units.First();
        _battleMapViewModel.SelectedUnit = otherUnit;

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.ShouldBeEmpty();
    }

    [Fact]
    public void GetAvailableActions_ShouldNotReturnShutdownAction_WhenUnitDestroyed()
    {
        // Arrange
        _unit1.ApplyDamage([CreateHitDataForLocation(
            PartLocation.CenterTorso,
            100,
            [],
            [])], HitDirection.Front);
        _battleMapViewModel.SelectedUnit = _unit1;

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.ShouldBeEmpty();
    }

    [Fact]
    public void GetAvailableActions_ShouldNotReturnShutdownAction_WhenUnitAlreadyShutdown()
    {
        // Arrange
        _unit1.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });
        _battleMapViewModel.SelectedUnit = _unit1;

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.ShouldBeEmpty();
    }

    [Fact]
    public void GetAvailableActions_ShouldNotReturnShutdownAction_WhenNotActivePlayer()
    {
        // Arrange - Clear active player (phase change automatically sets it)
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = Guid.Empty, // No active player
            UnitsToPlay = 0
        });
        _battleMapViewModel.SelectedUnit = _unit1;

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.ShouldBeEmpty();
    }
    
    [Fact]
    public void ExecuteShutdownAction_ShouldPublishShutdownCommand()
    {
        // Arrange
        _battleMapViewModel.SelectedUnit = _unit1;
        var shutdownAction = _sut.GetAvailableActions().First(a => a.Label == "Shutdown");

        // Act
        shutdownAction.OnExecute();
        
        // Assert
        _commandPublisher.Received(1).PublishCommand(Arg.Is<ShutdownUnitCommand>(cmd =>
            cmd.PlayerId == _player.Id &&
            cmd.UnitId == _unit1.Id &&
            cmd.GameOriginId == _game.Id));
    }

    [Fact]
    public void ExecuteShutdownAction_ShouldNotPublishShutdownCommand_WhenNotActivePlayer()
    {
        // Arrange
        _battleMapViewModel.SelectedUnit = _unit1;
        var shutdownAction = _sut.GetAvailableActions().First(a => a.Label == "Shutdown");
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = Guid.Empty, // No active player
            UnitsToPlay = 0
        });

        // Act
        shutdownAction.OnExecute();
        
        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<ShutdownUnitCommand>());
    }
    
    [Fact]
    public void ExecuteStartupAction_ShouldPublishStartupCommand()
    {
        // Arrange
        _battleMapViewModel.SelectedUnit = _unit1;
        _unit1.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = _game.Turn - 1 });

        // Mock heat effects calculator to allow startup
        _heatEffectsCalculator.GetShutdownAvoidNumber(Arg.Any<int>()).Returns(0); 
        var startupAction = _sut.GetAvailableActions().First(a => a.Label == "Startup");

        // Act
        startupAction.OnExecute();
        
        // Assert
        _commandPublisher.Received(1).PublishCommand(Arg.Is<StartupUnitCommand>(cmd =>
            cmd.PlayerId == _player.Id &&
            cmd.UnitId == _unit1.Id &&
            cmd.GameOriginId == _game.Id));
    }
    
    [Fact]
    public void ExecuteStartupAction_ShouldNotPublishStartupCommand_WhenNotActivePlayer()
    {
        // Arrange
        _battleMapViewModel.SelectedUnit = _unit1;
        _unit1.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = _game.Turn - 1 });

        // Mock heat effects calculator to allow startup
        _heatEffectsCalculator.GetShutdownAvoidNumber(Arg.Any<int>()).Returns(0); 
        var startupAction = _sut.GetAvailableActions().First(a => a.Label == "Startup");
        
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = Guid.Empty, // No active player
            UnitsToPlay = 0
        });

        // Act
        startupAction.OnExecute();
        
        // Assert
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<StartupUnitCommand>());
    }
    
    [Fact]
    public void GetAvailableActions_ShouldReturnStartupAction_WhenUnitIsShutdownAndCanStartup()
    {
        // Arrange
        _battleMapViewModel.SelectedUnit = _unit1;

        // Shutdown the unit in a previous turn
        _unit1.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = _game.Turn - 1 });

        // Mock heat effects calculator to allow startup
        _heatEffectsCalculator.GetShutdownAvoidNumber(Arg.Any<int>()).Returns(8); // Possible startup

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.ShouldContain(action => action.Label.Contains("Startup"));
    }

    [Fact]
    public void GetAvailableActions_ShouldReturnStartupActionWithProbability_WhenStartupNotGuaranteed()
    {
        // Arrange
        _battleMapViewModel.SelectedUnit = _unit1;

        // Shutdown the unit in a previous turn
        _unit1.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = _game.Turn - 1 });

        // Mock heat effects calculator to return startup with probability
        _heatEffectsCalculator.GetShutdownAvoidNumber(Arg.Any<int>()).Returns(8); // 42% chance

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        var startupAction = actions.FirstOrDefault(action => action.Label.Contains("Startup"));
        startupAction.ShouldNotBeNull();
        startupAction.Label.ShouldContain("42%");
    }

    [Fact]
    public void GetAvailableActions_ShouldNotReturnStartupAction_WhenUnitNotShutdown()
    {
        // Arrange
        _battleMapViewModel.SelectedUnit = _unit1;
        // Unit is not shutdown

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.ShouldNotContain(action => action.Label.Contains("Startup"));
    }

    [Fact]
    public void GetAvailableActions_ShouldNotReturnStartupAction_WhenShutdownInSameTurn()
    {
        // Arrange
        _battleMapViewModel.SelectedUnit = _unit1;

        // Shutdown the unit in the current turn
        _unit1.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = _game.Turn });

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.ShouldNotContain(action => action.Label.Contains("Startup"));
    }

    [Fact]
    public void GetAvailableActions_ShouldNotReturnStartupAction_WhenHeatTooHigh()
    {
        // Arrange
        _battleMapViewModel.SelectedUnit = _unit1;

        // Shutdown the unit in a previous turn
        _unit1.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = _game.Turn - 1 });

        // Mock heat effects calculator to return an impossible startup
        _heatEffectsCalculator.GetShutdownAvoidNumber(Arg.Any<int>()).Returns(13); // Impossible

        // Act
        var actions = _sut.GetAvailableActions().ToList();

        // Assert
        actions.ShouldNotContain(action => action.Label.Contains("Startup"));
    }

    private void SetPhase(PhaseNames phase)
    {
        _game.HandleCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = phase
        });

        // For EndPhase, also send StartPhaseCommand to complete the two-stage phase transition
        if (phase == PhaseNames.End)
        {
            _game.HandleCommand(new StartPhaseCommand
            {
                GameOriginId = Guid.NewGuid(),
                Phase = phase
            });
        }
    }
}

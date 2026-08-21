using Microsoft.Extensions.Logging;
using System.Text.Json;
using AsyncAwaitBestPractices.MVVM;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Bots.Services;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Core.Data.Game.Players;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Services;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.Transport.SignalR.Client.Relay;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MVVM.Core.Services;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class JoinGameViewModelTests
{
    private readonly JoinGameViewModel _sut;
    private readonly IRulesProvider _rulesProvider = new TotalWarfareRulesProvider();
    private readonly IToHitCalculator _toHitCalculator = Substitute.For<IToHitCalculator>();
    private readonly IPilotingSkillCalculator  _pilotingSkillCalculator = Substitute.For<IPilotingSkillCalculator>();
    private readonly IConsciousnessCalculator _consciousnessCalculator = Substitute.For<IConsciousnessCalculator>();
    private readonly IHeatEffectsCalculator _heatEffectsCalculator = Substitute.For<IHeatEffectsCalculator>();
    private readonly IDispatcherService _dispatcherService = Substitute.For<IDispatcherService>();
    private readonly IGameFactory _gameFactory = Substitute.For<IGameFactory>();
    private readonly ICommandPublisher _commandPublisher = Substitute.For<ICommandPublisher>();
    private readonly IUnitsLoader _unitsLoader = Substitute.For<IUnitsLoader>();
    private readonly IBattleMapFactory _mapFactory = Substitute.For<IBattleMapFactory>();
    private readonly IMechFactory _mechFactory = Substitute.For<IMechFactory>();
    private readonly IFileCachingService _cachingService = Substitute.For<IFileCachingService>();
    private readonly IHashService _hashService = Substitute.For<IHashService>();
    private readonly IBotManager _botManager = Substitute.For<IBotManager>();
    private readonly ILogger<JoinGameViewModel> _logger = Substitute.For<ILogger<JoinGameViewModel>>();
    private readonly IGameConnector _gameConnector = Substitute.For<IGameConnector>();
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IClipboardService _clipboardService = Substitute.For<IClipboardService>();
    private readonly IMapPreviewRenderer _mapPreviewRenderer = Substitute.For<IMapPreviewRenderer>();
    private readonly ClientGame _clientGame;
    private bool _connectorConnected;
    private static readonly IBattleMapFactory BattleMapFactory = new BattleMapFactory();

    public JoinGameViewModelTests()
    {
        _unitsLoader.LoadUnits().Returns([MechFactoryTests.CreateDummyMechData()]);
        _clientGame = new ClientGame(_rulesProvider,
            _mechFactory,
            _commandPublisher, 
            _toHitCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _mapFactory,
            _hashService,
            Substitute.For<ILogger<ClientGame>>());

        _gameConnector.IsConnected.Returns(_ => _connectorConnected);
        _gameConnector.When(c => c.Disconnect(Arg.Any<CancellationToken>()))
            .Do(_ => _connectorConnected = false);
            
        // Configure the game factory to return our mock client game
        _gameFactory.CreateClientGame(_commandPublisher).Returns(_clientGame);
        
        // Configure dispatcher to execute actions immediately
        _dispatcherService.RunOnUIThread(Arg.InvokeDelegate<Func<Task>>());

        _cachingService.TryGetCachedFile(Arg.Any<string>()).Returns(Task.FromResult<byte[]?>(null));

        _localizationService.GetString("Join_Connecting").Returns("Connecting...");
        _localizationService.GetString("Join_Failed").Returns("Failed to join");
        _localizationService.GetString("Join_InvalidCode").Returns("Invalid code");
        _localizationService.GetString("Join_RoomExpired").Returns("Room expired");
        _localizationService.GetString("Join_HostNotReady").Returns("Host not ready");
        _localizationService.GetString("Join_RoomFull").Returns("Room full");
        _localizationService.GetString("Join_HubAtCapacity").Returns("Hub at capacity");
        _localizationService.GetString("Join_RateLimited").Returns("Rate limited");
        _localizationService.GetString("Join_ConnectionFailed").Returns("Connection failed");
        _localizationService.GetString("Join_ConfigurationError").Returns("Configuration error");
        _localizationService.GetString("Join_RoomJoinedInfo").Returns("Room: {0}");
        _localizationService.GetString("Join_ServerConnectedInfo").Returns("Server: {0}");

        _clipboardService.GetText().Returns(Task.FromResult<string?>(null));

        _sut = CreateSut();
    }

    private void EnableLanConnect() =>
        _gameConnector.When(c => c.ConnectToLan(Arg.Any<string>()))
            .Do(_ => _connectorConnected = true);

    private void EnableOnlineJoin() =>
        _gameConnector.When(c => c.JoinOnline(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()))
            .Do(_ => _connectorConnected = true);

    private JoinGameViewModel CreateSut(ILocalizationService? localizationService = null)
    {
        var sut = new JoinGameViewModel(
            _unitsLoader,
            _commandPublisher,
            _dispatcherService,
            _gameFactory,
            _gameConnector,
            _cachingService,
            _botManager,
            _logger,
            _mechFactory,
            _mapFactory,
            _mapPreviewRenderer,
            _clipboardService,
            localizationService ?? _localizationService);
        sut.AttachHandlers();
        return sut;
    }

    private void ConnectAndAckLobby()
    {
        EnableLanConnect();
        _sut.ConnectCommand.Execute(null);
        var lobbyCommand = (RequestGameLobbyStatusCommand)_commandPublisher.ReceivedCalls().Last().GetArguments()[0]!;
        _clientGame.HandleCommand(lobbyCommand with { GameOriginId = Guid.NewGuid() });
    }

    private void VerifyLogged(LogLevel level, Func<object, bool> statePredicate, Exception? expectedException)
    {
        _logger.Received(1).Log(
            level,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => statePredicate(state!)),
            expectedException == null
                ? Arg.Is<Exception?>(e => e == null)
                : Arg.Is<Exception>(e =>
                    expectedException.GetType().IsInstanceOfType(e) && e.Message == expectedException.Message),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task ConnectToServer_DelegatesToConnector()
    {
        // Arrange
        _sut.ServerIp = "127.0.0.1";
        EnableLanConnect();

        // Act
        _sut.ConnectCommand.Execute(null);

        // Assert
        await _gameConnector.Received(1).ConnectToLan(_sut.ServerAddress);
    }

    [Fact]
    public async Task ConnectToServer_RequestsLobbyStatus()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000"; // Set a valid server address
        
        // Act
        ConnectAndAckLobby();
        
        // Assert
        var lobbyCommand = (RequestGameLobbyStatusCommand)_commandPublisher.ReceivedCalls()
            .Last().GetArguments()[0]!;
        lobbyCommand.GameOriginId.ShouldBe(_clientGame.Id);
        _clientGame.HandleCommand(lobbyCommand with { GameOriginId = Guid.NewGuid() });
    }
    
    [Fact]
    public void ConnectToServer_SetsIsConnectedToTrue_OnSuccess()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000"; // Set a valid server address
        
        // Act
        ConnectAndAckLobby();
        
        // Assert
        _sut.IsConnected.ShouldBeTrue();
        _sut.CanPublishCommands.ShouldBeTrue();
    }
    
    [Fact]
    public void ConnectToServer_DoesNotCreateGame_WhenConnectionFails()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000"; // Set a valid server address
        _commandPublisher.ClearReceivedCalls(); // ClientGame construction subscribes via BaseGame
        
        // Act - the connector stays disconnected
        _sut.ConnectCommand.Execute(null);
        
        // Assert
        _sut.IsConnected.ShouldBeFalse();
        _gameFactory.DidNotReceive().CreateClientGame(Arg.Any<ICommandPublisher>());
        _commandPublisher.DidNotReceive().Subscribe(Arg.Any<Action<IGameCommand>>());
    }
    
    [Fact]
    public void ConnectCommand_CreatesClientGame_WhenLocalGameIsNull()
    {
        // Arrange
        _sut.ServerIp="127.0.0.1";
        // Act
        ConnectAndAckLobby();
        
        // Assert
        _gameFactory.Received(1).CreateClientGame(_commandPublisher);
    }

    [Fact]
    public async Task Disconnect_ShouldDisposeLocalGame()
    {
        // Arrange
        _sut.ServerIp = "127.0.0.1";
        ConnectAndAckLobby();
        _clientGame.IsDisposed.ShouldBeFalse();

        // Act
        await _sut.Disconnect();

        // Assert
        _clientGame.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task Disconnect_DelegatesToConnector()
    {
        // Arrange
        _sut.ServerIp = "127.0.0.1";
        ConnectAndAckLobby();
        _gameConnector.ClearReceivedCalls();

        // Act
        await _sut.Disconnect();

        // Assert
        await _gameConnector.Received(1).Disconnect(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Disconnect_ShouldSetIsConnectedToFalse()
    {
        // Arrange
        _sut.ServerIp = "127.0.0.1";
        ConnectAndAckLobby();
        _sut.IsConnected.ShouldBeTrue();

        // Act
        await _sut.Disconnect();

        // Assert
        _sut.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public async Task DisposeAsync_DisposesGameAndConnector()
    {
        // Arrange
        _sut.ServerIp = "127.0.0.1";
        ConnectAndAckLobby();
        _clientGame.IsDisposed.ShouldBeFalse();

        // Act
        await _sut.DisposeAsync();

        // Assert
        _clientGame.IsDisposed.ShouldBeTrue();
        await _gameConnector.Received(1).DisposeAsync();
        _sut.IsConnected.ShouldBeFalse();
    }
    
    [Fact]
    public void CanAddPlayer_ReturnsFalse_WhenNotConnected()
    {
        // Arrange
        _sut.IsConnected.ShouldBeFalse(); // Default state
        
        // Assert
        _sut.CanAddPlayer.ShouldBeFalse();
    }
    
    [Fact]
    public void CanAddPlayer_ReturnsTrue_WhenConnectedAndLessThanFourPlayers()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000";
        ConnectAndAckLobby();
        
        // Assert
        _sut.CanAddPlayer.ShouldBeTrue();
    }
    
    [Fact]
    public async Task CanAddPlayer_ReturnsFalse_WhenConnectedButFourPlayersExist()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000";
        ConnectAndAckLobby();
        
        // Add 4 players
        for (var i = 0; i < 4; i++)
        {
            await ((AsyncCommand)_sut.AddPlayerCommand!).ExecuteAsync();
        }
        
        // Assert
        _sut.CanAddPlayer.ShouldBeFalse();
    }
    
    [Fact]
    public void CanConnect_ReturnsTrue_WhenServerAddressIsSetAndNotConnected()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000";
        
        // Assert
        _sut.CanConnect.ShouldBeTrue();
    }
    
    [Fact]
    public void CanConnect_ReturnsFalse_WhenServerAddressIsEmpty()
    {
        // Arrange
        _sut.ServerIp = "";
        
        // Assert
        _sut.CanConnect.ShouldBeFalse();
    }
    
    [Fact]
    public void CanConnect_ReturnsFalse_WhenAlreadyConnected()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000";
        ConnectAndAckLobby();
        
        // Assert
        _sut.CanConnect.ShouldBeFalse();
    }
    
    [Fact]
    public async Task HandleCommandInternal_UpdatePlayerStatusCommand_UpdatesPlayerStatus()
    {
        // Connect and add a player
        _sut.ServerIp = "http://localhost:5000";
        ConnectAndAckLobby();
        await ((AsyncCommand)_sut.AddPlayerCommand!).ExecuteAsync();
        
        var player = _sut.Players.First();
        var playerId = player.Player.Id;
        
        // Create status update command
        var statusCommand = new UpdatePlayerStatusCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            PlayerStatus = PlayerStatus.Ready
        };
        
        // Act - call the method through HandleServerCommand which will call HandleCommandInternal
        _sut.HandleServerCommand(statusCommand);
        
        // Assert
        player.Player.Status.ShouldBe(PlayerStatus.Ready);
    }
    
    [Fact]
    public void HandleCommandInternal_JoinGameCommand_AddsNewRemotePlayer()
    {
        // Connect
        _sut.ServerIp = "http://localhost:5000";
        ConnectAndAckLobby();
        
        // Create join command for a new remote player
        var remotePlayerId = Guid.NewGuid();
        var joinCommand = new JoinGameCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = remotePlayerId,
            PlayerName = "Remote Player",
            Units = [MechFactoryTests.CreateDummyMechData()],
            Tint = "#FFFFFF",
            PilotAssignments = []
        };

        // Act
        _sut.HandleServerCommand(joinCommand);

        // Assert
        _sut.Players.Count.ShouldBe(1 + 1); // default + joined
        var remotePlayer = _sut.Players.FirstOrDefault(p => p.Player.Id == remotePlayerId);
        remotePlayer.ShouldNotBeNull();
        remotePlayer.IsLocalPlayer.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleCommandInternal_JoinGameCommand_RemotePlayerJoinCallback_IsNoOpAndDoesNotThrow()
    {
        // Arrange - add a remote player via join command
        var remotePlayerId = Guid.NewGuid();
        var joinCommand = new JoinGameCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = remotePlayerId,
            PlayerName = "Remote Player",
            Units = [MechFactoryTests.CreateDummyMechData()],
            Tint = "#FFFFFF",
            PilotAssignments = []
        };
        _sut.HandleServerCommand(joinCommand);

        var remotePlayer = _sut.Players.First(p => p.Player.Id == remotePlayerId);
        remotePlayer.IsLocalPlayer.ShouldBeFalse();

        // The remote player is registered with a no-op join callback (remote players don't publish join).
        // The callback is not reachable through CanJoin (requires IsLocalPlayer), so invoke it directly.
        var joinActionField = typeof(PlayerViewModel).GetField("_joinGameAction",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var joinAction = (Action<PlayerViewModel>?)joinActionField!.GetValue(remotePlayer);
        joinAction.ShouldNotBeNull();

        // Clear recorded calls so we only observe the callback's own behavior
        _commandPublisher.ClearReceivedCalls();

        // Act & Assert - the no-op callback is invoked without throwing and publishes no command
        Should.NotThrow(() => joinAction.Invoke(remotePlayer));
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<IGameCommand>());
    }
    
    [Fact]
    public void HandleCommandInternal_JoinGameCommand_UpdatesExistingLocalPlayer()
    {
        // Connect and add a player
        _sut.ServerIp = "http://localhost:5000";
        ConnectAndAckLobby();
        
        var player = _sut.Players.First();
        var playerId = player.Player.Id;

        // Create join command for the existing local player
        var joinCommand = new JoinGameCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            PlayerName = player.Player.Name,
            Units = [MechFactoryTests.CreateDummyMechData()],
            Tint = "#FFFFFF",
            PilotAssignments = []
        };

        // Act
        _sut.HandleServerCommand(joinCommand);

        // Assert
        _sut.Players.Count.ShouldBe(1); // No new player added
        player.Player.Status.ShouldBe(PlayerStatus.Joined);
    }

    [Fact]
    public async Task HandleCommandInternal_SetBattleMapCommand_SetsLobbyMapPreview_AndDoesNotNavigate()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000";
        ConnectAndAckLobby();
        var navigationService = Substitute.For<INavigationService>();
        _sut.SetNavigationService(navigationService);
        var battleMap = BattleMapFactory.GenerateMap(2, 2,
            new SingleTerrainGenerator(2, 2, new ClearTerrain()));
        _mapFactory.CreateFromData(Arg.Any<BattleMapData>()).Returns(battleMap);

        // Act
        _sut.HandleServerCommand(new SetBattleMapCommand
        {
            GameOriginId = Guid.NewGuid(),
            MapData = new BattleMapData { HexData = [] }
        });

        // Assert - the map is stored as a preview, no navigation happens
        _sut.PreviewMap.ShouldBe(battleMap);
        _sut.HasLobbyMapPreview.ShouldBeTrue();
        navigationService.DidNotReceive().GetViewModel<BattleMapViewModel>();
        await navigationService.DidNotReceive().NavigateToViewModelAsync(Arg.Any<BattleMapViewModel>());
    }

    [Fact]
    public async Task HandleCommandInternal_SetBattleMapCommand_GeneratesPreviewImage()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000";
        ConnectAndAckLobby();
        var battleMap = BattleMapFactory.GenerateMap(2, 2,
            new SingleTerrainGenerator(2, 2, new ClearTerrain()));
        _mapFactory.CreateFromData(Arg.Any<BattleMapData>()).Returns(battleMap);
        var previewImage = new object();
        _mapPreviewRenderer.GeneratePreview(battleMap, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<object?>(previewImage));

        // Act
        _sut.HandleServerCommand(new SetBattleMapCommand
        {
            GameOriginId = Guid.NewGuid(),
            MapData = new BattleMapData { HexData = [] }
        });

        // Assert
        _sut.PreviewMap.ShouldBe(battleMap);
        _sut.PreviewImage.ShouldBe(previewImage);
        await _mapPreviewRenderer.Received(1)
            .GeneratePreview(battleMap, Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateLobbyMapPreview_ClearsPreviewImage_WhenReplacementPreviewIsNull()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000";
        ConnectAndAckLobby();
        var firstMap = BattleMapFactory.GenerateMap(2, 2,
            new SingleTerrainGenerator(2, 2, new ClearTerrain()));
        var secondMap = BattleMapFactory.GenerateMap(3, 3,
            new SingleTerrainGenerator(3, 3, new ClearTerrain()));
        _mapFactory.CreateFromData(Arg.Any<BattleMapData>()).Returns(firstMap, secondMap);

        var firstImage = Substitute.For<IDisposable>();
        _mapPreviewRenderer.GeneratePreview(firstMap, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<object?>(firstImage));
        // The replacement render returns null (e.g. renderer couldn't produce an image)
        _mapPreviewRenderer.GeneratePreview(secondMap, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<object?>(null));

        // Act - first map produces a completed preview image
        _sut.HandleServerCommand(new SetBattleMapCommand
        {
            GameOriginId = Guid.NewGuid(),
            MapData = new BattleMapData { HexData = [] }
        });
        _sut.PreviewImage.ShouldBe(firstImage);

        // A second map arrives whose GeneratePreview result is null
        _sut.HandleServerCommand(new SetBattleMapCommand
        {
            GameOriginId = Guid.NewGuid(),
            MapData = new BattleMapData { HexData = [] }
        });

        // Assert - the stale preview is cleared and disposed; no image lingers for the new map
        _sut.PreviewImage.ShouldBeNull();
        _sut.PreviewMap.ShouldBe(secondMap);
        firstImage.Received(1).Dispose();
        await _mapPreviewRenderer.Received(1)
            .GeneratePreview(secondMap, Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleCommandInternal_ChangePhaseCommand_NavigatesToBattleMap_WhenLeavingStart()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000";
        ConnectAndAckLobby();
        var navigationService = Substitute.For<INavigationService>();
        var localizationService = Substitute.For<ILocalizationService>();
        var imageService = Substitute.For<IImageService>();
        var battleMapViewModel = new BattleMapViewModel(imageService,
            Substitute.For<ITerrainAssetService>(),
            localizationService,
            Substitute.For<IDispatcherService>(),
            Substitute.For<IRulesProvider>(),
            Substitute.For<IPlatformService>());
        navigationService.GetViewModel<BattleMapViewModel>()
            .Returns(battleMapViewModel);

        _sut.SetNavigationService(navigationService);

        // Act
        _sut.HandleServerCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.Deployment
        });

        // Assert
        navigationService.Received(1).GetViewModel<BattleMapViewModel>();
        battleMapViewModel.Game.ShouldBe(_clientGame);
        await navigationService.Received(1).NavigateToViewModelAsync(battleMapViewModel);
    }

    [Fact]
    public async Task HandleCommandInternal_ChangePhaseCommand_UnsubscribesFromGameCommands_WhenNavigating()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000";
        ConnectAndAckLobby();
        var navigationService = Substitute.For<INavigationService>();
        var localizationService = Substitute.For<ILocalizationService>();
        var imageService = Substitute.For<IImageService>();
        var battleMapViewModel = new BattleMapViewModel(imageService,
            Substitute.For<ITerrainAssetService>(),
            localizationService,
            Substitute.For<IDispatcherService>(),
            Substitute.For<IRulesProvider>(),
            Substitute.For<IPlatformService>());
        navigationService.GetViewModel<BattleMapViewModel>()
            .Returns(battleMapViewModel);

        _sut.SetNavigationService(navigationService);

        // Act
        _sut.HandleServerCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.Deployment
        });

        // Assert - the lobby VM stops listening to game commands so later phase changes
        // cannot re-navigate and detach the battle map subscriptions
        _commandPublisher.Received(1).Unsubscribe(_sut.HandleServerCommand);
        await navigationService.Received(1).NavigateToViewModelAsync(battleMapViewModel);
    }

    [Fact]
    public void HandleCommandInternal_ChangePhaseCommand_DoesNotNavigate_WhenEnteringStart()
    {
        // Arrange
        var navigationService = Substitute.For<INavigationService>();
        _sut.SetNavigationService(navigationService);

        // Act
        _sut.HandleServerCommand(new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.Start
        });

        // Assert
        navigationService.DidNotReceive().GetViewModel<BattleMapViewModel>();
        navigationService.DidNotReceive().NavigateToViewModelAsync(Arg.Any<BattleMapViewModel>());
    }

    [Fact]
    public async Task HandleCommandInternal_ChangePhaseCommand_Throws_WhenBattleMapViewModelNotRegistered()
    {
        // Arrange
        var navigationService = Substitute.For<INavigationService>();
        navigationService.GetViewModel<BattleMapViewModel>().Returns((BattleMapViewModel?)null);
        _sut.SetNavigationService(navigationService);
        var command = new ChangePhaseCommand
        {
            GameOriginId = Guid.NewGuid(),
            Phase = PhaseNames.Deployment
        };
        var method = typeof(JoinGameViewModel).GetMethod("HandleCommandInternal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        var task = (Task)method!.Invoke(_sut, [command])!;

        // Assert
        var exception = await Should.ThrowAsync<Exception>(() => task);
        exception.Message.ShouldBe("BattleMapViewModel is not registered");
    }

    [Fact]
    public void ServerAddress_ShouldIncludeIP_AndPort()
    {
        const string ip = "127.0.0.1";
        const int port = 2439;
        const string hub = "makamekhub";

        _sut.ServerIp = ip;

        _sut.ServerAddress.ShouldBe($"http://{ip}:{port}/{hub}");
    }

    [Fact]
    public void AddDefaultPlayer_ShouldAddDefaultPlayer()
    {
        // Assert - default player should be added automatically when AttachHandlers is called
        _sut.Players.Count.ShouldBe(1);
        _sut.Players[0].Player.Name.ShouldStartWith("Player");
        _sut.Players[0].Player.Tint.ShouldNotBeNullOrEmpty();
        _sut.Players[0].Player.ControlType.ShouldBe(PlayerControlType.Human);
    }

    [Fact]
    public void AttachHandlers_WhenClipboardContainsValidRoomCode_AutoFillsRoomCode()
    {
        // Arrange
        _clipboardService.GetText().Returns("abcdef");

        // Act
        _sut.AttachHandlers();

        // Assert
        _sut.RoomCode.ShouldBe("ABCDEF");
        _sut.IsOnlineMode.ShouldBeTrue();
    }

    [Fact]
    public void AttachHandlers_WhenRoomCodeAlreadySet_DoesNotOverwriteWithClipboardCandidate()
    {
        // Arrange
        _clipboardService.GetText().Returns("abcdef");
        _sut.RoomCode = "GHIJKL";

        // Act
        _sut.AttachHandlers();

        // Assert - the pre-filled room code must be preserved
        _sut.RoomCode.ShouldBe("GHIJKL");
    }

    [Fact]
    public void AttachHandlers_WhenClipboardContainsInvalidText_DoesNotAutoFillRoomCode()
    {
        // Arrange
        _clipboardService.GetText().Returns("not a room code");

        // Act
        _sut.AttachHandlers();

        // Assert
        _sut.RoomCode.ShouldBeEmpty();
        _sut.IsOnlineMode.ShouldBeFalse();
    }

    [Fact]
    public void AttachHandlers_WhenClipboardContainsNoText_DoesNotAutoFillRoomCode()
    {
        // Arrange
        _clipboardService.GetText().Returns(Task.FromResult<string?>(null));

        // Act
        _sut.AttachHandlers();

        // Assert
        _sut.RoomCode.ShouldBeEmpty();
        _sut.IsOnlineMode.ShouldBeFalse();
    }

    [Fact]
    public void AttachHandlers_WhenClipboardReadIsDelayed_DoesNotOverwriteUserState()
    {
        // Arrange
        var clipboardTcs = new TaskCompletionSource<string?>();
        _clipboardService.GetText().Returns(clipboardTcs.Task);

        // Act - start the async clipboard read, then change join state before it completes
        _sut.AttachHandlers();
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "GHIJKL";
        clipboardTcs.SetResult("abcdef");

        // Assert - the stale clipboard result must not overwrite the user's state
        _sut.RoomCode.ShouldBe("GHIJKL");
        _sut.IsOnlineMode.ShouldBeTrue();
    }

    [Fact]
    public void AttachHandlers_WhenClipboardReadIsDelayed_AfterSuccessfulLanConnection_DoesNotApplyRoomCode()
    {
        // Arrange
        EnableLanConnect();
        var clipboardTcs = new TaskCompletionSource<string?>();
        _clipboardService.GetText().Returns(clipboardTcs.Task);

        // Act - start the async clipboard read, then connect to a LAN server before it completes
        _sut.AttachHandlers();
        _sut.ServerIp = "127.0.0.1";
        _sut.ConnectCommand.Execute(null);

        // Assert - the LAN connection succeeded while the read was still pending
        _sut.IsConnected.ShouldBeTrue();

        clipboardTcs.SetResult("abcdef");

        // Assert - the stale clipboard result must not be applied to the active session
        _sut.IsOnlineMode.ShouldBeFalse();
        _sut.RoomCode.ShouldBeEmpty();
    }

    [Fact]
    public void AddDefaultPlayer_ShouldSavePlayerToCache()
    {
        // Assert - cache should be called when default player is added
        _cachingService.Received().SaveToCache("DefaultPlayer", Arg.Any<byte[]>());
    }

    [Fact]
    public void OnDefaultPlayerNameChanged_ShouldInvokeCallback()
    {
        // Arrange
        var defaultPlayerData = PlayerData.CreateDefault() with { Name = "Cached Player" };
        var cachingService = Substitute.For<IFileCachingService>();
        cachingService.TryGetCachedFile("DefaultPlayer")
            .Returns(JsonSerializer.SerializeToUtf8Bytes(defaultPlayerData));

        var sut = new JoinGameViewModel(
            _unitsLoader,
            _commandPublisher,
            _dispatcherService,
            _gameFactory,
            _gameConnector,
            cachingService,
            _botManager,
            _logger,
            _mechFactory,
            _mapFactory,
            _mapPreviewRenderer,
            _clipboardService);
        sut.AttachHandlers();

        // Act
        sut.Players.First().SaveName();

        // Assert - callback should be invoked (at least once for the name change)
        // Note: We can't reliably test the exact number of calls due to async fire-and-forget pattern
        cachingService.Received().SaveToCache("DefaultPlayer", Arg.Any<byte[]>());
    }

    [Fact]
    public void CanAddPlayer_ShouldBeTrue_WhenNotConnectedAndNoPlayers()
    {
        // Arrange - create a fresh instance without calling AttachHandlers
        var cachingService = Substitute.For<IFileCachingService>();
        cachingService.TryGetCachedFile(Arg.Any<string>()).Returns(Task.FromResult<byte[]?>(null));

        var sut = new JoinGameViewModel(
            _unitsLoader,
            _commandPublisher,
            _dispatcherService,
            _gameFactory,
            _gameConnector,
            cachingService,
            _botManager,
            _logger,
            _mechFactory,
            _mapFactory,
            _mapPreviewRenderer,
            _clipboardService);

        // Assert - should be able to add default player even when not connected
        sut.IsConnected.ShouldBeFalse();
        sut.Players.Count.ShouldBe(0);
        sut.CanAddPlayer.ShouldBeTrue();
    }
    
    [Fact]
    public async Task ConnectCommand_ChangesPlayersCanJoinState_WhenConnected()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000";
        _sut.IsConnected.ShouldBeFalse();
        var player = _sut.Players[0];
        await player.AddUnit(MechFactoryTests.CreateDummyMechData());
        player.CanJoin.ShouldBeFalse();
        
        // Act
        ConnectAndAckLobby();
        
        // Assert
        _sut.IsConnected.ShouldBeTrue();
        _sut.Players.First().CanJoin.ShouldBeTrue();
    }

    [Fact]
    public async Task AddBotCommand_ShouldAddBotPlayer_WhenConnected()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000";
        ConnectAndAckLobby();
        var initialPlayerCount = _sut.Players.Count;

        // Act
        await ((AsyncCommand)_sut.AddBotCommand!).ExecuteAsync();

        // Assert
        _sut.Players.Count.ShouldBe(initialPlayerCount + 1);
        _sut.Players.Last().Player.ControlType.ShouldBe(PlayerControlType.Bot);
        _sut.CanAddPlayer.ShouldBeTrue();
    }

    [Fact]
    public async Task PublishJoinCommand_ForBotPlayer_AddsBot_ToBotManager()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000";
        ConnectAndAckLobby();

        // Add a bot player using AddBotCommand
        await ((AsyncCommand)_sut.AddBotCommand!).ExecuteAsync();
        var botPlayerVm = _sut.Players.Last();
        await botPlayerVm.AddUnit(_sut.AvailableUnits.First());

        // Verify the player is a bot
        botPlayerVm.Player.ControlType.ShouldBe(PlayerControlType.Bot);

        // Act
        botPlayerVm.JoinGameCommand.Execute(null);

        // Assert
        _botManager.Received(1).AddBot(botPlayerVm.Player);
    }

    [Fact]
    public void ConnectToServer_DisposesPreviousGame_WhenReconnectingAfterFailedAttempt()
    {
        // Arrange
        _sut.ServerIp = "127.0.0.1";

        var initializeCallCount = 0;
        _botManager.When(b => b.Initialize(Arg.Any<ClientGame>(), Arg.Any<DecisionEngineProvider>()))
            .Do(_ =>
            {
                initializeCallCount++;
                if (initializeCallCount == 1)
                    throw new Exception("Simulated initialization failure");
            });

        // Act - first call creates _localGame but fails during initialization
        EnableLanConnect();
        _sut.ConnectCommand.Execute(null);

        // First attempt left _localGame set but not disposed and IsConnected=false
        _clientGame.IsDisposed.ShouldBeFalse();
        _sut.IsConnected.ShouldBeFalse();

        _gameFactory.ClearReceivedCalls();

        // Act - second call disposes old, creates new
        _sut.ConnectCommand.Execute(null);

        // Assert
        _clientGame.IsDisposed.ShouldBeTrue(); // disposed by CreateAndInitializeLocalGame
        _gameFactory.Received(1).CreateClientGame(_commandPublisher);
    }

    [Fact]
    public async Task PublishJoinCommand_ForHumanPlayer_DoesNotAddBot_ToBotManager()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000";
        ConnectAndAckLobby();

        // Add a human player using AddPlayerCommand
        await ((AsyncCommand)_sut.AddPlayerCommand!).ExecuteAsync();
        var humanPlayerVm = _sut.Players.Last();
        await humanPlayerVm.AddUnit(_sut.AvailableUnits.First());

        // Verify the player is human
        humanPlayerVm.Player.ControlType.ShouldBe(PlayerControlType.Human);

        // Act
        humanPlayerVm.JoinGameCommand.Execute(null);

        // Assert
        _botManager.DidNotReceive().AddBot(Arg.Any<IPlayer>());
    }

    // ---------- Online (relay) join flow ----------

    [Fact]
    public void JoinMode_DefaultValue_IsLan()
    {
        _sut.IsLanMode.ShouldBeTrue();
        _sut.IsOnlineMode.ShouldBeFalse();
    }

    [Fact]
    public void CanJoin_ReturnsTrue_WhenRoomCodeIsSetAndNotConnected()
    {
        _sut.RoomCode = "ABCDEF";

        _sut.CanJoin.ShouldBeTrue();
    }

    [Fact]
    public void CanJoin_ReturnsFalse_WhenRoomCodeIsEmpty()
    {
        _sut.RoomCode = "";

        _sut.CanJoin.ShouldBeFalse();
    }

    [Fact]
    public async Task JoinRoom_Success_ConnectsAndCreatesGame()
    {
        // Arrange
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        EnableOnlineJoin();

        _commandPublisher.ClearReceivedCalls();
        _gameFactory.ClearReceivedCalls();
        _botManager.ClearReceivedCalls();

        // Act
        await ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Assert
        await _gameConnector.Received(1).JoinOnline("ABCDEF", null, Arg.Any<CancellationToken>());
        _commandPublisher.Received(1).Subscribe(Arg.Any<Action<IGameCommand>>());
        _gameFactory.Received(1).CreateClientGame(_commandPublisher);
        _botManager.Received(1).Initialize(_clientGame, Arg.Any<DecisionEngineProvider>());
        _commandPublisher.Received(1).PublishCommand(Arg.Is<RequestGameLobbyStatusCommand>(c => c.GameOriginId == _clientGame.Id));
        _sut.IsConnected.ShouldBeTrue();
        _sut.JoinError.ShouldBeNull();
        _sut.JoinStatusText.ShouldBeEmpty();
        _sut.CanJoin.ShouldBeFalse();
    }

    [Theory]
    [InlineData(RelayClientErrorCode.RoomNotFound, "Join_InvalidCode")]
    [InlineData(RelayClientErrorCode.RoomExpired, "Join_RoomExpired")]
    [InlineData(RelayClientErrorCode.HostNotReady, "Join_HostNotReady")]
    [InlineData(RelayClientErrorCode.RoomFull, "Join_RoomFull")]
    [InlineData(RelayClientErrorCode.HubAtCapacity, "Join_HubAtCapacity")]
    [InlineData(RelayClientErrorCode.RateLimited, "Join_RateLimited")]
    [InlineData(RelayClientErrorCode.NetworkError, "Join_ConnectionFailed")]
    [InlineData(RelayClientErrorCode.Timeout, "Join_ConnectionFailed")]
    [InlineData(RelayClientErrorCode.ConfigurationError, "Join_ConfigurationError")]
    [InlineData(RelayClientErrorCode.Unknown, "Join_Failed")]
    public async Task JoinRoom_WithJoinError_ShowsLocalizedErrorAndDoesNotConnect(RelayClientErrorCode code, string expectedKey)
    {
        // Arrange - the connector reports a failed join without connecting
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        _gameConnector.JoinOnline("ABCDEF", null, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _gameConnector.OnlineError.Returns(new RelayClientError(code, "unused"));

        _commandPublisher.ClearReceivedCalls();
        _gameFactory.ClearReceivedCalls();

        // Act
        await ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Assert
        var expectedText = _localizationService.GetString(expectedKey);
        _sut.JoinError.ShouldBe(expectedText);
        _sut.JoinStatusText.ShouldBe(expectedText);
        _sut.IsConnected.ShouldBeFalse();
        _sut.CanJoin.ShouldBeTrue();
        _gameFactory.DidNotReceive().CreateClientGame(_commandPublisher);
        _commandPublisher.DidNotReceive().Subscribe(Arg.Any<Action<IGameCommand>>());
        _sut.JoinError!.ShouldNotContain("ABCDEF");
        _sut.JoinStatusText.ShouldNotContain("ABCDEF");
    }

    [Fact]
    public async Task JoinRoom_WhenCancelled_LogsAtInfoLevelAndDoesNotConnect()
    {
        // Arrange
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        _gameConnector.JoinOnline(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Throws(new OperationCanceledException());

        _commandPublisher.ClearReceivedCalls();
        _gameFactory.ClearReceivedCalls();

        // Act
        await ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Assert - cancellation is expected, so only an info-level log is emitted and no connection is made
        _sut.IsConnected.ShouldBeFalse();
        _sut.JoinError.ShouldBeNull();
        _sut.CanJoin.ShouldBeTrue();
        _gameFactory.DidNotReceive().CreateClientGame(_commandPublisher);
        VerifyLogged(
            LogLevel.Information,
            state => state.ToString()!.Contains("cancelled"),
            null);
    }

    [Fact]
    public async Task JoinRoom_WhenPostCreationInitializationFails_DisconnectsAndShowsFailure()
    {
        // Arrange - the connector connects, but initializing the local game fails
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        EnableOnlineJoin();
        _botManager.When(b => b.Initialize(Arg.Any<ClientGame>(), Arg.Any<DecisionEngineProvider>()))
            .Do(_ => throw new Exception("Simulated initialization failure"));

        _commandPublisher.ClearReceivedCalls();
        _gameConnector.ClearReceivedCalls();

        // Act
        await ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Assert - the connector is torn down, the subscription is removed and a generic failure is shown
        _commandPublisher.Received(1).Unsubscribe(_sut.HandleServerCommand);
        await _gameConnector.Received(1).Disconnect(Arg.Any<CancellationToken>());
        _sut.IsConnected.ShouldBeFalse();
        _sut.JoinError.ShouldBe(_localizationService.GetString("Join_Failed"));
        VerifyLogged(
            LogLevel.Error,
            state => state.ToString()!.Contains("Error joining online game"),
            new Exception("Simulated initialization failure"));
    }

    [Fact]
    public async Task JoinRoom_UsesNormalizedRoomCode()
    {
        // Arrange
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "  abcdef  ";
        _gameConnector.JoinOnline("ABCDEF", null, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _gameConnector.OnlineError.Returns(new RelayClientError(RelayClientErrorCode.RoomNotFound, "unused"));

        // Act
        await ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Assert
        await _gameConnector.Received(1).JoinOnline("ABCDEF", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchingJoinMode_DuringJoin_CancelsActiveJoinToken_AndDoesNotConnect()
    {
        // Arrange
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        var joinTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken capturedToken = default;
        _gameConnector.JoinOnline(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedToken = ci.Arg<CancellationToken>();
                return joinTcs.Task;
            });

        _commandPublisher.ClearReceivedCalls();
        _gameFactory.ClearReceivedCalls();

        // Act
        var joinTask = ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();
        _sut.IsLanMode = true;

        // Assert - switching modes cancels the in-flight join token
        capturedToken.IsCancellationRequested.ShouldBeTrue();
        _sut.JoinError.ShouldBeNull();

        // Complete the in-flight join; it must not proceed to connect
        joinTcs.SetResult();
        await joinTask;

        _sut.IsConnected.ShouldBeFalse();
        _sut.CanJoin.ShouldBeTrue();
        _gameFactory.DidNotReceive().CreateClientGame(_commandPublisher);
        _commandPublisher.DidNotReceive().Subscribe(Arg.Any<Action<IGameCommand>>());
    }

    [Fact]
    public async Task SwitchingJoinMode_ClearsJoinError_ButPreservesRoomCode()
    {
        // Arrange - trigger a failed online join to set JoinError
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        _gameConnector.JoinOnline("ABCDEF", null, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _gameConnector.OnlineError.Returns(new RelayClientError(RelayClientErrorCode.RoomNotFound, "unused"));
        await ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();
        _sut.JoinError.ShouldNotBeNull();

        // Act
        _sut.IsLanMode = true;

        // Assert
        _sut.IsLanMode.ShouldBeTrue();
        _sut.IsOnlineMode.ShouldBeFalse();
        _sut.JoinError.ShouldBeNull();
        _sut.RoomCode.ShouldBe("ABCDEF");
    }

    [Fact]
    public async Task JoinStatusText_WhileJoining_ReturnsConnectingText()
    {
        // Arrange
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        var joinTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _gameConnector.JoinOnline(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(joinTcs.Task);

        // Act
        var joinTask = ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Assert
        _sut.JoinStatusText.ShouldBe(_localizationService.GetString("Join_Connecting"));

        // Clean up
        joinTcs.SetResult();
        await joinTask;
    }

    [Fact]
    public async Task CanJoin_ReturnsFalse_WhileJoining()
    {
        // Arrange
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        var joinTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _gameConnector.JoinOnline(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(joinTcs.Task);

        // Act
        var joinTask = ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Assert
        _sut.CanJoin.ShouldBeFalse();

        // Clean up
        joinTcs.SetResult();
        await joinTask;
        _sut.CanJoin.ShouldBeTrue();
    }

    [Fact]
    public async Task JoinRoom_WhileJoining_DoesNotStartSecondJoin()
    {
        // Arrange
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        var joinTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _gameConnector.JoinOnline(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(joinTcs.Task);

        // Act
        var firstJoin = ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();
        await ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Assert
        await _gameConnector.Received(1).JoinOnline(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());

        // Clean up
        joinTcs.SetResult();
        await firstJoin;
    }

    [Fact]
    public void RoomCode_NormalizesToTrimmedUppercase()
    {
        _sut.RoomCode = "  abcdef  ";

        _sut.RoomCode.ShouldBe("ABCDEF");
    }

    [Fact]
    public async Task JoinRoom_DisposesPreviousGame_WhenGameAlreadyExists()
    {
        // Arrange - create a game via a failed LAN connect, leaving IsConnected=false
        _sut.ServerIp = "127.0.0.1";

        var initializeCallCount = 0;
        _botManager.When(b => b.Initialize(Arg.Any<ClientGame>(), Arg.Any<DecisionEngineProvider>()))
            .Do(_ =>
            {
                initializeCallCount++;
                if (initializeCallCount == 1)
                    throw new Exception("Simulated initialization failure");
            });

        EnableLanConnect();
        _sut.ConnectCommand.Execute(null);

        _clientGame.IsDisposed.ShouldBeFalse();
        _sut.IsConnected.ShouldBeFalse();

        _gameFactory.ClearReceivedCalls();

        // Arrange a successful online join
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        _sut.CanJoin.ShouldBeTrue();
        EnableOnlineJoin();

        // Act
        await ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Assert
        _clientGame.IsDisposed.ShouldBeTrue();
        _gameFactory.Received(1).CreateClientGame(_commandPublisher);
        _sut.IsConnected.ShouldBeTrue();
        _sut.JoinError.ShouldBeNull();
    }

    [Fact]
    public async Task JoinRoom_WhenDisconnectedDuringJoin_DoesNotRestoreConnection()
    {
        // Arrange
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        var joinTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _gameConnector.JoinOnline(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var token = ci.Arg<CancellationToken>();
                // When the token is cancelled, abort the in-flight join
                token.Register(() => joinTcs.TrySetException(new OperationCanceledException()));
                return joinTcs.Task;
            });

        _commandPublisher.ClearReceivedCalls();
        _gameFactory.ClearReceivedCalls();

        // Act
        var joinTask = ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();
        await _sut.Disconnect(); // Cancels the active join token
        await joinTask;

        // Assert - Disconnect aborts the pending join: no game is created and connection is not restored
        _gameFactory.DidNotReceive().CreateClientGame(_commandPublisher);
        _commandPublisher.DidNotReceive().Subscribe(Arg.Any<Action<IGameCommand>>());
        _sut.IsConnected.ShouldBeFalse();
        _sut.JoinError.ShouldBeNull(); // Cancellation, not error
    }

    [Fact]
    public void JoinedRoomInfoText_ReturnsEmpty_WhenNotConnected()
    {
        // Arrange
        _sut.IsConnected.ShouldBeFalse();

        // Assert
        _sut.JoinedRoomInfoText.ShouldBeEmpty();
    }

    [Fact]
    public async Task JoinedRoomInfoText_ReturnsLocalizedServerInfo_AfterLanConnect()
    {
        // Arrange
        _sut.ServerIp = "127.0.0.1";
        var expectedText = string.Format(_localizationService.GetString("Join_ServerConnectedInfo"), _sut.ServerAddress);
        var raisedEvents = new List<string?>();
        _sut.PropertyChanged += (_, e) => raisedEvents.Add(e.PropertyName);

        // Act
        ConnectAndAckLobby();

        // Assert
        _sut.IsConnected.ShouldBeTrue();
        _sut.JoinedRoomInfoText.ShouldBe(expectedText);
        raisedEvents.ShouldContain(nameof(JoinGameViewModel.JoinedRoomInfoText));

        // Act - changing the server address while connected updates the info text
        raisedEvents.Clear();
        _sut.ServerIp = "192.168.1.10";

        // Assert
        var updatedText = string.Format(_localizationService.GetString("Join_ServerConnectedInfo"), _sut.ServerAddress);
        _sut.JoinedRoomInfoText.ShouldBe(updatedText);
        raisedEvents.ShouldContain(nameof(JoinGameViewModel.JoinedRoomInfoText));

        // Act
        raisedEvents.Clear();
        await _sut.Disconnect();

        // Assert
        _sut.IsConnected.ShouldBeFalse();
        _sut.JoinedRoomInfoText.ShouldBeEmpty();
        raisedEvents.ShouldContain(nameof(JoinGameViewModel.JoinedRoomInfoText));
    }

    [Fact]
    public async Task JoinedRoomInfoText_ReturnsLocalizedRoomInfo_AfterOnlineJoin()
    {
        // Arrange
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        var expectedText = string.Format(_localizationService.GetString("Join_RoomJoinedInfo"), "ABCDEF");
        EnableOnlineJoin();
        var raisedEvents = new List<string?>();
        _sut.PropertyChanged += (_, e) => raisedEvents.Add(e.PropertyName);

        // Act
        await ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Assert
        _sut.IsConnected.ShouldBeTrue();
        _sut.JoinedRoomInfoText.ShouldBe(expectedText);
        raisedEvents.ShouldContain(nameof(JoinGameViewModel.JoinedRoomInfoText));

        // Act - changing the room code while connected updates the info text
        raisedEvents.Clear();
        _sut.RoomCode = "GHIJKL";

        // Assert
        var updatedRoomText = string.Format(_localizationService.GetString("Join_RoomJoinedInfo"), _sut.RoomCode);
        _sut.JoinedRoomInfoText.ShouldBe(updatedRoomText);
        raisedEvents.ShouldContain(nameof(JoinGameViewModel.JoinedRoomInfoText));

        // Act - switching to LAN mode while connected updates the info text
        raisedEvents.Clear();
        _sut.ServerIp = "127.0.0.1";
        _sut.IsLanMode = true;

        // Assert
        var lanModeText = string.Format(_localizationService.GetString("Join_ServerConnectedInfo"), _sut.ServerAddress);
        _sut.JoinedRoomInfoText.ShouldBe(lanModeText);
        raisedEvents.ShouldContain(nameof(JoinGameViewModel.JoinedRoomInfoText));

        // Act
        raisedEvents.Clear();
        await _sut.Disconnect();

        // Assert
        _sut.IsConnected.ShouldBeFalse();
        _sut.JoinedRoomInfoText.ShouldBeEmpty();
        raisedEvents.ShouldContain(nameof(JoinGameViewModel.JoinedRoomInfoText));
    }

    [Fact]
    public void IsLanFormVisible_ReturnsTrue_WhenLanModeAndNotConnected()
    {
        // Arrange
        _sut.IsOnlineMode = true;
        var raisedEvents = new List<string?>();
        _sut.PropertyChanged += (_, e) => raisedEvents.Add(e.PropertyName);

        // Act
        _sut.IsLanMode = true;

        // Assert
        _sut.IsConnected.ShouldBeFalse();
        _sut.IsLanFormVisible.ShouldBeTrue();
        raisedEvents.ShouldContain(nameof(JoinGameViewModel.IsLanFormVisible));
    }

    [Fact]
    public void IsLanFormVisible_ReturnsFalse_WhenOnlineMode()
    {
        // Arrange
        var raisedEvents = new List<string?>();
        _sut.PropertyChanged += (_, e) => raisedEvents.Add(e.PropertyName);

        // Act
        _sut.IsOnlineMode = true;

        // Assert
        _sut.IsLanFormVisible.ShouldBeFalse();
        raisedEvents.ShouldContain(nameof(JoinGameViewModel.IsLanFormVisible));
    }

    [Fact]
    public void IsLanFormVisible_ReturnsFalse_WhenConnected()
    {
        // Arrange
        _sut.ServerIp = "127.0.0.1";
        var raisedEvents = new List<string?>();
        _sut.PropertyChanged += (_, e) => raisedEvents.Add(e.PropertyName);

        // Act
        ConnectAndAckLobby();

        // Assert
        _sut.IsConnected.ShouldBeTrue();
        _sut.IsLanFormVisible.ShouldBeFalse();
        raisedEvents.ShouldContain(nameof(JoinGameViewModel.IsLanFormVisible));
    }

    [Fact]
    public void IsOnlineFormVisible_ReturnsTrue_WhenOnlineModeAndNotConnected()
    {
        // Arrange
        _sut.IsLanMode = true;
        var raisedEvents = new List<string?>();
        _sut.PropertyChanged += (_, e) => raisedEvents.Add(e.PropertyName);

        // Act
        _sut.IsOnlineMode = true;

        // Assert
        _sut.IsConnected.ShouldBeFalse();
        _sut.IsOnlineFormVisible.ShouldBeTrue();
        raisedEvents.ShouldContain(nameof(JoinGameViewModel.IsOnlineFormVisible));
    }

    [Fact]
    public void IsOnlineFormVisible_ReturnsFalse_WhenLanMode()
    {
        // Arrange
        _sut.IsOnlineMode = true;
        var raisedEvents = new List<string?>();
        _sut.PropertyChanged += (_, e) => raisedEvents.Add(e.PropertyName);

        // Act
        _sut.IsLanMode = true;

        // Assert
        _sut.IsOnlineFormVisible.ShouldBeFalse();
        raisedEvents.ShouldContain(nameof(JoinGameViewModel.IsOnlineFormVisible));
    }

    [Fact]
    public async Task IsOnlineFormVisible_ReturnsFalse_WhenConnected()
    {
        // Arrange
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        EnableOnlineJoin();
        var raisedEvents = new List<string?>();
        _sut.PropertyChanged += (_, e) => raisedEvents.Add(e.PropertyName);

        // Act
        await ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Assert
        _sut.IsConnected.ShouldBeTrue();
        _sut.IsOnlineFormVisible.ShouldBeFalse();
        raisedEvents.ShouldContain(nameof(JoinGameViewModel.IsOnlineFormVisible));
    }

    [Fact]
    public async Task UpdateLobbyMapPreview_WhenCancelled_LogsDebugMessage()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000";
        ConnectAndAckLobby();
        var battleMap = BattleMapFactory.GenerateMap(2, 2,
            new SingleTerrainGenerator(2, 2, new ClearTerrain()));
        _mapFactory.CreateFromData(Arg.Any<BattleMapData>()).Returns(battleMap);

        var previewTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        // Signal set as soon as GeneratePreview is entered, so disposal happens only after
        // the render is actually in flight (no arbitrary Task.Delay guessing).
        var previewEnteredTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken capturedToken = default;
        _mapPreviewRenderer.GeneratePreview(battleMap, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedToken = ci.Arg<CancellationToken>();
                capturedToken.Register(() => previewTcs.TrySetCanceled(capturedToken));
                previewEnteredTcs.TrySetResult();
                return previewTcs.Task;
            });

        // Signal completed when the expected cancellation debug log is observed.
        var logReceivedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _logger
            .When(l => l.Log(
                LogLevel.Debug,
                Arg.Any<EventId>(),
                Arg.Is<object>(state => state.ToString()!.Contains("Map preview generation cancelled")),
                Arg.Is<Exception?>(e => e == null),
                Arg.Any<Func<object, Exception?, string>>()))
            .Do(_ => logReceivedTcs.TrySetResult());

        // Act - start preview generation
        _sut.HandleServerCommand(new SetBattleMapCommand
        {
            GameOriginId = Guid.NewGuid(),
            MapData = new BattleMapData { HexData = [] }
        });

        // Wait until GeneratePreview has actually been entered before cancelling
        await previewEnteredTcs.Task;

        // Cancel the in-flight preview by disposing the view model (which cancels the preview token)
        await _sut.DisposeAsync();

        // Assert - asynchronously wait (bounded) for the cancellation log rather than asserting
        // immediately, since the cancellation continuation runs asynchronously after disposal.
        var completed = await Task.WhenAny(
            logReceivedTcs.Task,
            Task.Delay(TimeSpan.FromSeconds(5)));
        completed.ShouldBe(logReceivedTcs.Task,
            "Timed out waiting for 'Map preview generation cancelled' debug log");

        _logger.Received().Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("Map preview generation cancelled")),
            Arg.Is<Exception?>(e => e == null),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task JoinRoom_WhenSupersededByNewJoin_DisconnectsOrphanedConnection()
    {
        // Arrange
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        var firstJoinTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondJoinTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _gameConnector.JoinOnline(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(firstJoinTcs.Task, secondJoinTcs.Task);

        _commandPublisher.ClearReceivedCalls();
        _gameFactory.ClearReceivedCalls();

        // Act - start a join, switch to LAN mode, then start a newer join that supersedes it
        var firstJoin = ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();
        _sut.IsLanMode = true;
        var secondJoin = ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Complete the superseded join; it must clean up the connection it left behind.
        // The join completed having connected (connector reports connected) before it was superseded.
        _connectorConnected = true;
        firstJoinTcs.SetResult();
        await firstJoin;

        // Assert - the superseded first join cleaned up its orphaned connection
        await _gameConnector.Received(1).Disconnect(Arg.Any<CancellationToken>());
        _gameFactory.DidNotReceive().CreateClientGame(_commandPublisher);
        _commandPublisher.DidNotReceive().Subscribe(Arg.Any<Action<IGameCommand>>());
        _sut.IsConnected.ShouldBeFalse();
        _sut.JoinError.ShouldBeNull();

        // Switch back to online mode so the newer join completes as a successful connection
        _sut.IsOnlineMode = true;
        _connectorConnected = true;
        secondJoinTcs.SetResult();
        await secondJoin;

        // Assert - the newer join connected successfully
        _gameFactory.Received(1).CreateClientGame(_commandPublisher);
        _commandPublisher.Received(1).Subscribe(Arg.Any<Action<IGameCommand>>());
        _sut.IsConnected.ShouldBeTrue();
        _sut.JoinError.ShouldBeNull();
    }
}

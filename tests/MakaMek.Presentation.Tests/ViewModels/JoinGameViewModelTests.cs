using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Services;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Services.Transport.Relay;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MVVM.Core.Services;
using Sanet.Transport;
using Sanet.Transport.SignalR.Client.Publishers;
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
    private readonly ITransportFactory _transportFactory = Substitute.For<ITransportFactory>();
    private readonly ICommandTransportAdapter _adapter = Substitute.For<ICommandTransportAdapter>();
    private readonly ITransportPublisher _transportPublisher = Substitute.For<ITransportPublisher>();
    private readonly ICommandPublisher _commandPublisher = Substitute.For<ICommandPublisher>();
    private readonly IUnitsLoader _unitsLoader = Substitute.For<IUnitsLoader>();
    private readonly IBattleMapFactory _mapFactory = Substitute.For<IBattleMapFactory>();
    private readonly IMechFactory _mechFactory = Substitute.For<IMechFactory>();
    private readonly IFileCachingService _cachingService = Substitute.For<IFileCachingService>();
    private readonly IHashService _hashService = Substitute.For<IHashService>();
    private readonly IBotManager _botManager = Substitute.For<IBotManager>();
    private readonly ILogger<JoinGameViewModel> _logger = Substitute.For<ILogger<JoinGameViewModel>>();
    private readonly IRelayRoomClient _relayRoomClient = Substitute.For<IRelayRoomClient>();
    private readonly IRelayPublisherFactory _relayPublisherFactory = Substitute.For<IRelayPublisherFactory>();
    private readonly IOptions<RelayClientOptions> _relayOptions = Substitute.For<IOptions<RelayClientOptions>>();
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly ClientGame _clientGame;

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
        // Configure the adapter to be accessible from the command publisher
        _commandPublisher.Adapter.Returns(_adapter);
        
        // Configure the transport factory to return our mock transport publisher
        _transportFactory.CreateAndStartClientPublisher(Arg.Any<string>())
            .Returns(Task.FromResult(_transportPublisher));
            
        // Configure the game factory to return our mock client game
        _gameFactory.CreateClientGame(_commandPublisher).Returns(_clientGame);
        
        // Configure dispatcher to execute actions immediately
        _dispatcherService.RunOnUIThread(Arg.InvokeDelegate<Func<Task>>());

        _cachingService.TryGetCachedFile(Arg.Any<string>()).Returns(Task.FromResult<byte[]?>(null));

        _relayOptions.Value.Returns(new RelayClientOptions { BaseUrl = "http://hub.local" });

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

        _sut = CreateSut();
    }

    private JoinGameViewModel CreateSut(
        IFileCachingService? cachingService = null,
        IRelayRoomClient? relayRoomClient = null,
        IRelayPublisherFactory? relayPublisherFactory = null,
        IOptions<RelayClientOptions>? relayOptions = null)
    {
        var sut = new JoinGameViewModel(
            _unitsLoader,
            _commandPublisher,
            _dispatcherService,
            _gameFactory,
            _transportFactory,
            cachingService ?? _cachingService,
            _botManager,
            _logger,
            _mechFactory,
            relayRoomClient ?? _relayRoomClient,
            relayPublisherFactory ?? _relayPublisherFactory,
            relayOptions ?? _relayOptions,
            _localizationService);
        sut.AttachHandlers();
        return sut;
    }

    private static RelayClientPublisher CreateRelayPublisher(string roomCode, string sessionToken, Guid hostId) =>
        new("http://hub.local/hubs/relay", roomCode, sessionToken, NullLogger<RelayClientPublisher>.Instance, hostId.ToString());

    private void ConnectAndAckLobby()
    {
        _sut.ConnectCommand.Execute(null);
        var lobbyCommand = (RequestGameLobbyStatusCommand)_commandPublisher.ReceivedCalls().Last().GetArguments()[0]!;
        _clientGame.HandleCommand(lobbyCommand with { GameOriginId = Guid.NewGuid() });
    }

    [Fact]
    public void ConnectToServer_ClearsExistingPublishers()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000"; // Set a valid server address
        
        // Act
        ConnectAndAckLobby();
        
        // Assert
        // Verify that ClearPublishers was called on the adapter
        _adapter.Received(1).ClearPublishers();
    }
    
    [Fact]
    public void ConnectToServer_RequestsLobbyStatus()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000"; // Set a valid server address
        
        // Act
        _sut.ConnectCommand.Execute(null);
        
        // Assert
        var lobbyCommand= (RequestGameLobbyStatusCommand)_commandPublisher.ReceivedCalls() 
            .Last().GetArguments()[0]!;
        lobbyCommand.GameOriginId.ShouldBe(_clientGame.Id);
        _clientGame.HandleCommand(lobbyCommand with { GameOriginId = Guid.NewGuid() });
    }
    
    [Fact]
    public void ConnectToServer_AddsNewPublisherAfterClearing()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000"; // Set a valid server address
        
        // Act
        ConnectAndAckLobby();
        
        // Assert
        // Verify that the new publisher was added to the adapter
        _adapter.Received(1).AddPublisher(_transportPublisher);
    }
    
    [Fact]
    public void ConnectToServer_SetsIsConnectedToTrue_OnSuccess()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000"; // Set a valid server address
        
        // Act
        _sut.ConnectCommand.Execute(null);
        
        // Assert
        var lobbyCommand= (RequestGameLobbyStatusCommand)_commandPublisher.ReceivedCalls() 
            .Last().GetArguments()[0]!;
        _clientGame.HandleCommand(lobbyCommand with { GameOriginId = Guid.NewGuid() });
        
        _sut.IsConnected.ShouldBeTrue();
        _sut.CanPublishCommands.ShouldBeTrue();
    }
    
    [Fact]
    public void ConnectToServer_SetsIsConnectedToFalse_OnError()
    {
        // Arrange
        _sut.ServerIp = "http://localhost:5000"; // Set a valid server address
        
        // Configure the factory to throw an exception
        _transportFactory.CreateAndStartClientPublisher(Arg.Any<string>())
            .Returns<Task<ITransportPublisher>>(_ => throw new Exception("Connection failed"));
        
        // Act
        _sut.ConnectCommand.Execute(null);
        
        // Assert
        _sut.IsConnected.ShouldBeFalse();
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
    public void ConnectCommand_DoesNotCreateClientGame_WhenLocalGameExists()
    {
        // Arrange - call once to create the game
        _sut.ServerIp="127.0.0.1";
        _sut.ConnectCommand.Execute(null);
        _gameFactory.ClearReceivedCalls();
        
        // Act - call again
        ConnectAndAckLobby();
        
        // Assert - should not create a new game
        _gameFactory.DidNotReceive().CreateClientGame(Arg.Any<ICommandPublisher>());
    }

    [Fact]
    public void Disconnect_ShouldDisposeLocalGame()
    {
        // Arrange
        _sut.ServerIp = "127.0.0.1";
        ConnectAndAckLobby();
        _clientGame.IsDisposed.ShouldBeFalse();

        // Act
        _sut.Disconnect();

        // Assert
        _clientGame.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public void Disconnect_ShouldClearPublishers()
    {
        // Arrange
        _sut.ServerIp = "127.0.0.1";
        ConnectAndAckLobby();
        _adapter.ClearReceivedCalls();

        // Act
        _sut.Disconnect();

        // Assert
        _adapter.Received(1).ClearPublishers();
    }

    [Fact]
    public void Disconnect_ShouldSetIsConnectedToFalse()
    {
        // Arrange
        _sut.ServerIp = "127.0.0.1";
        ConnectAndAckLobby();
        _sut.IsConnected.ShouldBeTrue();

        // Act
        _sut.Disconnect();

        // Assert
        _sut.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public void Dispose_ShouldCallDisconnect()
    {
        // Arrange
        _sut.ServerIp = "127.0.0.1";
        ConnectAndAckLobby();
        _clientGame.IsDisposed.ShouldBeFalse();

        // Act
        _sut.Dispose();

        // Assert
        _clientGame.IsDisposed.ShouldBeTrue();
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
        _sut.ConnectCommand.Execute(null);
        
        var lobbyCommand= (RequestGameLobbyStatusCommand)_commandPublisher.ReceivedCalls() 
            .Last().GetArguments()[0]!;
        // Complete request lobby command
        _clientGame.HandleCommand(lobbyCommand with { GameOriginId = Guid.NewGuid() });
        
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
    public async Task HandleCommandInternal_SetBattleMapCommand_SetsBattleMapAndNavigates()
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
        _sut.HandleServerCommand(new SetBattleMapCommand
        {
            GameOriginId = Guid.NewGuid(),
            MapData = new BattleMapData { HexData = [] }
        });

        // Assert
        navigationService.Received(1).GetViewModel<BattleMapViewModel>();
        battleMapViewModel.Game.ShouldNotBeNull();
        await navigationService.Received(1).NavigateToViewModelAsync(battleMapViewModel);
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
            _transportFactory,
            cachingService,
            _botManager,
            _logger,
            _mechFactory);
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
            _transportFactory,
            cachingService,
            _botManager,
            _logger,
            _mechFactory);

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

        // Act - first call creates _localGame but fails before IsConnected=true
        _sut.ConnectCommand.Execute(null);

        // First attempt left _localGame set but not disposed and IsConnected=false
        _clientGame.IsDisposed.ShouldBeFalse();
        _sut.IsConnected.ShouldBeFalse();

        _gameFactory.ClearReceivedCalls();

        // Act - second call enters the block (line 179), disposes old, creates new
        _sut.ConnectCommand.Execute(null);

        // Assert
        _clientGame.IsDisposed.ShouldBeTrue(); // disposed at line 181
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
    public async Task JoinRoom_Success_CreatesPublisherAndConnects()
    {
        // Arrange
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        var player = _sut.Players.First();
        var playerId = player.Player.Id;
        var playerName = player.Player.Name;
        const string sessionToken = "session-token";
        var hostId = Guid.NewGuid();
        _relayRoomClient.JoinAsync("ABCDEF", playerId, playerName, Arg.Any<CancellationToken>())
            .Returns(RoomJoinResult.Succeeded("ABCDEF", sessionToken, "Player", playerId, hostId));
        var publisher = CreateRelayPublisher("ABCDEF", sessionToken, hostId);
        _relayPublisherFactory.CreateAsync("http://hub.local/hubs/relay", "ABCDEF", sessionToken, hostId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(publisher));

        _commandPublisher.ClearReceivedCalls();
        _adapter.ClearReceivedCalls();
        _relayRoomClient.ClearReceivedCalls();
        _relayPublisherFactory.ClearReceivedCalls();
        _gameFactory.ClearReceivedCalls();
        _botManager.ClearReceivedCalls();

        // Act
        await ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Assert
        await _relayRoomClient.Received(1).JoinAsync("ABCDEF", playerId, playerName, Arg.Any<CancellationToken>());
        await _relayPublisherFactory.Received(1).CreateAsync(
            "http://hub.local/hubs/relay", "ABCDEF", sessionToken, hostId, Arg.Any<CancellationToken>());
        _adapter.Received(1).ClearPublishers();
        _adapter.Received(1).AddPublisher(publisher);
        _commandPublisher.Received(1).Subscribe(Arg.Any<Action<IGameCommand>>());
        _gameFactory.Received(1).CreateClientGame(_commandPublisher);
        _botManager.Received(1).Initialize(_clientGame, Arg.Any<DecisionEngineProvider>());
        _commandPublisher.Received(1).PublishCommand(Arg.Is<RequestGameLobbyStatusCommand>(c => c.GameOriginId == _clientGame.Id));
        _sut.IsConnected.ShouldBeTrue();
        _sut.JoinError.ShouldBeNull();
        _sut.JoinStatusText.ShouldBeEmpty();
        _sut.CanJoin.ShouldBeFalse();
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

        _sut.ConnectCommand.Execute(null);

        _clientGame.IsDisposed.ShouldBeFalse();
        _sut.IsConnected.ShouldBeFalse();

        _gameFactory.ClearReceivedCalls();

        // Arrange a successful online join
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        _sut.CanJoin.ShouldBeTrue();
        var player = _sut.Players.First();
        const string sessionToken = "session-token";
        var hostId = Guid.NewGuid();
        _relayRoomClient.JoinAsync("ABCDEF", player.Player.Id, player.Player.Name, Arg.Any<CancellationToken>())
            .Returns(RoomJoinResult.Succeeded("ABCDEF", sessionToken, "Player", player.Player.Id, hostId));
        var publisher = CreateRelayPublisher("ABCDEF", sessionToken, hostId);
        _relayPublisherFactory.CreateAsync("http://hub.local/hubs/relay", "ABCDEF", sessionToken, hostId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(publisher));

        // Act
        await ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Assert
        _clientGame.IsDisposed.ShouldBeTrue();
        _gameFactory.Received(1).CreateClientGame(_commandPublisher);
        _sut.IsConnected.ShouldBeTrue();
        _sut.JoinError.ShouldBeNull();
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
        // Arrange
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        var player = _sut.Players.First();
        const string sessionToken = "session-token";
        _relayRoomClient.JoinAsync("ABCDEF", player.Player.Id, player.Player.Name, Arg.Any<CancellationToken>())
            .Returns(RoomJoinResult.Failed(new RelayClientError(code, "unused")));

        _commandPublisher.ClearReceivedCalls();
        _relayPublisherFactory.ClearReceivedCalls();

        // Act
        await ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Assert
        var expectedText = _localizationService.GetString(expectedKey);
        _sut.JoinError.ShouldBe(expectedText);
        _sut.JoinStatusText.ShouldBe(expectedText);
        _sut.IsConnected.ShouldBeFalse();
        _sut.CanJoin.ShouldBeTrue();
        await _relayPublisherFactory.DidNotReceive().CreateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        _commandPublisher.DidNotReceive().Subscribe(Arg.Any<Action<IGameCommand>>());
        _sut.JoinError!.ShouldNotContain("ABCDEF");
        _sut.JoinError!.ShouldNotContain(sessionToken);
        _sut.JoinStatusText.ShouldNotContain("ABCDEF");
        _sut.JoinStatusText.ShouldNotContain(sessionToken);
    }

    [Fact]
    public async Task JoinRoom_WhenPublisherCreationFails_ShowsConnectionFailedAndStaysRecoverable()
    {
        // Arrange
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        var player = _sut.Players.First();
        const string sessionToken = "session-token";
        _relayOptions.Value.Returns(new RelayClientOptions { BaseUrl = "http://hub.local", ApiKey = "api-key-secret" });
        _relayRoomClient.JoinAsync("ABCDEF", player.Player.Id, player.Player.Name, Arg.Any<CancellationToken>())
            .Returns(RoomJoinResult.Succeeded("ABCDEF", sessionToken, "Player", player.Player.Id, Guid.NewGuid()));
        _relayPublisherFactory.CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("boom"));

        // Act
        await ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Assert
        _sut.JoinError.ShouldBe(_localizationService.GetString("Join_ConnectionFailed"));
        _sut.JoinStatusText.ShouldBe(_localizationService.GetString("Join_ConnectionFailed"));
        _sut.IsConnected.ShouldBeFalse();
        _sut.CanJoin.ShouldBeTrue();
        _adapter.DidNotReceive().ClearPublishers();
        _gameFactory.DidNotReceive().CreateClientGame(_commandPublisher);
        _sut.JoinError!.ShouldNotContain(sessionToken);
        _sut.JoinError!.ShouldNotContain("api-key-secret");
        _sut.JoinStatusText.ShouldNotContain(sessionToken);
        _sut.JoinStatusText.ShouldNotContain("api-key-secret");
    }

    [Fact]
    public async Task JoinRoom_WhenRelayNotConfigured_ShowsConfigurationErrorAndDoesNotCallJoin()
    {
        // Arrange - no relay dependencies are provided
        var sut = new JoinGameViewModel(
            _unitsLoader,
            _commandPublisher,
            _dispatcherService,
            _gameFactory,
            _transportFactory,
            _cachingService,
            _botManager,
            _logger,
            _mechFactory,
            localizationService: _localizationService);
        sut.AttachHandlers();
        sut.IsOnlineMode = true;
        sut.RoomCode = "ABCDEF";

        // Act
        await ((AsyncCommand)sut.JoinRoomCommand).ExecuteAsync();

        // Assert
        sut.JoinError.ShouldBe(_localizationService.GetString("Join_ConfigurationError"));
        sut.JoinStatusText.ShouldBe(_localizationService.GetString("Join_ConfigurationError"));
        sut.IsConnected.ShouldBeFalse();
        sut.CanJoin.ShouldBeTrue();
        await _relayRoomClient.DidNotReceive().JoinAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchingJoinMode_ClearsJoinError_ButPreservesRoomCode()
    {
        // Arrange - trigger a failed online join to set JoinError
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        var player = _sut.Players.First();
        _relayRoomClient.JoinAsync("ABCDEF", player.Player.Id, player.Player.Name, Arg.Any<CancellationToken>())
            .Returns(RoomJoinResult.Failed(new RelayClientError(RelayClientErrorCode.RoomNotFound, "unused")));
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
        var joinTcs = new TaskCompletionSource<RoomJoinResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _relayRoomClient.JoinAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(joinTcs.Task);

        // Act
        var joinTask = ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Assert
        _sut.JoinStatusText.ShouldBe(_localizationService.GetString("Join_Connecting"));

        // Clean up
        joinTcs.SetResult(RoomJoinResult.Failed(new RelayClientError(RelayClientErrorCode.Unknown, "unused")));
        await joinTask;
    }

    [Fact]
    public async Task CanJoin_ReturnsFalse_WhileJoining()
    {
        // Arrange
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        var joinTcs = new TaskCompletionSource<RoomJoinResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _relayRoomClient.JoinAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(joinTcs.Task);

        // Act
        var joinTask = ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Assert
        _sut.CanJoin.ShouldBeFalse();

        // Clean up
        joinTcs.SetResult(RoomJoinResult.Failed(new RelayClientError(RelayClientErrorCode.Unknown, "unused")));
        await joinTask;
        _sut.CanJoin.ShouldBeTrue();
    }

    [Fact]
    public async Task JoinRoom_WhileJoining_DoesNotStartSecondJoin()
    {
        // Arrange
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        var joinTcs = new TaskCompletionSource<RoomJoinResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _relayRoomClient.JoinAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(joinTcs.Task);

        // Act
        var firstJoin = ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();
        await ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Assert
        await _relayRoomClient.Received(1).JoinAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        // Clean up
        joinTcs.SetResult(RoomJoinResult.Failed(new RelayClientError(RelayClientErrorCode.Unknown, "unused")));
        await firstJoin;
    }

    [Fact]
    public void RoomCode_NormalizesToTrimmedUppercase()
    {
        _sut.RoomCode = "  abcdef  ";

        _sut.RoomCode.ShouldBe("ABCDEF");
    }

    [Fact]
    public async Task JoinRoom_UsesNormalizedRoomCode()
    {
        // Arrange
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "  abcdef  ";
        var player = _sut.Players.First();
        _relayRoomClient.JoinAsync("ABCDEF", player.Player.Id, player.Player.Name, Arg.Any<CancellationToken>())
            .Returns(RoomJoinResult.Failed(new RelayClientError(RelayClientErrorCode.RoomNotFound, "unused")));

        // Act
        await ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Assert
        await _relayRoomClient.Received(1).JoinAsync("ABCDEF", player.Player.Id, player.Player.Name, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinRoom_WhenPostCreationInitializationFails_RemovesAndDisposesPublisher()
    {
        // Arrange
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        var player = _sut.Players.First();
        const string sessionToken = "session-token";
        var hostId = Guid.NewGuid();
        _relayRoomClient.JoinAsync("ABCDEF", player.Player.Id, player.Player.Name, Arg.Any<CancellationToken>())
            .Returns(RoomJoinResult.Succeeded("ABCDEF", sessionToken, "Player", player.Player.Id, hostId));
        var publisher = CreateRelayPublisher("ABCDEF", sessionToken, hostId);
        _relayPublisherFactory.CreateAsync("http://hub.local/hubs/relay", "ABCDEF", sessionToken, hostId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(publisher));
        _botManager.When(b => b.Initialize(Arg.Any<ClientGame>(), Arg.Any<DecisionEngineProvider>()))
            .Do(_ => throw new Exception("Simulated initialization failure"));

        _commandPublisher.ClearReceivedCalls();
        _adapter.ClearReceivedCalls();

        // Act
        await ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();

        // Assert
        _adapter.Received(1).AddPublisher(publisher);
        _adapter.Received(1).RemovePublisher(publisher);
        publisher.State.ToString().ShouldBe("Disconnected");
        _sut.IsConnected.ShouldBeFalse();
        _sut.JoinError.ShouldBe(_localizationService.GetString("Join_ConnectionFailed"));
    }

    [Fact]
    public async Task Disconnect_RemovesAndDisposesOnlinePublisher()
    {
        // Arrange - successful online join
        _sut.IsOnlineMode = true;
        _sut.RoomCode = "ABCDEF";
        var player = _sut.Players.First();
        const string sessionToken = "session-token";
        var hostId = Guid.NewGuid();
        _relayRoomClient.JoinAsync("ABCDEF", player.Player.Id, player.Player.Name, Arg.Any<CancellationToken>())
            .Returns(RoomJoinResult.Succeeded("ABCDEF", sessionToken, "Player", player.Player.Id, hostId));
        var publisher = CreateRelayPublisher("ABCDEF", sessionToken, hostId);
        _relayPublisherFactory.CreateAsync("http://hub.local/hubs/relay", "ABCDEF", sessionToken, hostId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(publisher));

        await ((AsyncCommand)_sut.JoinRoomCommand).ExecuteAsync();
        _sut.IsConnected.ShouldBeTrue();

        _adapter.ClearReceivedCalls();

        // Act
        _sut.Disconnect();

        // Assert
        _adapter.Received(1).RemovePublisher(publisher);
        publisher.State.ToString().ShouldBe("Disconnected");
        _sut.IsConnected.ShouldBeFalse();
    }
}

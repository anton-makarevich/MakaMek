using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.SignalR.Client;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Mechanics.WeaponAttack;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Services.Transport.Relay;
using Sanet.Transport;
using Sanet.Transport.SignalR.Client.Publishers;
using Shouldly;
using Sanet.MakaMek.Core.Services.Logging.Factories;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Services.Logging;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Tests.Models.Game;

public class GameManagerTests : IDisposable
{
    private readonly GameManager _sut;
    private readonly ICommandPublisher _commandPublisher;
    private readonly CommandTransportAdapter _transportAdapter;
    private readonly IGameFactory _gameFactory;
    private readonly ServerGame _serverGame;
    private readonly INetworkHostService _networkHostService;
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly ICommandLoggerFactory _commandLoggerFactory = Substitute.For<ICommandLoggerFactory>();
    private readonly ILogger<GameManager> _logger = Substitute.For<ILogger<GameManager>>();

    public GameManagerTests()
    {
        _commandPublisher = Substitute.For<ICommandPublisher>();
        // Use a real adapter with a mock publisher for testing AddPublisher calls
        var initialPublisher = Substitute.For<ITransportPublisher>(); 
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger<CommandTransportAdapter>().Returns(Substitute.For<ILogger<CommandTransportAdapter>>());
        _transportAdapter = new CommandTransportAdapter(loggerFactory, [initialPublisher]);
        _gameFactory = Substitute.For<IGameFactory>();
        _networkHostService = Substitute.For<INetworkHostService>();

        var rulesProvider = Substitute.For<IRulesProvider>();
        var mechFactory = Substitute.For<IMechFactory>();
        var diceRoller = Substitute.For<IDiceRoller>();
        var toHitCalculator = Substitute.For<IToHitCalculator>();
        var damageTransferCalculator = Substitute.For<IDamageTransferCalculator>();
        var criticalHitsCalculator = Substitute.For<ICriticalHitsCalculator>();
        var pilotingSkillCalculator = Substitute.For<IPilotingSkillCalculator>();
        var consciousnessCalculator = Substitute.For<IConsciousnessCalculator>();
        var heatEffectsCalculator = Substitute.For<IHeatEffectsCalculator>();
        var fallProcessor = Substitute.For<IFallProcessor>();

        _serverGame = new ServerGame(rulesProvider,
            mechFactory,
            _commandPublisher,
            diceRoller,
            toHitCalculator,
            damageTransferCalculator,
            criticalHitsCalculator,
            Substitute.For<IHullBreachCalculator>(),
            pilotingSkillCalculator,
            consciousnessCalculator,
            heatEffectsCalculator,
            fallProcessor,
            Substitute.For<IWeaponAttackResolver>(),
            Substitute.For<ILogger<ServerGame>>());
        _gameFactory.CreateServerGame(_commandPublisher).Returns(_serverGame);
        _commandPublisher.Adapter.Returns(_transportAdapter);

        _sut = new GameManager(
            _commandPublisher,
            _gameFactory,
            _localizationService,
            _commandLoggerFactory,
            _logger,
            _networkHostService);
    }
    
    private GameManager CreateSutWithNullHost() => new GameManager(
        _commandPublisher,
        _gameFactory,
        _localizationService,
        _commandLoggerFactory,
        _logger);

    private GameManager CreateSutWithRelay(
        IRelayRoomClient relayRoomClient,
        IRelayPublisherFactory relayPublisherFactory,
        INetworkHostService? networkHostService = null,
        ILogger<GameManager>? logger = null,
        string baseUrl = "http://hub.local",
        string apiKey = "api-key")
    {
        var hubConfigurationProvider = Substitute.For<IRelayHubConfigurationProvider>();
        hubConfigurationProvider.GetActiveOptions().Returns(Task.FromResult(new RelayClientOptions
        {
            BaseUrl = baseUrl,
            ApiKey = apiKey
        }));
        return new GameManager(
            _commandPublisher,
            _gameFactory,
            _localizationService,
            _commandLoggerFactory,
            logger ?? _logger,
            networkHostService,
            relayRoomClient,
            relayPublisherFactory,
            hubConfigurationProvider);
    }

    private static RelayClientPublisher CreateRelayPublisher(string roomCode, string sessionToken, Guid hostId) =>
        new("http://hub.local/hubs/relay", roomCode, sessionToken, NullLogger<RelayClientPublisher>.Instance, hostId.ToString());

    [Fact]
    public async Task InitializeLobby_WithLanEnabled_AndNotRunning_StartsNetworkHostAndAddsPublisher()
    {
        // Arrange
        var networkPublisher = Substitute.For<ITransportPublisher>();
        _networkHostService.CanStart.Returns(true);
        _networkHostService.IsRunning.Returns(false);
        _networkHostService.Publisher.Returns(networkPublisher);

        // Act
        await _sut.InitializeLobby();

        // Assert
        await _networkHostService.Received(1).Start();
        _transportAdapter.TransportPublishers.Count.ShouldBe(2); // Initial mock + network publisher
        _transportAdapter.TransportPublishers.ShouldContain(networkPublisher);
        _gameFactory.Received(1).CreateServerGame(_commandPublisher);
    }
    
    [Fact]
    public async Task InitializeLobby_CreatesServerGame()
    {
        // Act
        await _sut.InitializeLobby();

        // Assert
        _sut.ServerGameId.ShouldNotBeNull();
    }

    [Fact]
    public async Task InitializeLobby_WithLanEnabled_AndNetworkPublisherIsNull_StartsNetworkHostButDoesNotAddPublisher()
    {
        // Arrange
        _networkHostService.IsRunning.Returns(false);
        _networkHostService.CanStart.Returns(true);
        _networkHostService.Publisher.Returns((ITransportPublisher?)null);

        // Act
        await _sut.InitializeLobby();

        // Assert
        await _networkHostService.Received(1).Start();
        _transportAdapter.TransportPublishers.Count.ShouldBe(1); // Only the initial mock publisher
        _gameFactory.Received(1).CreateServerGame(_commandPublisher);
    }

    [Fact]
    public async Task InitializeLobby_WithLanEnabled_AndAlreadyRunning_DoesNotStartNetworkHost()
    {
        // Arrange
        _networkHostService.IsRunning.Returns(true);

        // Act
        await _sut.InitializeLobby();

        // Assert
        await _networkHostService.DidNotReceive().Start();
        _transportAdapter.TransportPublishers.Count.ShouldBe(1); // Only initial mock publisher
        _gameFactory.Received(1).CreateServerGame(_commandPublisher);
    }

    [Fact]
    public async Task InitializeLobby_WhenNetworkHostNotSupported_DoesNotStartNetworkHostOrAddPublisher()
    {
        // Arrange
        _networkHostService.IsRunning.Returns(false);
        _networkHostService.CanStart.Returns(false);

        // Act
        await _sut.InitializeLobby();

        // Assert
        await _networkHostService.DidNotReceive().Start();
        _transportAdapter.TransportPublishers.Count.ShouldBe(1);
        _gameFactory.Received(1).CreateServerGame(_commandPublisher);
    }

    [Fact]
    public void GetLanServerAddress_WhenRunning_ReturnsHubUrl()
    {
        // Arrange
        var expectedUrl = "http://localhost:2439";
        _networkHostService.IsRunning.Returns(true);
        _networkHostService.HubUrl.Returns(expectedUrl);

        // Act
        var result = _sut.GetLanServerAddress();

        // Assert
        result.ShouldBe(expectedUrl);
    }

    [Fact]
    public void GetLanServerAddress_WhenNotRunning_ReturnsNull()
    {
        // Arrange
        _networkHostService.IsRunning.Returns(false);

        // Act
        var result = _sut.GetLanServerAddress();

        // Assert
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsLanServerRunning_ReturnsCorrectValueFromNetworkHost(bool isRunning)
    {
        // Arrange
        _networkHostService.IsRunning.Returns(isRunning);

        // Act & Assert
        _sut.IsLanServerRunning.ShouldBe(isRunning);
    }

    [Fact]
    public void IsLanServerRunning_WhenHostIsNull_ReturnsFalse()
    {
        // Arrange
        var sutWithNullHost = CreateSutWithNullHost();

        // Act & Assert
        sutWithNullHost.IsLanServerRunning.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanStartLanServer_ReturnsCorrectValueFromNetworkHost(bool canStart)
    {
        // Arrange
        _networkHostService.CanStart.Returns(canStart);

        // Act & Assert
        _sut.CanStartLanServer.ShouldBe(canStart);
    }

    [Fact]
    public void CanStartLanServer_WhenHostIsNull_ReturnsFalse()
    {
        // Arrange
        var sutWithNullHost = CreateSutWithNullHost();

        // Act & Assert
        sutWithNullHost.CanStartLanServer.ShouldBeFalse();
    }

    [Fact]
    public async Task StartServer_CalledMultipleTimes_StartsNetworkHostOnlyOnce()
    {
        // Arrange
        var networkPublisher = Substitute.For<ITransportPublisher>();
        _networkHostService.CanStart.Returns(true);
        _networkHostService.IsRunning.Returns(false); // Start as not running
        _networkHostService.Publisher.Returns(networkPublisher);

        // Act
        await _sut.InitializeLobby(); // First call, enable LAN
        _networkHostService.IsRunning.Returns(true);  // Simulate network host is now running
        await _sut.InitializeLobby(); // Second call

        // Assert
        await _networkHostService.Received(1).Start(); // Should only be called once
    }

    [Fact]
    public void Dispose_CallsNetworkHostDispose()
    {
        // Act
        _sut.Dispose();

        // Assert
        _networkHostService.Received(1).Dispose();
    }

    [Fact]
    public void Dispose_WhenHostIsNull_DoesNotThrow()
    {
        // Arrange
        var sutWithNullHost = CreateSutWithNullHost();

        // Act & Assert
        Should.NotThrow(() => sutWithNullHost.Dispose());
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DisposesHostOnlyOnce()
    {
        // Act
        _sut.Dispose();
        _sut.Dispose(); // Call again

        // Assert
        _networkHostService.Received(1).Dispose(); // Should still be called only once
    }

    [Fact]
    public async Task SetBattleMap_CallsSetBattleMapOnServerGame()
    {
        // Arrange
        await _sut.InitializeLobby(); // Ensure _serverGame is created via factory
        var battleMap = new BattleMap(10, 10);

        // Act
        _serverGame.BattleMap.ShouldBeNull();
        _sut.SetBattleMap(battleMap);

        // Assert
        _serverGame.BattleMap.ShouldBe(battleMap); // Verify the map was set
    }

    [Fact]
    public async Task InitializeLobby_SubscribesLoggerAndLogsOnReceivedCommand()
    {
        // Arrange
        var logger = Substitute.For<ICommandLogger>();
        _commandLoggerFactory.CreateLogger(_localizationService, _serverGame).Returns(logger);
        Action<IGameCommand>? capturedHandler = null;
        _commandPublisher
            .When(cp => cp.Subscribe(Arg.Any<Action<IGameCommand>>(), Arg.Any<ITransportPublisher>()))
            .Do(ci => capturedHandler = ci.Arg<Action<IGameCommand>>());

        // Act
        await _sut.InitializeLobby();

        // Assert subscription and factory usage
        _commandLoggerFactory.Received(1).CreateLogger(_localizationService, _serverGame);
        _commandPublisher.Received()
            .Subscribe(Arg.Any<Action<IGameCommand>>(), Arg.Any<ITransportPublisher>());
        capturedHandler.ShouldNotBeNull();

        // When publisher invokes handler, logger.Log should be called
        var cmd = Substitute.For<IGameCommand>();
        capturedHandler!(cmd);
        logger.Received(1).Log(cmd);
    }

    [Fact]
    public async Task InitializeLobby_SafeLog_SwallowsLoggerExceptions()
    {
        // Arrange
        var logger = Substitute.For<ICommandLogger>();
        _commandLoggerFactory.CreateLogger(_localizationService, _serverGame).Returns(logger);
        Action<IGameCommand>? capturedHandler = null;
        _commandPublisher
            .When(cp => cp.Subscribe(Arg.Any<Action<IGameCommand>>(), Arg.Any<ITransportPublisher>()))
            .Do(ci => capturedHandler = ci.Arg<Action<IGameCommand>>());
        await _sut.InitializeLobby();
        capturedHandler.ShouldNotBeNull();
        var cmd = Substitute.For<IGameCommand>();
        logger
            .When(l => l.Log(cmd))
            .Do(_ => throw new Exception("Logger failure"));

        // Act & Assert: handler should not throw despite logger throwing
        Should.NotThrow(() => capturedHandler!(cmd));
        logger.Received(1).Log(cmd);
    }

    [Fact]
    public async Task InitializeLobby_CalledMultipleTimes_UnsubscribesLoggerBeforeResubscribing()
    {
        // Arrange
        var logger = Substitute.For<ICommandLogger>();
        _commandLoggerFactory.CreateLogger(_localizationService, _serverGame).Returns(logger);

        // Act
        await _sut.InitializeLobby();
        await _sut.InitializeLobby();

        // Assert
        _commandPublisher.Received()
            .Subscribe(Arg.Any<Action<IGameCommand>>(), Arg.Any<ITransportPublisher>());
        _commandLoggerFactory.Received(2).CreateLogger(_localizationService, _serverGame);
        _commandPublisher.Received().Unsubscribe(Arg.Any<Action<IGameCommand>>());
    }

    [Fact]
    public async Task Dispose_DisposesCommandLogger()
    {
        // Arrange
        var logger = Substitute.For<ICommandLogger>();
        _commandLoggerFactory.CreateLogger(_localizationService, _serverGame).Returns(logger);
        await _sut.InitializeLobby();

        // Act
        _sut.Dispose();

        // Assert
        logger.Received(1).Dispose();
    }

    [Fact]
    public async Task InitializeLobbyOnline_WhenRelayNotConfigured_SetsConfigurationError()
    {
        var sut = CreateSutWithNullHost();

        await sut.InitializeLobbyOnline();

        sut.OnlineError.ShouldNotBeNull();
        sut.OnlineError!.Code.ShouldBe(RelayClientErrorCode.ConfigurationError);
        sut.RoomCode.ShouldBeNull();
        sut.IsOnlineServerRunning.ShouldBeFalse();
        _gameFactory.DidNotReceive().CreateServerGame(_commandPublisher);
    }

    [Fact]
    public async Task InitializeLobbyOnline_WhenActiveBaseUrlIsBlank_SetsConfigurationError()
    {
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        var relayPublisherFactory = Substitute.For<IRelayPublisherFactory>();
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory, baseUrl: "   ");

        await sut.InitializeLobbyOnline();

        sut.OnlineError.ShouldNotBeNull();
        sut.OnlineError!.Code.ShouldBe(RelayClientErrorCode.ConfigurationError);
        sut.RoomCode.ShouldBeNull();
        sut.IsOnlineServerRunning.ShouldBeFalse();
        await relayRoomClient.DidNotReceive().Create(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeLobbyOnline_WhenCreateRoomFails_SetsErrorAndDisposesServerGame()
    {
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        var relayPublisherFactory = Substitute.For<IRelayPublisherFactory>();
        var error = new RelayClientError(RelayClientErrorCode.HubAtCapacity, "Hub is full");
        relayRoomClient.Create(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(RoomCreateResult.Failed(error));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);

        await sut.InitializeLobbyOnline();

        sut.OnlineError.ShouldBe(error);
        sut.RoomCode.ShouldBeNull();
        sut.IsOnlineServerRunning.ShouldBeFalse();
        sut.ServerGameId.ShouldBeNull();
        _gameFactory.Received(1).CreateServerGame(_commandPublisher);
        await relayRoomClient.DidNotReceive().Ready(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeLobbyOnline_WhenCreateRoomSucceeds_StartsOnlineServerAndSetsRoomCode()
    {
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        var relayPublisherFactory = Substitute.For<IRelayPublisherFactory>();
        var networkHostService = Substitute.For<INetworkHostService>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>())
            .Returns(RoomCreateResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Succeeded());
        var publisher = CreateRelayPublisher(roomCode, sessionToken, hostGameId);
        relayPublisherFactory.Create("http://hub.local/hubs/relay", roomCode, sessionToken, hostGameId, "api-key", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(publisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory, networkHostService);

        await sut.InitializeLobbyOnline();

        sut.RoomCode.ShouldBe(roomCode);
        sut.IsOnlineServerRunning.ShouldBeTrue();
        sut.OnlineError.ShouldBeNull();
        _gameFactory.Received(1).CreateServerGame(_commandPublisher);
        _transportAdapter.TransportPublishers.ShouldContain(publisher);
        await relayRoomClient.Received(1).Ready(roomCode, sessionToken, Arg.Any<CancellationToken>());
        await networkHostService.DidNotReceive().Start();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task InitializeLobbyOnline_WhenReadyFails_ClearsStateAndDisposesServerGame()
    {
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        var relayPublisherFactory = Substitute.For<IRelayPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>())
            .Returns(RoomCreateResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        var readyError = new RelayClientError(RelayClientErrorCode.HostNotReady, "Host did not confirm ready");
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Failed(readyError));
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Succeeded());
        var publisher = CreateRelayPublisher(roomCode, sessionToken, hostGameId);
        relayPublisherFactory.Create("http://hub.local/hubs/relay", roomCode, sessionToken, hostGameId, "api-key", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(publisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);

        await sut.InitializeLobbyOnline();

        sut.OnlineError.ShouldBe(readyError);
        sut.RoomCode.ShouldBeNull();
        sut.IsOnlineServerRunning.ShouldBeFalse();
        sut.ServerGameId.ShouldBeNull();
        _transportAdapter.TransportPublishers.ShouldNotContain(publisher);
        // Verify CloseAsync was called as best-effort cleanup
        await relayRoomClient.Received(1).Close(roomCode, sessionToken, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeLobbyOnline_WhenPublisherFactoryThrows_SetsErrorAndCleansUp()
    {
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        var relayPublisherFactory = Substitute.For<IRelayPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var playerId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>())
            .Returns(RoomCreateResult.Succeeded(roomCode, sessionToken, "Host", playerId, hostId));
        relayPublisherFactory.Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("boom"));
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Succeeded());
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);

        await sut.InitializeLobbyOnline();

        sut.OnlineError.ShouldNotBeNull();
        sut.OnlineError!.Code.ShouldBe(RelayClientErrorCode.Unknown);
        sut.RoomCode.ShouldBeNull();
        sut.IsOnlineServerRunning.ShouldBeFalse();
        sut.ServerGameId.ShouldBeNull();
        // Verify CloseAsync was called as best-effort cleanup
        await relayRoomClient.Received(1).Close(roomCode, sessionToken, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeLobbyOnline_WhenCloseThrowsDuringFailureCleanup_SwallowsAndCleansUp()
    {
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        var relayPublisherFactory = Substitute.For<IRelayPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>())
            .Returns(RoomCreateResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        var readyError = new RelayClientError(RelayClientErrorCode.HostNotReady, "Host did not confirm ready");
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Failed(readyError));
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("relay unreachable"));
        var publisher = CreateRelayPublisher(roomCode, sessionToken, hostGameId);
        relayPublisherFactory.Create("http://hub.local/hubs/relay", roomCode, sessionToken, hostGameId, "api-key", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(publisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);

        await sut.InitializeLobbyOnline();

        sut.OnlineError.ShouldBe(readyError);
        sut.RoomCode.ShouldBeNull();
        sut.IsOnlineServerRunning.ShouldBeFalse();
        sut.ServerGameId.ShouldBeNull();
        _transportAdapter.TransportPublishers.ShouldNotContain(publisher);
        await relayRoomClient.Received(1).Close(roomCode, sessionToken, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dispose_WhenOnlineServerRunning_DisposesRelayPublisher()
    {
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        var relayPublisherFactory = Substitute.For<IRelayPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>())
            .Returns(RoomCreateResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Succeeded());
        var publisher = CreateRelayPublisher(roomCode, sessionToken, hostGameId);
        relayPublisherFactory.Create("http://hub.local/hubs/relay", roomCode, sessionToken, hostGameId, "api-key", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(publisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);
        await sut.InitializeLobbyOnline();
        sut.IsOnlineServerRunning.ShouldBeTrue();

        await sut.DisposeAsync();

        sut.IsOnlineServerRunning.ShouldBeFalse();
        publisher.State.ShouldBe(HubConnectionState.Disconnected);
        _transportAdapter.TransportPublishers.ShouldNotContain(publisher);
    }

    [Fact]
    public async Task InitializeLobbyOnline_CalledTwice_RemovesPreviousPublisherBeforeInstallingNewOne()
    {
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        var relayPublisherFactory = Substitute.For<IRelayPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>())
            .Returns(RoomCreateResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Succeeded());
        var firstPublisher = CreateRelayPublisher(roomCode, sessionToken, hostGameId);
        var secondPublisher = CreateRelayPublisher(roomCode, sessionToken, hostGameId);
        relayPublisherFactory.Create("http://hub.local/hubs/relay", roomCode, sessionToken, hostGameId, "api-key", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(firstPublisher), Task.FromResult(secondPublisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);

        await sut.InitializeLobbyOnline();
        _transportAdapter.TransportPublishers.ShouldContain(firstPublisher);

        await sut.InitializeLobbyOnline();

        _transportAdapter.TransportPublishers.ShouldNotContain(firstPublisher);
        _transportAdapter.TransportPublishers.ShouldContain(secondPublisher);
        firstPublisher.State.ShouldBe(HubConnectionState.Disconnected);
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task InitializeLobbyOnline_WhenCancelled_CleansUpAndRethrows()
    {
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        var relayPublisherFactory = Substitute.For<IRelayPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>())
            .Returns(RoomCreateResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Throws<OperationCanceledException>();
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Succeeded());
        var publisher = CreateRelayPublisher(roomCode, sessionToken, hostGameId);
        relayPublisherFactory.Create("http://hub.local/hubs/relay", roomCode, sessionToken, hostGameId, "api-key", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(publisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => sut.InitializeLobbyOnline(cts.Token));

        sut.RoomCode.ShouldBeNull();
        sut.ServerGameId.ShouldBeNull();
        sut.IsOnlineServerRunning.ShouldBeFalse();
        sut.OnlineError.ShouldBeNull();
        _transportAdapter.TransportPublishers.ShouldNotContain(publisher);
        // Verify CloseAsync was called as best-effort cleanup
        await relayRoomClient.Received(1).Close(roomCode, sessionToken, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeLobbyOnline_CalledTwice_WhenRemovePublisherThrows_SwallowsAndContinues()
    {
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        var relayPublisherFactory = Substitute.For<IRelayPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>())
            .Returns(RoomCreateResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Succeeded());
        var firstPublisher = CreateRelayPublisher(roomCode, sessionToken, hostGameId);
        var secondPublisher = CreateRelayPublisher(roomCode, sessionToken, hostGameId);
        relayPublisherFactory.Create("http://hub.local/hubs/relay", roomCode, sessionToken, hostGameId, "api-key", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(firstPublisher), Task.FromResult(secondPublisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);

        await sut.InitializeLobbyOnline();
        sut.IsOnlineServerRunning.ShouldBeTrue();

        // Make RemovePublisher throw so the swallow-catch in RemoveAndDisposeOnlinePublisherAsync runs
        var throwingAdapter = Substitute.For<ICommandTransportAdapter>();
        throwingAdapter.When(x => x.RemovePublisher(Arg.Any<ITransportPublisher>()))
            .Do(_ => throw new InvalidOperationException("boom"));
        _commandPublisher.Adapter.Returns(throwingAdapter);

        await Should.NotThrowAsync(() => sut.InitializeLobbyOnline());

        sut.RoomCode.ShouldBe(roomCode);
        sut.IsOnlineServerRunning.ShouldBeTrue();
        firstPublisher.State.ShouldBe(HubConnectionState.Disconnected);
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task CloseOnlineRoomAsync_WhenNoOnlineRoomActive_ReturnsTrue()
    {
        // Arrange
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        var relayPublisherFactory = Substitute.For<IRelayPublisherFactory>();
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);

        // Act
        var result = await sut.CloseOnlineRoom();

        // Assert
        result.ShouldBeTrue();
        await relayRoomClient.DidNotReceive().Close(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CloseOnlineRoomAsync_WhenOnlineRoomActive_ClosesRoomAndClearsStateAndReturnsTrue()
    {
        // Arrange
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        var relayPublisherFactory = Substitute.For<IRelayPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>())
            .Returns(RoomCreateResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Succeeded());
        var publisher = CreateRelayPublisher(roomCode, sessionToken, hostGameId);
        relayPublisherFactory.Create("http://hub.local/hubs/relay", roomCode, sessionToken, hostGameId, "api-key", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(publisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);
        await sut.InitializeLobbyOnline();
        sut.RoomCode.ShouldBe(roomCode);

        // Act
        var result = await sut.CloseOnlineRoom();

        // Assert
        result.ShouldBeTrue();
        await relayRoomClient.Received(1).Close(roomCode, sessionToken, Arg.Any<CancellationToken>());
        sut.RoomCode.ShouldBeNull();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task CloseOnlineRoomAsync_CalledTwice_OnlyClosesRoomOnce()
    {
        // Arrange
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        var relayPublisherFactory = Substitute.For<IRelayPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>())
            .Returns(RoomCreateResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Succeeded());
        var publisher = CreateRelayPublisher(roomCode, sessionToken, hostGameId);
        relayPublisherFactory.Create("http://hub.local/hubs/relay", roomCode, sessionToken, hostGameId, "api-key", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(publisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);
        await sut.InitializeLobbyOnline();

        // Act - close twice; second call should be a no-op (room already closed / state cleared)
        var result1 = await sut.CloseOnlineRoom();
        var result2 = await sut.CloseOnlineRoom();

        // Assert
        result1.ShouldBeTrue();
        result2.ShouldBeTrue();
        await relayRoomClient.Received(1).Close(roomCode, sessionToken, Arg.Any<CancellationToken>());
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task InitializeLobbyOnline_WhenCloseOnlineRoomFails_AbortsAndSetsError()
    {
        // Arrange
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        var relayPublisherFactory = Substitute.For<IRelayPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>())
            .Returns(RoomCreateResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        var publisher = CreateRelayPublisher(roomCode, sessionToken, hostGameId);
        relayPublisherFactory.Create("http://hub.local/hubs/relay", roomCode, sessionToken, hostGameId, "api-key", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(publisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);
        await sut.InitializeLobbyOnline();
        sut.RoomCode.ShouldBe(roomCode);

        // Act - try to initialize again, which should try to close the existing room and fail
        await sut.InitializeLobbyOnline();

        // Assert - should have set an error and not cleared the room code
        sut.OnlineError.ShouldNotBeNull();
        sut.OnlineError!.Code.ShouldBe(RelayClientErrorCode.Unknown);
        sut.RoomCode.ShouldBe(roomCode);
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task CloseOnlineRoomAsync_WhenCloseThrows_ReturnsFalseAndDoesNotClearState()
    {
        // Arrange
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        var relayPublisherFactory = Substitute.For<IRelayPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>())
            .Returns(RoomCreateResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        var publisher = CreateRelayPublisher(roomCode, sessionToken, hostGameId);
        relayPublisherFactory.Create("http://hub.local/hubs/relay", roomCode, sessionToken, hostGameId, "api-key", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(publisher));
        var logger = Substitute.For<ILogger<GameManager>>();
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory, logger: logger);
        await sut.InitializeLobbyOnline();

        // Act & Assert - close should return false and not throw
        var result = await sut.CloseOnlineRoom();
        result.ShouldBeFalse();

        // State is not cleared since the close call failed, allowing a retry
        sut.RoomCode.ShouldBe(roomCode);
        logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
        await sut.DisposeAsync();
    }

[Fact]
    public async Task CloseOnlineRoomAsync_WhenCancelled_ReturnsFalseAndDoesNotClearState()
    {
        // Arrange
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        var relayPublisherFactory = Substitute.For<IRelayPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>())
            .Returns(RoomCreateResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        var publisher = CreateRelayPublisher(roomCode, sessionToken, hostGameId);
        relayPublisherFactory.Create("http://hub.local/hubs/relay", roomCode, sessionToken, hostGameId, "api-key", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(publisher));
        var logger = Substitute.For<ILogger<GameManager>>();
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory, logger: logger);
        await sut.InitializeLobbyOnline();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert - a cancelled close should return false without throwing
        var result = await sut.CloseOnlineRoom(cts.Token);
        result.ShouldBeFalse();

        // State is preserved so the close can be retried once the token is usable again
        sut.RoomCode.ShouldBe(roomCode);
        await relayRoomClient.Received(1).Close(roomCode, sessionToken, Arg.Any<CancellationToken>());
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task CloseOnlineRoomAsync_WhenCloseReturnsFailed_ReturnsFalseAndDoesNotClearState()
    {
        // Arrange
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        var relayPublisherFactory = Substitute.For<IRelayPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>())
            .Returns(RoomCreateResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Succeeded());
        var closeError = new RelayClientError(RelayClientErrorCode.Unknown, "Close failed");
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Failed(closeError));
        var publisher = CreateRelayPublisher(roomCode, sessionToken, hostGameId);
        relayPublisherFactory.Create("http://hub.local/hubs/relay", roomCode, sessionToken, hostGameId, "api-key", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(publisher));
        var logger = Substitute.For<ILogger<GameManager>>();
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory, logger: logger);
        await sut.InitializeLobbyOnline();

        // Act
        var result = await sut.CloseOnlineRoom();

        // Assert - close should return false without clearing state
        result.ShouldBeFalse();
        sut.RoomCode.ShouldBe(roomCode);
        logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception?>(ex => ex == null),
            Arg.Any<Func<object, Exception?, string>>());
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_ClosesOnlineRoomBeforeDisposingPublisher()
    {
        // Arrange
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        var relayPublisherFactory = Substitute.For<IRelayPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>())
            .Returns(RoomCreateResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>())
            .Returns(RoomOperationResult.Succeeded());
        var publisher = CreateRelayPublisher(roomCode, sessionToken, hostGameId);
        relayPublisherFactory.Create("http://hub.local/hubs/relay", roomCode, sessionToken, hostGameId, "api-key", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(publisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);
        await sut.InitializeLobbyOnline();

        // Act
        await sut.DisposeAsync();

        // Assert
        await relayRoomClient.Received(1).Close(roomCode, sessionToken, Arg.Any<CancellationToken>());
        sut.RoomCode.ShouldBeNull();
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}

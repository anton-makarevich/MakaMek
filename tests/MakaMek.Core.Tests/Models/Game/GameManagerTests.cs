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
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.Transport.SignalR.Client.Relay;
using Sanet.Transport;
using Sanet.Transport.SignalR.Client.Factories;
using Sanet.Transport.SignalR.Client.Publishers;
using Shouldly;
using Sanet.MakaMek.Core.Services.Logging.Factories;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Services.Logging;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Tests.Models.Game;

public class GameManagerTests : IDisposable
{
    private const string RelayTicketValue = "relay-ticket";

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
        IPublisherFactory relayPublisherFactory,
        INetworkHostService? networkHostService = null,
        ILogger<GameManager>? logger = null,
        string baseUrl = "http://hub.local",
        string apiKey = "api-key",
        IRelayHubConfigurationProvider? hubConfigurationProvider = null)
    {
        var provider = hubConfigurationProvider ?? Substitute.For<IRelayHubConfigurationProvider>();
        if (hubConfigurationProvider is null)
        {
            provider.GetActiveOptions().Returns(Task.FromResult(new RelayClientOptions
            {
                BaseUrl = baseUrl,
                ApiKey = apiKey
            }));
        }
        return new GameManager(
            _commandPublisher,
            _gameFactory,
            _localizationService,
            _commandLoggerFactory,
            logger ?? _logger,
            networkHostService,
            relayRoomClient,
            relayPublisherFactory,
            provider);
    }

    private static RelayClientPublisher CreateRelayPublisher(string roomCode, string relayTicket) =>
        new("http://hub.local/hubs/relay", roomCode, relayTicket, NullLogger<RelayClientPublisher>.Instance);

    private static RelayPublisherOptions RelayOptions(string roomCode) => new()
    {
        HubUrl = "http://hub.local/hubs/relay",
        RoomCode = roomCode,
        RelayTicket = RelayTicketValue
    };

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
        Should.NotThrow(sutWithNullHost.Dispose);
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
    public async Task Dispose_WhenRemoveLanPublisherThrows_SwallowsAndContinues()
    {
        // Arrange - InitializeLobby sets _lanPublisher via network host
        var networkPublisher = Substitute.For<ITransportPublisher>();
        _networkHostService.CanStart.Returns(true);
        _networkHostService.IsRunning.Returns(false);
        _networkHostService.Publisher.Returns(networkPublisher);
        await _sut.InitializeLobby();

        // Make RemovePublisher throw
        var throwingAdapter = Substitute.For<ICommandTransportAdapter>();
        throwingAdapter.When(x => x.RemovePublisher(Arg.Any<ITransportPublisher>()))
            .Do(_ => throw new InvalidOperationException("adapter disposed"));
        _commandPublisher.Adapter.Returns(throwingAdapter);

        // Act & Assert - should not throw despite RemovePublisher failing
        Should.NotThrow(() => _sut.Dispose());
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
        _serverGame.TurnPhase.ShouldBe(PhaseNames.Start); // SetBattleMap must not transition
    }

    [Fact]
    public async Task TryStartGame_DelegatesToServerGame()
    {
        // Arrange
        await _sut.InitializeLobby();
        var battleMap = new BattleMap(10, 10);
        var playerId = Guid.NewGuid();

        // The host player joins and becomes ready so the transition gate is met
        _serverGame.HandleCommand(new JoinGameCommand
        {
            PlayerId = playerId,
            PlayerName = "Host",
            GameOriginId = Guid.NewGuid(),
            Units = [],
            Tint = "#FF0000",
            PilotAssignments = []
        });
        _serverGame.HandleCommand(new UpdatePlayerStatusCommand
        {
            PlayerId = playerId,
            GameOriginId = Guid.NewGuid(),
            PlayerStatus = PlayerStatus.Ready
        });

        // Act - the map is set (broadcast only, no transition)...
        _sut.SetBattleMap(battleMap);
        _serverGame.TurnPhase.ShouldBe(PhaseNames.Start);

        // ...and TryStartGame is the explicit trigger for the phase transition
        _sut.TryStartGame();

        // Assert - the manager delegates to the server game which moves Start → Deployment
        _serverGame.TurnPhase.ShouldBe(PhaseNames.Deployment);
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
        await _sut.DisposeAsync();

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
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory, baseUrl: "   ");

        await sut.InitializeLobbyOnline();

        sut.OnlineError.ShouldNotBeNull();
        sut.OnlineError!.Code.ShouldBe(RelayClientErrorCode.ConfigurationError);
        sut.RoomCode.ShouldBeNull();
        sut.IsOnlineServerRunning.ShouldBeFalse();
        await relayRoomClient.DidNotReceive().Create(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>());
    }

    [Fact]
    public async Task InitializeLobbyOnline_WhenCreateRoomFails_SetsErrorAndDisposesServerGame()
    {
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        var error = new RelayClientError(RelayClientErrorCode.HubAtCapacity, "Hub is full");
        relayRoomClient.Create(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Failed(error));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);

        await sut.InitializeLobbyOnline();

        sut.OnlineError.ShouldBe(error);
        sut.RoomCode.ShouldBeNull();
        sut.IsOnlineServerRunning.ShouldBeFalse();
        sut.ServerGameId.ShouldBeNull();
        _gameFactory.Received(1).CreateServerGame(_commandPublisher);
        await relayRoomClient.DidNotReceive().Ready(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>());
    }

    [Fact]
    public async Task InitializeLobbyOnline_WhenCreateRoomSucceeds_StartsOnlineServerAndSetsRoomCode()
    {
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        var networkHostService = Substitute.For<INetworkHostService>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory, networkHostService);

        await sut.InitializeLobbyOnline();

        sut.RoomCode.ShouldBe(roomCode);
        sut.IsOnlineServerRunning.ShouldBeTrue();
        sut.OnlineError.ShouldBeNull();
        _gameFactory.Received(1).CreateServerGame(_commandPublisher);
        _transportAdapter.TransportPublishers.ShouldContain(publisher);
        await relayRoomClient.Received(1).Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>());
        await networkHostService.DidNotReceive().Start();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task InitializeLobbyOnline_WhenActiveHubChangesDuringInit_PinsRoomLifecycleToOriginallySelectedHub()
    {
        // Arrange
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        var originalOptions = new RelayClientOptions { BaseUrl = "http://hub.local", ApiKey = "api-key" };
        var changedOptions = new RelayClientOptions { BaseUrl = "http://other-hub.local", ApiKey = "other-key" };
        var hubConfigurationProvider = Substitute.For<IRelayHubConfigurationProvider>();
        hubConfigurationProvider.GetActiveOptions()
            .Returns(Task.FromResult(originalOptions), Task.FromResult(changedOptions));
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory, hubConfigurationProvider: hubConfigurationProvider);

        // Act - host a room, then close it; the active hub changes between provider reads
        await sut.InitializeLobbyOnline();
        await sut.CloseOnlineRoom();

        // Assert - Create, Ready, Close and the publisher all stay on the hub that was
        // selected when hosting began, even though the provider returns a different value now
        await relayRoomClient.Received(1).Create(_serverGame.Id, Arg.Any<CancellationToken>(), originalOptions);
        await relayRoomClient.Received(1).Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), originalOptions);
        await relayRoomClient.Received(1).Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), originalOptions);
        await relayPublisherFactory.Received(1)
            .Create(RelayOptions(roomCode), Arg.Any<CancellationToken>());
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task InitializeLobbyOnline_WhenReadyFails_ClearsStateAndDisposesServerGame()
    {
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        var readyError = new RelayClientError(RelayClientErrorCode.HostNotReady, "Host did not confirm ready");
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Failed(readyError));
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);

        await sut.InitializeLobbyOnline();

        sut.OnlineError.ShouldBe(readyError);
        sut.RoomCode.ShouldBeNull();
        sut.IsOnlineServerRunning.ShouldBeFalse();
        sut.ServerGameId.ShouldBeNull();
        _transportAdapter.TransportPublishers.ShouldNotContain(publisher);
        // Verify CloseAsync was called as best-effort cleanup
        await relayRoomClient.Received(1).Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>());
    }

    [Fact]
    public async Task InitializeLobbyOnline_WhenRelayTicketFails_SetsErrorAndCleansUp()
    {
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        var ticketError = new RelayClientError(RelayClientErrorCode.Unknown, "No ticket");
        relayRoomClient.GetRelayTicket(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Failed(ticketError));
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);

        await sut.InitializeLobbyOnline();

        sut.OnlineError.ShouldBe(ticketError);
        sut.RoomCode.ShouldBeNull();
        sut.IsOnlineServerRunning.ShouldBeFalse();
        sut.ServerGameId.ShouldBeNull();
        await relayPublisherFactory.DidNotReceive().Create(
            Arg.Any<RelayPublisherOptions>(), Arg.Any<CancellationToken>());
        await relayRoomClient.Received(1).Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>());
    }

    [Fact]
    public async Task InitializeLobbyOnline_WhenPublisherFactoryThrows_SetsErrorAndCleansUp()
    {
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var playerId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", playerId, hostId));
        relayPublisherFactory.Create(Arg.Any<RelayPublisherOptions>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("boom"));
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);

        await sut.InitializeLobbyOnline();

        sut.OnlineError.ShouldNotBeNull();
        sut.OnlineError!.Code.ShouldBe(RelayClientErrorCode.Unknown);
        sut.RoomCode.ShouldBeNull();
        sut.IsOnlineServerRunning.ShouldBeFalse();
        sut.ServerGameId.ShouldBeNull();
        // Verify CloseAsync was called as best-effort cleanup
        await relayRoomClient.Received(1).Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>());
    }

    [Fact]
    public async Task InitializeLobbyOnline_WhenCloseThrowsDuringFailureCleanup_SwallowsAndCleansUp()
    {
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        var readyError = new RelayClientError(RelayClientErrorCode.HostNotReady, "Host did not confirm ready");
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Failed(readyError));
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .ThrowsAsync(new HttpRequestException("relay unreachable"));
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);

        await sut.InitializeLobbyOnline();

        sut.OnlineError.ShouldBe(readyError);
        sut.RoomCode.ShouldBeNull();
        sut.IsOnlineServerRunning.ShouldBeFalse();
        sut.ServerGameId.ShouldBeNull();
        _transportAdapter.TransportPublishers.ShouldNotContain(publisher);
        await relayRoomClient.Received(1).Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>());
    }

    [Fact]
    public async Task Dispose_WhenOnlineServerRunning_DisposesRelayPublisher()
    {
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));
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
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        var firstPublisher = CreateRelayPublisher(roomCode, sessionToken);
        var secondPublisher = CreateRelayPublisher(roomCode, sessionToken);
        relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(firstPublisher), Task.FromResult<ITransportPublisher>(secondPublisher));
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
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Throws<OperationCanceledException>();
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));
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
        await relayRoomClient.Received(1).Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>());
    }

    [Fact]
    public async Task InitializeLobbyOnline_CalledTwice_WhenRemovePublisherThrows_SwallowsAndContinues()
    {
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        var firstPublisher = CreateRelayPublisher(roomCode, sessionToken);
        var secondPublisher = CreateRelayPublisher(roomCode, sessionToken);
        relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(firstPublisher), Task.FromResult<ITransportPublisher>(secondPublisher));
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
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);

        // Act
        var result = await sut.CloseOnlineRoom();

        // Assert
        result.ShouldBeTrue();
        await relayRoomClient.DidNotReceive().Close(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>());
    }

    [Fact]
    public async Task CloseOnlineRoomAsync_WhenOnlineRoomActive_ClosesRoomAndClearsStateAndReturnsTrue()
    {
        // Arrange
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);
        await sut.InitializeLobbyOnline();
        sut.RoomCode.ShouldBe(roomCode);

        // Act
        var result = await sut.CloseOnlineRoom();

        // Assert
        result.ShouldBeTrue();
        await relayRoomClient.Received(1).Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>());
        sut.RoomCode.ShouldBeNull();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task CloseOnlineRoomAsync_CalledTwice_OnlyClosesRoomOnce()
    {
        // Arrange
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);
        await sut.InitializeLobbyOnline();

        // Act - close twice; second call should be a no-op (room already closed / state cleared)
        var result1 = await sut.CloseOnlineRoom();
        var result2 = await sut.CloseOnlineRoom();

        // Assert
        result1.ShouldBeTrue();
        result2.ShouldBeTrue();
        await relayRoomClient.Received(1).Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>());
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task InitializeLobbyOnline_WhenCloseOnlineRoomFails_AbortsAndSetsError()
    {
        // Arrange
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));
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
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));
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
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .ThrowsAsync(new OperationCanceledException());
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));
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
        await relayRoomClient.Received(1).Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>());
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task CloseOnlineRoomAsync_WhenCloseReturnsFailed_ReturnsFalseAndDoesNotClearState()
    {
        // Arrange
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        var closeError = new RelayClientError(RelayClientErrorCode.Unknown, "Close failed");
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Failed(closeError));
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));
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
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);
        await sut.InitializeLobbyOnline();

        // Act
        await sut.DisposeAsync();

        // Assert
        await relayRoomClient.Received(1).Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>());
        sut.RoomCode.ShouldBeNull();
    }

    [Fact]
    public async Task DisposeAsync_WhenPublisherDisposeAsyncThrows_SwallowsException()
    {
        // Arrange
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        var throwingPublisher = Substitute.For<ITransportPublisher, IAsyncDisposable>();
        ((IAsyncDisposable)throwingPublisher).When(x => x.DisposeAsync())
            .Throw(new InvalidOperationException("dispose boom"));
        relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(throwingPublisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory);
        await sut.InitializeLobbyOnline();
        sut.IsOnlineServerRunning.ShouldBeTrue();

        // Act & Assert - DisposeAsync should not throw even though the publisher's dispose does
        await Should.NotThrowAsync(() => sut.DisposeAsync().AsTask());
        await ((IAsyncDisposable)throwingPublisher).Received(1).DisposeAsync();
        sut.IsOnlineServerRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task InitializeLobby_ThenOnline_RemovesLanPublisherAndStopsHost()
    {
        // Arrange - create a SUT with both relay and network host support
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        var relayPublisher = CreateRelayPublisher(roomCode, relayTicket: RelayTicketValue);
        relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(relayPublisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory, _networkHostService);

        // Start in LAN mode
        var networkPublisher = Substitute.For<ITransportPublisher>();
        _networkHostService.CanStart.Returns(true);
        _networkHostService.IsRunning.Returns(false);
        _networkHostService.Publisher.Returns(networkPublisher);
        await sut.InitializeLobby();
        _transportAdapter.TransportPublishers.ShouldContain(networkPublisher);
        // Simulate host has started
        _networkHostService.IsRunning.Returns(true);

        // Act - transition to online
        await sut.InitializeLobbyOnline();

        // Assert - LAN publisher removed, relay publisher added, host stopped
        _transportAdapter.TransportPublishers.ShouldNotContain(networkPublisher);
        _transportAdapter.TransportPublishers.ShouldContain(relayPublisher);
        await _networkHostService.Received(1).Stop();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task InitializeLobbyOnline_ThenLan_RemovesRelayPublisherAndAddsLan()
    {
        // Arrange - set up online first
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        var relayPublisher = CreateRelayPublisher(roomCode, relayTicket: RelayTicketValue);
        relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(relayPublisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory, _networkHostService);

        await sut.InitializeLobbyOnline();
        _transportAdapter.TransportPublishers.ShouldContain(relayPublisher);

        // Arrange - set up LAN publisher
        var lanPublisher = Substitute.For<ITransportPublisher>();
        _networkHostService.CanStart.Returns(true);
        _networkHostService.IsRunning.Returns(false);
        _networkHostService.Publisher.Returns(lanPublisher);

        // Act
        await sut.InitializeLobby();

        // Assert
        _transportAdapter.TransportPublishers.ShouldNotContain(relayPublisher);
        _transportAdapter.TransportPublishers.ShouldContain(lanPublisher);
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task InitializeLobby_ThenOnline_AdapterContainsLocalAndRelayPublishersOnly()
    {
        // Arrange - create a SUT with both relay and network host support
        var relayRoomClient = Substitute.For<IRelayRoomClient>();
        relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        var relayPublisherFactory = Substitute.For<IPublisherFactory>();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var deviceSessionId = Guid.NewGuid();
        var hostGameId = Guid.NewGuid();
        relayRoomClient.Create(_serverGame.Id, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Host", deviceSessionId, hostGameId));
        relayRoomClient.Ready(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        relayRoomClient.Close(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RoomOperationResult.Succeeded());
        var relayPublisher = CreateRelayPublisher(roomCode, relayTicket: RelayTicketValue);
        relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(relayPublisher));
        var sut = CreateSutWithRelay(relayRoomClient, relayPublisherFactory, _networkHostService);

        // Start in LAN mode
        var networkPublisher = Substitute.For<ITransportPublisher>();
        _networkHostService.CanStart.Returns(true);
        _networkHostService.IsRunning.Returns(false);
        _networkHostService.Publisher.Returns(networkPublisher);
        await sut.InitializeLobby();
        _transportAdapter.TransportPublishers.ShouldContain(networkPublisher);
        // Simulate host has started
        _networkHostService.IsRunning.Returns(true);

        // Act - transition to online
        await sut.InitializeLobbyOnline();

        // Assert - only the local-loopback publisher and relay publisher remain;
        // the LAN publisher was removed, guaranteeing single delivery of host-local commands
        _transportAdapter.TransportPublishers.ShouldNotContain(networkPublisher);
        _transportAdapter.TransportPublishers.ShouldContain(relayPublisher);
        _transportAdapter.TransportPublishers.Count.ShouldBe(2); // initial local + relay
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task RemoveLanPublisher_WhenRemovePublisherThrows_LogsDebugAndDoesNotThrow()
    {
        // Arrange - fully isolated setup
        var commandPublisher = Substitute.For<ICommandPublisher>();
        var initialPublisher = Substitute.For<ITransportPublisher>();
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger<CommandTransportAdapter>().Returns(Substitute.For<ILogger<CommandTransportAdapter>>());
        var adapter = new CommandTransportAdapter(loggerFactory, [initialPublisher]);
        commandPublisher.Adapter.Returns(adapter);
        var gameFactory = Substitute.For<IGameFactory>();
        var networkHostService = Substitute.For<INetworkHostService>();
        var logger = Substitute.For<ILogger<GameManager>>();
        var gameManager = new GameManager(
            commandPublisher, gameFactory, _localizationService, _commandLoggerFactory,
            logger, networkHostService);

        var networkPublisher = Substitute.For<ITransportPublisher>();
        networkHostService.CanStart.Returns(true);
        networkHostService.IsRunning.Returns(false);
        networkHostService.Publisher.Returns(networkPublisher);
        await gameManager.InitializeLobby();
        adapter.TransportPublishers.ShouldContain(networkPublisher);

        // Now make the adapter throw on RemovePublisher
        var throwingAdapter = Substitute.For<ICommandTransportAdapter>();
        throwingAdapter.TransportPublishers.Returns(adapter.TransportPublishers);
        throwingAdapter.When(x => x.RemovePublisher(Arg.Any<ITransportPublisher>()))
            .Do(_ => throw new InvalidOperationException("adapter boom"));
        commandPublisher.Adapter.Returns(throwingAdapter);

        // Act - InitializeLobby calls ResetForNewGame which calls RemoveLanPublisherAndStopHost
        await Should.NotThrowAsync(() => gameManager.InitializeLobby());

        // Assert
        logger.Received().Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task StopHost_WhenStopThrows_LogsDebugAndDoesNotThrow()
    {
        // Arrange - fully isolated setup
        var commandPublisher = Substitute.For<ICommandPublisher>();
        var initialPublisher = Substitute.For<ITransportPublisher>();
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger<CommandTransportAdapter>().Returns(Substitute.For<ILogger<CommandTransportAdapter>>());
        var adapter = new CommandTransportAdapter(loggerFactory, [initialPublisher]);
        commandPublisher.Adapter.Returns(adapter);
        var gameFactory = Substitute.For<IGameFactory>();
        var networkHostService = Substitute.For<INetworkHostService>();
        var logger = Substitute.For<ILogger<GameManager>>();
        var gameManager = new GameManager(
            commandPublisher, gameFactory, _localizationService, _commandLoggerFactory,
            logger, networkHostService);

        var networkPublisher = Substitute.For<ITransportPublisher>();
        networkHostService.CanStart.Returns(true);
        networkHostService.IsRunning.Returns(false);
        networkHostService.Publisher.Returns(networkPublisher);
        await gameManager.InitializeLobby();
        networkHostService.IsRunning.Returns(true);
        networkHostService.Stop().ThrowsAsync(new InvalidOperationException("stop boom"));

        // Act & Assert - should not throw even though Stop throws
        await Should.NotThrowAsync(() => gameManager.InitializeLobby());
        logger.Received().Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task DisposeAsync_WhenRemoveLanPublisherThrows_LogsDebugAndDoesNotThrow()
    {
        // Arrange - fully isolated setup
        var commandPublisher = Substitute.For<ICommandPublisher>();
        var initialPublisher = Substitute.For<ITransportPublisher>();
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger<CommandTransportAdapter>().Returns(Substitute.For<ILogger<CommandTransportAdapter>>());
        var adapter = new CommandTransportAdapter(loggerFactory, [initialPublisher]);
        commandPublisher.Adapter.Returns(adapter);
        var gameFactory = Substitute.For<IGameFactory>();
        var networkHostService = Substitute.For<INetworkHostService>();
        var logger = Substitute.For<ILogger<GameManager>>();
        var gameManager = new GameManager(
            commandPublisher, gameFactory, _localizationService, _commandLoggerFactory,
            logger, networkHostService);

        var networkPublisher = Substitute.For<ITransportPublisher>();
        networkHostService.CanStart.Returns(true);
        networkHostService.IsRunning.Returns(false);
        networkHostService.Publisher.Returns(networkPublisher);
        await gameManager.InitializeLobby();
        networkHostService.IsRunning.Returns(true);

        // Make the adapter throw on RemovePublisher
        var throwingAdapter = Substitute.For<ICommandTransportAdapter>();
        throwingAdapter.TransportPublishers.Returns(adapter.TransportPublishers);
        throwingAdapter.When(x => x.RemovePublisher(Arg.Any<ITransportPublisher>()))
            .Do(_ => throw new InvalidOperationException("adapter boom"));
        commandPublisher.Adapter.Returns(throwingAdapter);

        // Act & Assert
        await Should.NotThrowAsync(() => gameManager.DisposeAsync().AsTask());
        logger.Received().Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.Transport.SignalR.Client.Relay;
using Sanet.Transport;
using Sanet.Transport.SignalR.Client.Factories;
using Sanet.Transport.SignalR.Client.Publishers;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game;

public class GameConnectorTests : IDisposable
{
    private const string RelayTicketValue = "relay-ticket";

    private readonly ICommandPublisher _commandPublisher;
    private readonly ICommandTransportAdapter _transportAdapter;
    private readonly ITransportFactory _transportFactory;
    private readonly IRelayRoomClient _relayRoomClient;
    private readonly IPublisherFactory _relayPublisherFactory;
    private readonly ILogger<GameConnector> _logger;
    private readonly GameConnector _sut;

    public GameConnectorTests()
    {
        _commandPublisher = Substitute.For<ICommandPublisher>();
        // Use a substitute for the adapter to allow simulating exceptions in tests
        _transportAdapter = Substitute.For<ICommandTransportAdapter>();
        _commandPublisher.Adapter.Returns(_transportAdapter);

        _transportFactory = Substitute.For<ITransportFactory>();
        _relayRoomClient = Substitute.For<IRelayRoomClient>();
        _relayRoomClient.GetRelayTicket(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Succeeded(RelayTicketValue, DateTimeOffset.UtcNow.AddMinutes(5)));
        _relayPublisherFactory = Substitute.For<IPublisherFactory>();
        _logger = Substitute.For<ILogger<GameConnector>>();

        _sut = new GameConnector(
            _commandPublisher,
            _transportFactory,
            _logger,
            _relayRoomClient,
            _relayPublisherFactory,
            CreateHubConfigurationProvider());
    }

    private static IRelayHubConfigurationProvider CreateHubConfigurationProvider(string baseUrl = "http://hub.local", string apiKey = "api-key")
    {
        var provider = Substitute.For<IRelayHubConfigurationProvider>();
        provider.GetActiveOptions().Returns(Task.FromResult(new RelayClientOptions
        {
            BaseUrl = baseUrl,
            ApiKey = apiKey
        }));
        return provider;
    }

    private GameConnector CreateSutWithoutRelay() => new(
        _commandPublisher,
        _transportFactory,
        _logger);

    private static RelayClientPublisher CreateRelayPublisher(string roomCode, string relayTicket) =>
        new("http://hub.local/hubs/relay", roomCode, relayTicket, NullLogger<RelayClientPublisher>.Instance);

    private static RelayPublisherOptions RelayOptions(string roomCode) => new()
    {
        HubUrl = "http://hub.local/hubs/relay",
        RoomCode = roomCode,
        RelayTicket = RelayTicketValue
    };

    private async Task<RelayClientPublisher> JoinOnlineAsync(GameConnector sut, string roomCode = "ABCDEF")
    {
        var deviceSessionId = Guid.NewGuid();
        const string sessionToken = "session-token";
        var hostGameId = Guid.NewGuid();
        _relayRoomClient.Join(roomCode, sessionToken: null, Arg.Any<CancellationToken>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Client", deviceSessionId, hostGameId));
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        _relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));

        await sut.JoinOnline(roomCode, sessionToken: null);

        return publisher;
    }

    // ---------- LAN connect ----------

    [Fact]
    public async Task ConnectToLanAsync_ConnectsAndAddsPublisher()
    {
        // Arrange
        var publisher = Substitute.For<ITransportPublisher>();
        _transportFactory.CreateAndStartClientPublisher("http://localhost:2439/makamekhub")
            .Returns(Task.FromResult(publisher));

        // Act
        await _sut.ConnectToLan("http://localhost:2439/makamekhub");

        // Assert
        await _transportFactory.Received(1).CreateAndStartClientPublisher("http://localhost:2439/makamekhub");
        await _transportAdapter.Received(1).ClearPublishers();
        _transportAdapter.Received(1).AddPublisher(publisher);
        _sut.IsConnected.ShouldBeTrue();
        _sut.OnlineError.ShouldBeNull();
    }

    [Fact]
    public async Task ConnectToLanAsync_ClearsPreviousPublishers_BeforeAddingNewOne()
    {
        // Arrange
        var firstPublisher = Substitute.For<ITransportPublisher>();
        var secondPublisher = Substitute.For<ITransportPublisher>();
        _transportFactory.CreateAndStartClientPublisher(Arg.Any<string>())
            .Returns(Task.FromResult(firstPublisher), Task.FromResult(secondPublisher));

        // Act
        await _sut.ConnectToLan("http://localhost:2439/makamekhub");
        await _sut.ConnectToLan("http://localhost:2439/makamekhub");

        // Assert
        await _transportAdapter.Received(2).ClearPublishers();
        _transportAdapter.Received(1).AddPublisher(firstPublisher);
        _transportAdapter.Received(1).AddPublisher(secondPublisher);
        _sut.IsConnected.ShouldBeTrue();
    }

    [Fact]
    public async Task ConnectToLanAsync_WhenTransportFactoryThrows_StaysDisconnected()
    {
        // Arrange
        _transportFactory.CreateAndStartClientPublisher(Arg.Any<string>())
            .Returns<Task<ITransportPublisher>>(_ => throw new InvalidOperationException("boom"));

        // Act
        await _sut.ConnectToLan("http://localhost:2439/makamekhub");

        // Assert
        _sut.IsConnected.ShouldBeFalse();
        _transportAdapter.DidNotReceive().AddPublisher(Arg.Any<ITransportPublisher>());
    }

    [Fact]
    public async Task ConnectToLanAsync_WhenPublisherIsAsyncDisposable_DisposesOnFailure()
    {
        // Arrange
        var asyncDisposablePublisher = Substitute.For<ITransportPublisher, IAsyncDisposable>();
        _transportFactory.CreateAndStartClientPublisher(Arg.Any<string>())
            .Returns(Task.FromResult(asyncDisposablePublisher));
        _transportAdapter.When(a => a.AddPublisher(Arg.Any<ITransportPublisher>()))
            .Throw(new InvalidOperationException("add failed"));

        // Act
        await _sut.ConnectToLan("http://localhost:2439/makamekhub");

        // Assert - publisher is disposed asynchronously when adding fails
        await ((IAsyncDisposable)asyncDisposablePublisher).Received(1).DisposeAsync();
        _sut.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public async Task ConnectToLanAsync_WhenDisposeAsyncThrows_SwallowsException()
    {
        // Arrange - create a publisher that throws when disposed
        var throwingPublisher = Substitute.For<ITransportPublisher, IAsyncDisposable>();
        ((IAsyncDisposable)throwingPublisher).When(x => x.DisposeAsync())
            .Throw(new InvalidOperationException("dispose boom"));
        _transportFactory.CreateAndStartClientPublisher(Arg.Any<string>())
            .Returns(Task.FromResult(throwingPublisher));
        _transportAdapter.When(a => a.AddPublisher(Arg.Any<ITransportPublisher>()))
            .Throw(new InvalidOperationException("add failed"));

        // Act & Assert - should not throw
        await Should.NotThrowAsync(() => _sut.ConnectToLan("http://localhost:2439/makamekhub"));
        _sut.IsConnected.ShouldBeFalse();
    }

    // ---------- Online join ----------

    [Fact]
    public async Task JoinOnlineAsync_WhenRelayNotConfigured_SetsConfigurationError()
    {
        // Arrange
        var sut = CreateSutWithoutRelay();

        // Act
        await sut.JoinOnline("ABCDEF", sessionToken: null);

        // Assert
        sut.OnlineError.ShouldNotBeNull();
        sut.OnlineError!.Code.ShouldBe(RelayClientErrorCode.ConfigurationError);
        sut.IsConnected.ShouldBeFalse();
        _transportAdapter.DidNotReceive().AddPublisher(Arg.Any<ITransportPublisher>());
    }

    [Fact]
    public async Task JoinOnlineAsync_WhenActiveBaseUrlIsBlank_SetsConfigurationError()
    {
        // Arrange
        var sut = new GameConnector(
            _commandPublisher,
            _transportFactory,
            _logger,
            _relayRoomClient,
            _relayPublisherFactory,
            CreateHubConfigurationProvider(baseUrl: "   "));

        // Act
        await sut.JoinOnline("ABCDEF", sessionToken: null);

        // Assert
        sut.OnlineError.ShouldNotBeNull();
        sut.OnlineError!.Code.ShouldBe(RelayClientErrorCode.ConfigurationError);
        sut.IsConnected.ShouldBeFalse();
        await _relayRoomClient.DidNotReceive().Join(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinOnlineAsync_WhenJoinFails_SetsErrorAndDoesNotConnect()
    {
        // Arrange
        var error = new RelayClientError(RelayClientErrorCode.RoomNotFound, "Room not found");
        _relayRoomClient.Join("ABCDEF", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(RoomSessionResult.Failed(error));

        // Act
        await _sut.JoinOnline("ABCDEF", sessionToken: null);

        // Assert
        _sut.OnlineError.ShouldBe(error);
        _sut.IsConnected.ShouldBeFalse();
        await _relayPublisherFactory.DidNotReceive().Create(
            Arg.Any<RelayPublisherOptions>(), Arg.Any<CancellationToken>());
        _transportAdapter.DidNotReceive().AddPublisher(Arg.Any<ITransportPublisher>());
    }

    [Fact]
    public async Task JoinOnlineAsync_WhenJoinResultMissingSessionToken_SetsUnknownError()
    {
        // Arrange
        var joinResult = new RoomSessionResult(true, "ABCDEF", null, "Client", Guid.NewGuid(), Guid.NewGuid(), null);
        _relayRoomClient.Join("ABCDEF", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(joinResult);

        // Act
        await _sut.JoinOnline("ABCDEF", sessionToken: null);

        // Assert
        _sut.OnlineError.ShouldNotBeNull();
        _sut.OnlineError!.Code.ShouldBe(RelayClientErrorCode.Unknown);
        _sut.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public async Task JoinOnlineAsync_WhenRelayTicketFails_SetsErrorAndCleansUpMembership()
    {
        // Arrange
        var deviceSessionId = Guid.NewGuid();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var hostGameId = Guid.NewGuid();
        _relayRoomClient.Join(roomCode, sessionToken: null, Arg.Any<CancellationToken>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Client", deviceSessionId, hostGameId));
        var ticketError = new RelayClientError(RelayClientErrorCode.Unknown, "No ticket");
        _relayRoomClient.GetRelayTicket(roomCode, sessionToken, Arg.Any<CancellationToken>(), Arg.Any<RelayClientOptions?>())
            .Returns(RelayTicketResult.Failed(ticketError));

        // Act
        await _sut.JoinOnline(roomCode, sessionToken: null);

        // Assert
        _sut.OnlineError.ShouldBe(ticketError);
        _sut.IsConnected.ShouldBeFalse();
        await _relayPublisherFactory.DidNotReceive().Create(
            Arg.Any<RelayPublisherOptions>(), Arg.Any<CancellationToken>());
        _transportAdapter.DidNotReceive().AddPublisher(Arg.Any<ITransportPublisher>());
        await _relayRoomClient.Received(1).RemoveMember(roomCode, sessionToken, deviceSessionId);
    }

    [Fact]
    public async Task JoinOnlineAsync_WhenJoinSucceeds_AddsPublisherAndConnects()
    {
        // Arrange
        var deviceSessionId = Guid.NewGuid();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var hostGameId = Guid.NewGuid();
        _relayRoomClient.Join(roomCode, sessionToken: null, Arg.Any<CancellationToken>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Client", deviceSessionId, hostGameId));
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        _relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));

        // Act
        await _sut.JoinOnline(roomCode, sessionToken: null);

        // Assert
        await _relayPublisherFactory.Received(1).Create(
            RelayOptions(roomCode), Arg.Any<CancellationToken>());
        await _transportAdapter.Received(1).ClearPublishers();
        _transportAdapter.Received(1).AddPublisher(publisher);
        _transportAdapter.Received(1).RegisterDisconnectHandler(Arg.Any<Action<ITransportPublisher>>());
        _sut.IsConnected.ShouldBeTrue();
        _sut.OnlineError.ShouldBeNull();
    }

    [Fact]
    public async Task JoinOnlineAsync_WhenPublisherCreationFails_SetsNetworkErrorAndStaysDisconnected()
    {
        // Arrange
        var deviceSessionId = Guid.NewGuid();
        _relayRoomClient.Join("ABCDEF", sessionToken: null, Arg.Any<CancellationToken>())
            .Returns(RoomSessionResult.Succeeded("ABCDEF", "session-token", "Client", deviceSessionId, Guid.NewGuid()));
        _relayPublisherFactory.Create(Arg.Any<RelayPublisherOptions>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("boom"));

        // Act
        await _sut.JoinOnline("ABCDEF", sessionToken: null);

        // Assert
        _sut.OnlineError.ShouldNotBeNull();
        _sut.OnlineError!.Code.ShouldBe(RelayClientErrorCode.NetworkError);
        _sut.IsConnected.ShouldBeFalse();
        _transportAdapter.DidNotReceive().AddPublisher(Arg.Any<ITransportPublisher>());
    }

    [Fact]
    public async Task JoinOnlineAsync_WhenCleanupRemovePublisherThrows_SwallowsException()
    {
        // Arrange
        var deviceSessionId = Guid.NewGuid();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var hostGameId = Guid.NewGuid();
        _relayRoomClient.Join(roomCode, sessionToken: null, Arg.Any<CancellationToken>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Client", deviceSessionId, hostGameId));

        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        _relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));

        // Make the adapter throw when adding the publisher to trigger the failure
        // path, then throw again when removing it during cleanup
        _transportAdapter.When(a => a.AddPublisher(publisher))
            .Throw(new InvalidOperationException("add boom"));
        _transportAdapter.When(a => a.RemovePublisher(publisher))
            .Throw(new InvalidOperationException("remove boom"));

        // Act & Assert - should not throw from cleanup
        await Should.NotThrowAsync(() => _sut.JoinOnline(roomCode, sessionToken: null));
        _sut.OnlineError.ShouldNotBeNull();
        _sut.OnlineError!.Code.ShouldBe(RelayClientErrorCode.NetworkError);
        _sut.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveAndDisposeOnlinePublisher_WhenDisposeThrows_LogsWarningAndCompletes()
    {
        // Arrange
        var deviceSessionId = Guid.NewGuid();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var hostGameId = Guid.NewGuid();
        _relayRoomClient.Join(roomCode, sessionToken: null, Arg.Any<CancellationToken>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Client", deviceSessionId, hostGameId));
        var publisher = Substitute.For<ITransportPublisher>();
        publisher.When(x => x.DisposeAsync())
            .Throw(new InvalidOperationException("dispose boom"));
        _relayPublisherFactory.Create(Arg.Any<RelayPublisherOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(publisher));
        // Trigger the failure path so cleanup removes and disposes the created publisher
        _transportAdapter.When(a => a.AddPublisher(Arg.Any<ITransportPublisher>()))
            .Throw(new InvalidOperationException("add boom"));

        // Act & Assert - should not throw and should log a warning about the dispose failure
        await Should.NotThrowAsync(() => _sut.JoinOnline(roomCode, sessionToken: null));
        _sut.OnlineError.ShouldNotBeNull();
        _sut.OnlineError!.Code.ShouldBe(RelayClientErrorCode.NetworkError);
        _sut.IsConnected.ShouldBeFalse();
        _logger.Received(1).LogWarning(
            Arg.Any<InvalidOperationException>(),
            "Failed to dispose relay publisher during cleanup");
    }

    [Fact]
    public async Task JoinOnlineAsync_WhenJoinThrows_CleansUpAndRethrows()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _relayRoomClient.Join("ABCDEF", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.JoinOnline("ABCDEF", sessionToken: null, cts.Token));

        _sut.OnlineError.ShouldBeNull();
        _sut.IsConnected.ShouldBeFalse();
        _transportAdapter.DidNotReceive().AddPublisher(Arg.Any<ITransportPublisher>());
    }

    [Fact]
    public async Task JoinOnlineAsync_WhenCancelledDuringPublisherCreation_DisposesPublisherAndRethrows()
    {
        // Arrange
        var deviceSessionId = Guid.NewGuid();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var hostGameId = Guid.NewGuid();
        _relayRoomClient.Join(roomCode, sessionToken: null, Arg.Any<CancellationToken>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Client", deviceSessionId, hostGameId));
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        using var cts = new CancellationTokenSource();
        _relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));
        _relayPublisherFactory.When(f => f.Create(Arg.Any<RelayPublisherOptions>(), Arg.Any<CancellationToken>()))
            .Do(_ => cts.Cancel());

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.JoinOnline(roomCode, sessionToken: null, cts.Token));

        publisher.State.ToString().ShouldBe("Disconnected");
        _sut.OnlineError.ShouldBeNull();
        _sut.IsConnected.ShouldBeFalse();
        _transportAdapter.DidNotReceive().AddPublisher(Arg.Any<ITransportPublisher>());
        await _relayRoomClient.Received(1).RemoveMember(roomCode, sessionToken, deviceSessionId);
    }

    [Fact]
    public async Task JoinOnlineAsync_WhenCancelledAfterCreateAsync_CleansUpAndRethrows()
    {
        // Arrange
        var deviceSessionId = Guid.NewGuid();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var hostGameId = Guid.NewGuid();
        _relayRoomClient.Join(roomCode, sessionToken: null, Arg.Any<CancellationToken>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Client", deviceSessionId, hostGameId));
        using var cts = new CancellationTokenSource();
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        _relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));

        // Cancel after CreateAsync completes
        _relayPublisherFactory.When(f => f.Create(Arg.Any<RelayPublisherOptions>(), Arg.Any<CancellationToken>()))
            .Do(_ => cts.Cancel());

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.JoinOnline(roomCode, sessionToken: null, cts.Token));

        _sut.IsConnected.ShouldBeFalse();
        _transportAdapter.DidNotReceive().AddPublisher(Arg.Any<ITransportPublisher>());
    }

    [Fact]
    public async Task JoinOnlineAsync_WhenJoinSucceeds_RegistersDisconnectHandler()
    {
        // Arrange
        var mockAdapter = Substitute.For<ICommandTransportAdapter>();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        commandPublisher.Adapter.Returns(mockAdapter);
        var sut = new GameConnector(
            commandPublisher,
            _transportFactory,
            _logger,
            _relayRoomClient,
            _relayPublisherFactory,
            CreateHubConfigurationProvider());

        var deviceSessionId = Guid.NewGuid();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var hostGameId = Guid.NewGuid();
        _relayRoomClient.Join(roomCode, sessionToken: null, Arg.Any<CancellationToken>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Client", deviceSessionId, hostGameId));
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        _relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));

        Action<ITransportPublisher>? disconnectHandler = null;
        mockAdapter.When(a => a.RegisterDisconnectHandler(Arg.Any<Action<ITransportPublisher>>()))
            .Do(ci => disconnectHandler = ci.Arg<Action<ITransportPublisher>>());

        // Act
        await sut.JoinOnline(roomCode, sessionToken: null);

        // Assert
        disconnectHandler.ShouldNotBeNull();
        disconnectHandler.Invoke(publisher);

        // A GameEndedCommand with HostDisconnected reason is dispatched through the local receive path
        mockAdapter.Received(1).DispatchLocalCommand(
            Arg.Is<GameEndedCommand>(c => c.Reason == GameEndReason.HostDisconnected),
            publisher);
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task JoinOnlineAsync_DisconnectHandler_IgnoresOtherPublishers()
    {
        // Arrange
        var mockAdapter = Substitute.For<ICommandTransportAdapter>();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        commandPublisher.Adapter.Returns(mockAdapter);
        var sut = new GameConnector(
            commandPublisher,
            _transportFactory,
            _logger,
            _relayRoomClient,
            _relayPublisherFactory,
            CreateHubConfigurationProvider());

        var deviceSessionId = Guid.NewGuid();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var hostGameId = Guid.NewGuid();
        _relayRoomClient.Join(roomCode, sessionToken: null, Arg.Any<CancellationToken>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Client", deviceSessionId, hostGameId));
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        _relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));

        Action<ITransportPublisher>? disconnectHandler = null;
        mockAdapter.When(a => a.RegisterDisconnectHandler(Arg.Any<Action<ITransportPublisher>>()))
            .Do(ci => disconnectHandler = ci.Arg<Action<ITransportPublisher>>());
        await sut.JoinOnline(roomCode, sessionToken: null);
        disconnectHandler.ShouldNotBeNull();

        // Act - a publisher that is not the active relay publisher reports a disconnect
        disconnectHandler.Invoke(Substitute.For<ITransportPublisher>());

        // Assert - no command is dispatched for unrelated publishers
        mockAdapter.DidNotReceive().DispatchLocalCommand(Arg.Any<IGameCommand>(), Arg.Any<ITransportPublisher>());
        await sut.DisposeAsync();
    }

    // ---------- Disconnect ----------

    [Fact]
    public async Task DisconnectAsync_WhenNothingConnected_DoesNotCallRelayRoomClient()
    {
        // Act
        await _sut.Disconnect();

        // Assert
        await _relayRoomClient.DidNotReceive().RemoveMember(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        _sut.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public async Task DisconnectAsync_WhenOnlineRoomActive_RemovesMemberAndDisposesPublisher()
    {
        // Arrange
        var publisher = await JoinOnlineAsync(_sut);
        _sut.IsConnected.ShouldBeTrue();

        // Act
        await _sut.Disconnect();

        // Assert
        await _relayRoomClient.Received(1).RemoveMember(
            "ABCDEF", "session-token", Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        _transportAdapter.Received(1).RemovePublisher(publisher);
        publisher.State.ToString().ShouldBe("Disconnected");
        _sut.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public async Task DisconnectAsync_WhenRemoveMemberThrows_SwallowsAndCleansUp()
    {
        // Arrange
        var publisher = await JoinOnlineAsync(_sut);
        _relayRoomClient.RemoveMember(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("relay unreachable"));

        // Act & Assert
        await Should.NotThrowAsync(() => _sut.Disconnect());

        _transportAdapter.Received(1).RemovePublisher(publisher);
        publisher.State.ToString().ShouldBe("Disconnected");
        _sut.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public async Task DisconnectAsync_AfterLanConnect_ClearsPublishers()
    {
        // Arrange
        var publisher = Substitute.For<ITransportPublisher>();
        _transportFactory.CreateAndStartClientPublisher(Arg.Any<string>())
            .Returns(Task.FromResult(publisher));
        await _sut.ConnectToLan("http://localhost:2439/makamekhub");
        _sut.IsConnected.ShouldBeTrue();

        // Act
        await _sut.Disconnect();

        // Assert
        await _transportAdapter.Received(2).ClearPublishers();
        _sut.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public async Task DisconnectAsync_WhenClearPublishersThrows_SwallsAndCompletes()
    {
        // Arrange
        await JoinOnlineAsync(_sut);
        var mockAdapter = Substitute.For<ICommandTransportAdapter>();
        mockAdapter.When(a => a.ClearPublishers())
            .Throw(new InvalidOperationException("clear failed"));
        _commandPublisher.Adapter.Returns(mockAdapter);

        // Act & Assert - should not throw
        await Should.NotThrowAsync(() => _sut.Disconnect());
        _sut.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public async Task DisconnectAsync_WhenRemovePublisherThrows_LogsWarningAndCompletes()
    {
        // Arrange
        var publisher = await JoinOnlineAsync(_sut);
        _transportAdapter.When(a => a.RemovePublisher(publisher))
            .Throw(new InvalidOperationException("remove failed"));

        // Act & Assert - should not throw
        await Should.NotThrowAsync(() => _sut.Disconnect());
        _logger.Received(1).LogWarning(
            Arg.Any<InvalidOperationException>(),
            "Failed to remove relay publisher during cleanup");
        _sut.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public async Task DisconnectAsync_CalledTwice_IsIdempotent()
    {
        // Arrange
        await JoinOnlineAsync(_sut);
        await _sut.Disconnect();

        // Act
        await _sut.Disconnect();

        // Assert - the member is only removed once
        await _relayRoomClient.Received(1).RemoveMember(
            "ABCDEF", "session-token", Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        _sut.IsConnected.ShouldBeFalse();
    }

    // ---------- Dispose ----------

    [Fact]
    public async Task DisposeAsync_TearsDownConnection()
    {
        // Arrange
        var publisher = await JoinOnlineAsync(_sut);

        // Act
        await _sut.DisposeAsync();

        // Assert
        await _relayRoomClient.Received(1).RemoveMember(
            "ABCDEF", "session-token", Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        _transportAdapter.Received(1).RemovePublisher(publisher);
        publisher.State.ToString().ShouldBe("Disconnected");
        _sut.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_IsIdempotent()
    {
        // Arrange
        await JoinOnlineAsync(_sut);

        // Act
        await _sut.DisposeAsync();
        await _sut.DisposeAsync();

        // Assert
        await _relayRoomClient.Received(1).RemoveMember(
            "ABCDEF", "session-token", Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinOnlineAsync_WhenJoinSucceeds_SetsConnectedHostGameId()
    {
        // Arrange
        var deviceSessionId = Guid.NewGuid();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var hostGameId = Guid.NewGuid();
        _relayRoomClient.Join(roomCode, sessionToken: null, Arg.Any<CancellationToken>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Client", deviceSessionId, hostGameId));
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        _relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));

        // Act
        await _sut.JoinOnline(roomCode, sessionToken: null);

        // Assert
        _sut.ConnectedHostGameId.ShouldBe(hostGameId);
        _sut.IsConnected.ShouldBeTrue();
    }

    [Fact]
    public async Task DisconnectAsync_ClearsConnectedHostGameId()
    {
        // Arrange
        await JoinOnlineAsync(_sut);
        _sut.ConnectedHostGameId.ShouldNotBeNull();

        // Act
        await _sut.Disconnect();

        // Assert
        _sut.ConnectedHostGameId.ShouldBeNull();
    }

    [Fact]
    public void ConnectedHostGameId_IsNull_ByDefault()
    {
        _sut.ConnectedHostGameId.ShouldBeNull();
    }

    [Fact]
    public async Task ConnectToLanAsync_ClearsConnectedHostGameId()
    {
        // Arrange
        await JoinOnlineAsync(_sut);
        _sut.ConnectedHostGameId.ShouldNotBeNull();

        // Act
        await _sut.ConnectToLan("http://localhost:2439/makamekhub");

        // Assert
        _sut.ConnectedHostGameId.ShouldBeNull();
    }

    [Fact]
    public async Task DisconnectHandler_ClearsConnectedHostGameId()
    {
        // Arrange
        var mockAdapter = Substitute.For<ICommandTransportAdapter>();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        commandPublisher.Adapter.Returns(mockAdapter);
        var sut = new GameConnector(
            commandPublisher,
            _transportFactory,
            _logger,
            _relayRoomClient,
            _relayPublisherFactory,
            CreateHubConfigurationProvider());

        var deviceSessionId = Guid.NewGuid();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var hostGameId = Guid.NewGuid();
        _relayRoomClient.Join(roomCode, sessionToken: null, Arg.Any<CancellationToken>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Client", deviceSessionId, hostGameId));
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        _relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));

        Action<ITransportPublisher>? disconnectHandler = null;
        mockAdapter.When(a => a.RegisterDisconnectHandler(Arg.Any<Action<ITransportPublisher>>()))
            .Do(ci => disconnectHandler = ci.Arg<Action<ITransportPublisher>>());
        await sut.JoinOnline(roomCode, sessionToken: null);
        sut.ConnectedHostGameId.ShouldBe(hostGameId);
        disconnectHandler.ShouldNotBeNull();

        // Act
        disconnectHandler.Invoke(publisher);

        // Assert
        sut.ConnectedHostGameId.ShouldBeNull();
        sut.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public async Task DisconnectHandler_SetsGameOriginIdFromConnectedHostGameId()
    {
        // Arrange
        var mockAdapter = Substitute.For<ICommandTransportAdapter>();
        var commandPublisher = Substitute.For<ICommandPublisher>();
        commandPublisher.Adapter.Returns(mockAdapter);
        var sut = new GameConnector(
            commandPublisher,
            _transportFactory,
            _logger,
            _relayRoomClient,
            _relayPublisherFactory,
            CreateHubConfigurationProvider());

        var deviceSessionId = Guid.NewGuid();
        const string roomCode = "ABCDEF";
        const string sessionToken = "session-token";
        var hostGameId = Guid.NewGuid();
        _relayRoomClient.Join(roomCode, sessionToken: null, Arg.Any<CancellationToken>())
            .Returns(RoomSessionResult.Succeeded(roomCode, sessionToken, "Client", deviceSessionId, hostGameId));
        var publisher = CreateRelayPublisher(roomCode, sessionToken);
        _relayPublisherFactory.Create(RelayOptions(roomCode), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ITransportPublisher>(publisher));

        Action<ITransportPublisher>? disconnectHandler = null;
        mockAdapter.When(a => a.RegisterDisconnectHandler(Arg.Any<Action<ITransportPublisher>>()))
            .Do(ci => disconnectHandler = ci.Arg<Action<ITransportPublisher>>());
        await sut.JoinOnline(roomCode, sessionToken: null);
        disconnectHandler.ShouldNotBeNull();

        // Act
        disconnectHandler.Invoke(publisher);

        // Assert - GameOriginId matches the host game ID so ClientGame.ShouldHandleCommand accepts it
        mockAdapter.Received(1).DispatchLocalCommand(
            Arg.Is<GameEndedCommand>(c => c.GameOriginId == hostGameId),
            publisher);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}

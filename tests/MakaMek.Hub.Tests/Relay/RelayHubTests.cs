using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sanet.MakaMek.Hub.Contracts;
using Sanet.MakaMek.Hub.Relay;
using Sanet.MakaMek.Hub.Rooms;
using Sanet.MakaMek.Hub.Security;
using Sanet.MakaMek.Hub.Tests.TestLoggers;
using Shouldly;

namespace Sanet.MakaMek.Hub.Tests.Relay;

public class RelayHubTests
{
    [Fact]
    public async Task OnConnectedAsync_WithoutHttpContext_AbortsConnection()
    {
        var hub = CreateHub();
        hub.Context = new TestHubCallerContext();

        await hub.OnConnectedAsync();

        ((TestHubCallerContext)hub.Context).WasAborted.ShouldBeTrue();
    }

    [Fact]
    public async Task OnConnectedAsync_WithHttpContextButNoSession_AbortsConnection()
    {
        var hub = CreateHub();
        hub.Context = new TestHubCallerContext(new DefaultHttpContext());

        await hub.OnConnectedAsync();

        ((TestHubCallerContext)hub.Context).WasAborted.ShouldBeTrue();
    }

[Fact]
    public async Task OnConnectedAsync_ClientSession_WithoutHostConnection_LogsWarning()
    {
        var logger = new CapturingLogger<RelayHub>();
        var rateLimiter = Substitute.For<IRelayRateLimiter>();
        var roomManager = Substitute.For<IRoomManager>();
        roomManager.GetHostConnectionId(Arg.Any<string>()).Returns((string?)null);
        var hub = CreateHub(logger, rateLimiter, roomManager);
        var groups = Substitute.For<IGroupManager>();
        groups.AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.CompletedTask);
        hub.Groups = groups;

        var session = new RoomSession(
            "tok", "ROOM1", Guid.NewGuid(), RoomRole.Client, DateTimeOffset.UtcNow.AddHours(1));
        hub.Context = ContextForSession(session);

        await hub.OnConnectedAsync();

        logger.GetMessages(LogLevel.Warning).ShouldContain(
            message => message.Contains("found no host connection", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Relay_WithoutHttpContext_ThrowsHubException()
    {
        var hub = CreateHub();
        hub.Context = new TestHubCallerContext();

        var exception = await Should.ThrowAsync<HubException>(
            async () => await hub.Relay("room1", CreateEnvelope()));

        exception.Message.ShouldContain("Authenticated session is missing");
    }

    [Fact]
    public async Task Relay_WithHttpContextButNoSession_ThrowsHubException()
    {
        var hub = CreateHub();
        hub.Context = new TestHubCallerContext(new DefaultHttpContext());

        var exception = await Should.ThrowAsync<HubException>(
            async () => await hub.Relay("room1", CreateEnvelope()));

        exception.Message.ShouldContain("Authenticated session is missing");
    }

    [Fact]
    public async Task Relay_FromSupersededConnection_ThrowsConnectionSuperseded()
    {
        var rateLimiter = Substitute.For<IRelayRateLimiter>();
        rateLimiter.TryConsume(Arg.Any<string>()).Returns(true);
        var roomManager = Substitute.For<IRoomManager>();
        roomManager.GetConnectionId(Arg.Any<string>(), Arg.Any<Guid>()).Returns("other-conn");
        var options = Options.Create(new Configuration.HubOptions());
        var hub = new RelayHub(
            rateLimiter, roomManager, Substitute.For<IPeerNotificationScheduler>(), options,
            NullLogger<RelayHub>.Instance);

        var roomCode = "ROOM1";
        var session = new RoomSession("tok", roomCode, Guid.NewGuid(), RoomRole.Client,
            DateTimeOffset.UtcNow.AddHours(1));

        var httpContext = new DefaultHttpContext();
        httpContext.Items[RelayAuthenticationDefaults.AuthenticatedSessionItemKey] = session;
        hub.Context = new TestHubCallerContext(httpContext);
        hub.Clients = Substitute.For<IHubCallerClients<IRelayHub>>();

        var exception = await Should.ThrowAsync<HubException>(
            async () => await hub.Relay(roomCode, CreateEnvelope()));

        exception.Message.ShouldContain(nameof(HubErrorCode.ConnectionSuperseded));
    }

    private static RelayEnvelope CreateEnvelope(string? payload = null)
        => new("sender", payload ?? "payload", "1.0.0", 1, DateTime.UtcNow);

    [Fact]
    public async Task Relay_WrongRoom_LogsWarning()
    {
        var logger = new CapturingLogger<RelayHub>();
        var hub = CreateHub(logger);
        hub.Context = ContextForSession(new RoomSession(
            "tok", "ROOM1", Guid.NewGuid(), RoomRole.Client, DateTimeOffset.UtcNow.AddHours(1)));
        hub.Clients = Substitute.For<IHubCallerClients<IRelayHub>>();

        await Should.ThrowAsync<HubException>(
            async () => await hub.Relay("OTHER", CreateEnvelope()));

        logger.GetMessages(LogLevel.Warning).ShouldContain(
            message => message.Contains("does not match the caller's room", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Relay_Successful_LogsDebug_WithMessageType()
    {
        var logger = new CapturingLogger<RelayHub>();
        var rateLimiter = Substitute.For<IRelayRateLimiter>();
        rateLimiter.TryConsume(Arg.Any<string>()).Returns(true);
        var roomManager = Substitute.For<IRoomManager>();
        roomManager.GetConnectionId(Arg.Any<string>(), Arg.Any<Guid>())
            .Returns("test-connection-id");
        var hub = CreateHub(logger, rateLimiter, roomManager);

        var session = new RoomSession(
            "tok", "ROOM1", Guid.NewGuid(), RoomRole.Client, DateTimeOffset.UtcNow.AddHours(1));
        hub.Context = ContextForSession(session);

        var roomClients = Substitute.For<IRelayHub>();
        var clients = Substitute.For<IHubCallerClients<IRelayHub>>();
        clients.OthersInGroup(session.RoomCode).Returns(roomClients);
        hub.Clients = clients;

        await hub.Relay(session.RoomCode, CreateEnvelope("{\"MessageType\":\"DeployUnitCommand\"}"));

        logger.GetMessages(LogLevel.Debug).ShouldContain(
            message => message.Contains("DeployUnitCommand", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Relay_PayloadTooLarge_LogsWarning()
    {
        var logger = new CapturingLogger<RelayHub>();
        var rateLimiter = Substitute.For<IRelayRateLimiter>();
        rateLimiter.TryConsume(Arg.Any<string>()).Returns(true);
        var roomManager = Substitute.For<IRoomManager>();
        roomManager.GetConnectionId(Arg.Any<string>(), Arg.Any<Guid>())
            .Returns("test-connection-id");
        var options = Options.Create(new Configuration.HubOptions { MaxRelayPayloadBytes = 4 });
        var hub = new RelayHub(
            rateLimiter, roomManager, Substitute.For<IPeerNotificationScheduler>(), options, logger);
        hub.Context = ContextForSession(new RoomSession(
            "tok", "ROOM1", Guid.NewGuid(), RoomRole.Client, DateTimeOffset.UtcNow.AddHours(1)));
        hub.Clients = Substitute.For<IHubCallerClients<IRelayHub>>();

        await Should.ThrowAsync<HubException>(
            async () => await hub.Relay("ROOM1", CreateEnvelope()));

        logger.GetMessages(LogLevel.Warning).ShouldContain(
            message => message.Contains("exceeds the", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Relay_WithWhitespacePayload_StillRelays()
    {
        var logger = new CapturingLogger<RelayHub>();
        var hub = CreateHubWithConnectedRelay(logger, out var session, out var roomClients);

        await hub.Relay(session.RoomCode, CreateEnvelope("   "));

        await roomClients.Received(1).OnReceive(Arg.Any<RelayEnvelope>());
    }

    [Fact]
    public async Task Relay_WithInvalidJsonPayload_StillRelays()
    {
        var logger = new CapturingLogger<RelayHub>();
        var hub = CreateHubWithConnectedRelay(logger, out var session, out var roomClients);

        await hub.Relay(session.RoomCode, CreateEnvelope("not-json"));

        await roomClients.Received(1).OnReceive(Arg.Any<RelayEnvelope>());
    }

    [Fact]
    public async Task Relay_WithPayloadWithoutMessageType_StillRelays()
    {
        var logger = new CapturingLogger<RelayHub>();
        var hub = CreateHubWithConnectedRelay(logger, out var session, out var roomClients);

        await hub.Relay(session.RoomCode, CreateEnvelope("{\"other\":1}"));

        await roomClients.Received(1).OnReceive(Arg.Any<RelayEnvelope>());
    }

    [Fact]
    public async Task OnConnectedAsync_ActiveClientConnection_CancelsPendingNotification_AndNotifiesHostWithDeviceSessionId()
    {
        var roomManager = Substitute.For<IRoomManager>();
        roomManager.GetHostConnectionId("ROOM1").Returns("host-conn");
        var scheduler = Substitute.For<IPeerNotificationScheduler>();
        var hub = CreateHub(roomManager: roomManager, scheduler: scheduler);
        var groups = Substitute.For<IGroupManager>();
        groups.AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.CompletedTask);
        hub.Groups = groups;

        var hostClients = Substitute.For<IRelayHub>();
        var clients = Substitute.For<IHubCallerClients<IRelayHub>>();
        clients.Client("host-conn").Returns(hostClients);
        hub.Clients = clients;

        var session = new RoomSession(
            "tok", "ROOM1", Guid.NewGuid(), RoomRole.Client, DateTimeOffset.UtcNow.AddHours(1));
        hub.Context = ContextForSession(session);

        await hub.OnConnectedAsync();

        scheduler.Received(1).CancelDisconnectNotification("ROOM1", session.DeviceSessionId);
        await hostClients.Received(1).OnPeerConnected(session.DeviceSessionId.ToString());
        await hostClients.DidNotReceive().OnPeerDisconnected(Arg.Any<string>());
    }

    [Fact]
    public async Task OnDisconnectedAsync_ActiveClientConnection_SchedulesDisconnectNotification()
    {
        var roomManager = Substitute.For<IRoomManager>();
        roomManager.UnregisterConnection(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>()).Returns(true);
        var scheduler = Substitute.For<IPeerNotificationScheduler>();
        var hub = CreateHub(roomManager: roomManager, scheduler: scheduler);

        var session = new RoomSession(
            "tok", "ROOM1", Guid.NewGuid(), RoomRole.Client, DateTimeOffset.UtcNow.AddHours(1));
        hub.Context = ContextForSession(session);

        await hub.OnDisconnectedAsync(null);

        scheduler.Received(1).ScheduleDisconnectNotification("ROOM1", session.DeviceSessionId);
        scheduler.DidNotReceive().CancelDisconnectNotification(Arg.Any<string>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task OnDisconnectedAsync_ClientConnectionNotActive_DoesNotScheduleNotification()
    {
        var roomManager = Substitute.For<IRoomManager>();
        roomManager.UnregisterConnection(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>()).Returns(false);
        var scheduler = Substitute.For<IPeerNotificationScheduler>();
        var hub = CreateHub(roomManager: roomManager, scheduler: scheduler);

        var session = new RoomSession(
            "tok", "ROOM1", Guid.NewGuid(), RoomRole.Client, DateTimeOffset.UtcNow.AddHours(1));
        hub.Context = ContextForSession(session);

        await hub.OnDisconnectedAsync(null);

        scheduler.DidNotReceive().ScheduleDisconnectNotification(Arg.Any<string>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task OnDisconnectedAsync_HostConnectionSuperseded_LogsInformation()
    {
        var logger = new CapturingLogger<RelayHub>();
        var rateLimiter = Substitute.For<IRelayRateLimiter>();
        var roomManager = Substitute.For<IRoomManager>();
        roomManager.TryMarkHostDisconnected(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(false);
        var hub = CreateHub(logger, rateLimiter, roomManager);

        var session = new RoomSession(
            "tok", "ROOM1", Guid.NewGuid(), RoomRole.Host, DateTimeOffset.UtcNow.AddHours(1));
        hub.Context = ContextForSession(session);

        await hub.OnDisconnectedAsync(null);

        logger.GetMessages(LogLevel.Information).ShouldContain(
            message => message.Contains("superseded connection remains active", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithoutAuthenticatedSession_LogsDebug()
    {
        var logger = new CapturingLogger<RelayHub>();
        var hub = CreateHub(logger);
        hub.Context = new TestHubCallerContext(new DefaultHttpContext());

        await hub.OnDisconnectedAsync(null);

        logger.GetMessages(LogLevel.Debug).ShouldContain(
            message => message.Contains("closed without an authenticated session", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithException_LogsWarning()
    {
        var logger = new CapturingLogger<RelayHub>();
        var hub = CreateHub(logger);
        hub.Context = new TestHubCallerContext(new DefaultHttpContext());

        await hub.OnDisconnectedAsync(new Exception("boom"));

        logger.GetMessages(LogLevel.Warning).ShouldContain(
            message => message.Contains("closed with error", StringComparison.Ordinal));
    }

    private static RelayHub CreateHubWithConnectedRelay(
        ILogger<RelayHub> logger,
        out RoomSession session,
        out IRelayHub roomClients)
    {
        var rateLimiter = Substitute.For<IRelayRateLimiter>();
        rateLimiter.TryConsume(Arg.Any<string>()).Returns(true);
        var roomManager = Substitute.For<IRoomManager>();
        roomManager.GetConnectionId(Arg.Any<string>(), Arg.Any<Guid>())
            .Returns("test-connection-id");
        var hub = CreateHub(logger, rateLimiter, roomManager);

        session = new RoomSession(
            "tok", "ROOM1", Guid.NewGuid(), RoomRole.Client, DateTimeOffset.UtcNow.AddHours(1));
        hub.Context = ContextForSession(session);

        roomClients = Substitute.For<IRelayHub>();
        var clients = Substitute.For<IHubCallerClients<IRelayHub>>();
        clients.OthersInGroup(session.RoomCode).Returns(roomClients);
        hub.Clients = clients;

        return hub;
    }

    private static RelayHub CreateHub(
        ILogger<RelayHub>? logger = null,
        IRelayRateLimiter? rateLimiter = null,
        IRoomManager? roomManager = null,
        IPeerNotificationScheduler? scheduler = null)
    {
        rateLimiter ??= Substitute.For<IRelayRateLimiter>();
        roomManager ??= Substitute.For<IRoomManager>();
        var options = Options.Create(new Configuration.HubOptions());
        return new RelayHub(
            rateLimiter,
            roomManager,
            scheduler ?? Substitute.For<IPeerNotificationScheduler>(),
            options,
            logger ?? NullLogger<RelayHub>.Instance);
    }

    private static TestHubCallerContext ContextForSession(RoomSession session)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[RelayAuthenticationDefaults.AuthenticatedSessionItemKey] = session;
        return new TestHubCallerContext(httpContext);
    }

    private class TestHubCallerContext : HubCallerContext
    {
        public TestHubCallerContext(HttpContext? httpContext = null)
        {
            if (httpContext is not null)
            {
                var feature = new HttpContextFeature { HttpContext = httpContext };
                Features.Set<IHttpContextFeature>(feature);
            }
        }

        public override string ConnectionId { get; } = "test-connection-id";
        public override ClaimsPrincipal User { get; } = new();
        public override string? UserIdentifier => null;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override IFeatureCollection Features { get; } = new FeatureCollection();

        public override void Abort() => WasAborted = true;

        public bool WasAborted { get; private set; }
    }

    private sealed class HttpContextFeature : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; }
    }
}

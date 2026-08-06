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
        var hub = new RelayHub(rateLimiter, roomManager, options, NullLogger<RelayHub>.Instance);

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

    private static RelayEnvelope CreateEnvelope()
        => new("sender", "payload", "1.0.0", 1, DateTime.UtcNow);

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

        await hub.Relay(session.RoomCode, CreateEnvelope());

        logger.GetMessages(LogLevel.Debug).ShouldContain(
            message => message.Contains("Relaying", StringComparison.Ordinal));
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
        var hub = new RelayHub(rateLimiter, roomManager, options, logger);
        hub.Context = ContextForSession(new RoomSession(
            "tok", "ROOM1", Guid.NewGuid(), RoomRole.Client, DateTimeOffset.UtcNow.AddHours(1)));
        hub.Clients = Substitute.For<IHubCallerClients<IRelayHub>>();

        await Should.ThrowAsync<HubException>(
            async () => await hub.Relay("ROOM1", CreateEnvelope()));

        logger.GetMessages(LogLevel.Warning).ShouldContain(
            message => message.Contains("exceeds the", StringComparison.Ordinal));
    }

    private static RelayHub CreateHub(
        ILogger<RelayHub>? logger = null,
        IRelayRateLimiter? rateLimiter = null,
        IRoomManager? roomManager = null)
    {
        rateLimiter ??= Substitute.For<IRelayRateLimiter>();
        roomManager ??= Substitute.For<IRoomManager>();
        var options = Options.Create(new Configuration.HubOptions());
        return new RelayHub(rateLimiter, roomManager, options, logger ?? NullLogger<RelayHub>.Instance);
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

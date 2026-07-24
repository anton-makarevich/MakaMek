using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sanet.MakaMek.Hub.Relay;
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

    private static RelayHub CreateHub()
    {
        var rateLimiter = Substitute.For<IRelayRateLimiter>();
        var options = Options.Create(new Configuration.HubOptions());
        return new RelayHub(rateLimiter, options);
    }

    private static RelayEnvelope CreateEnvelope()
        => new("sender", "payload", "1.0.0", 1, DateTime.UtcNow);

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

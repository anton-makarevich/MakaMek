using Microsoft.Extensions.Options;
using Sanet.MakaMek.Hub.Configuration;
using Sanet.MakaMek.Hub.Relay;
using Shouldly;

namespace Sanet.MakaMek.Hub.Tests.Relay;

public class RelayRateLimiterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void RemoveConnection_WithNullOrEmptyConnectionId_DoesNotThrow(string? connectionId)
    {
        var options = Options.Create(new HubOptions { RelayRateLimitPerMinute = 120 });
        var sut = new RelayRateLimiter(options, TimeProvider.System);

        var act = () => sut.RemoveConnection(connectionId!);

        act.ShouldNotThrow();
    }

    [Fact]
    public void RemoveConnection_WithValidId_RemovesStateSoWindowResets()
    {
        var options = Options.Create(new HubOptions { RelayRateLimitPerMinute = 1 });
        var sut = new RelayRateLimiter(options, TimeProvider.System);

        sut.TryConsume("conn-1").ShouldBeTrue();
        sut.TryConsume("conn-1").ShouldBeFalse();

        sut.RemoveConnection("conn-1");

        sut.TryConsume("conn-1").ShouldBeTrue();
    }
}

using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Core.Services.Transport.Relay;
using Sanet.Transport.SignalR.Client.Publishers;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services.Transport.Relay;

public class RelayPublisherFactoryTests
{
    private const string RoomCode = "ABCDEF";
    private const string SessionToken = "session-token";
    private const string ApiKey = "api-key";

    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(10);

    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();
    private readonly RelayPublisherFactory _sut;

    public RelayPublisherFactoryTests()
    {
        _loggerFactory.CreateLogger<RelayClientPublisher>()
            .Returns(Substitute.For<ILogger<RelayClientPublisher>>());
        _sut = new RelayPublisherFactory(_loggerFactory);
    }

    [Fact]
    public async Task CreateAsync_WhenCancelledBeforeStart_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var exception = await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.CreateAsync("http://127.0.0.1:1/hubs/relay", RoomCode, SessionToken, Guid.NewGuid(), ApiKey, cts.Token));

        exception.CancellationToken.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task CreateAsync_WhenHubUnreachable_Throws()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        await WithTimeout(
            Should.ThrowAsync<Exception>(
                () => _sut.CreateAsync($"http://127.0.0.1:{port}/hubs/relay", RoomCode, SessionToken, Guid.NewGuid(), ApiKey)));
    }

    [Fact]
    public async Task CreateAsync_WhenHubReachable_ReturnsConnectedPublisher()
    {
        await using var host = await TestRelayHubHost.StartAsync(ApiKey);
        var hubUrl = host.Urls.First().TrimEnd('/') + "/hubs/relay";

        var publisher = await WithTimeout(
            _sut.CreateAsync(hubUrl, RoomCode, SessionToken, Guid.NewGuid(), ApiKey));

        await using var _ = publisher;
        publisher.IsConnected.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateAsync_WhenHubRejectsWrongApiKey_Throws()
    {
        await using var host = await TestRelayHubHost.StartAsync(ApiKey);
        var hubUrl = host.Urls.First().TrimEnd('/') + "/hubs/relay";

        await WithTimeout(
            Should.ThrowAsync<Exception>(
                () => _sut.CreateAsync(hubUrl, RoomCode, SessionToken, Guid.NewGuid(), "wrong-key")));
    }

    [Fact]
    public async Task CreateAsync_WhenHubRequiresApiKey_AndNoneSupplied_Throws()
    {
        await using var host = await TestRelayHubHost.StartAsync(ApiKey);
        var hubUrl = host.Urls.First().TrimEnd('/') + "/hubs/relay";

        await WithTimeout(
            Should.ThrowAsync<Exception>(
                () => _sut.CreateAsync(hubUrl, RoomCode, SessionToken, Guid.NewGuid(), string.Empty)));
    }

    private static async Task<T> WithTimeout<T>(Task<T> task)
    {
        var completed = await Task.WhenAny(task, Task.Delay(OperationTimeout));
        completed.ShouldBe(task, $"Operation did not complete within {OperationTimeout.TotalSeconds}s");
        return await task;
    }
}

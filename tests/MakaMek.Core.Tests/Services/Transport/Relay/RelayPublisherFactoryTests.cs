using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
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
            () => _sut.CreateAsync("http://127.0.0.1:1/hubs/relay", RoomCode, SessionToken, Guid.NewGuid(), cts.Token));

        exception.CancellationToken.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task CreateAsync_WhenHubUnreachable_Throws()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            var acceptTask = listener.AcceptTcpClientAsync();

            var createTask = _sut.CreateAsync(
                $"http://127.0.0.1:{port}/hubs/relay", RoomCode, SessionToken, Guid.NewGuid());

            // Keep the listener active until the connection attempt reaches it,
            // then close the accepted connection so the WebSocket handshake fails deterministically.
            var connection = await acceptTask;
            connection.Close();

            await Should.ThrowAsync<WebSocketException>(() => createTask);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task CreateAsync_WhenHubReachable_ReturnsConnectedPublisher()
    {
        await using var host = await TestRelayHubHost.StartAsync();
        var hubUrl = host.Urls.First().TrimEnd('/') + "/hubs/relay";

        var publisher = await _sut.CreateAsync(hubUrl, RoomCode, SessionToken, Guid.NewGuid());

        await using var _ = publisher;
        publisher.IsConnected.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateAsync_WhenCancelledWhileStarting_ThrowsOperationCanceledException()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            using var cts = new CancellationTokenSource();
            var acceptTask = listener.AcceptTcpClientAsync();
            var createTask = _sut.CreateAsync(
                $"http://127.0.0.1:{port}/hubs/relay",
                RoomCode,
                SessionToken,
                Guid.NewGuid(),
                cts.Token);

            // Startup barrier: completes when the listener accepts the connection attempt.
            using var connection = await acceptTask;
            await cts.CancelAsync();

            var exception = await Should.ThrowAsync<OperationCanceledException>(() => createTask);
            exception.CancellationToken.IsCancellationRequested.ShouldBeTrue();
        }
        finally
        {
            listener.Stop();
        }
    }
}

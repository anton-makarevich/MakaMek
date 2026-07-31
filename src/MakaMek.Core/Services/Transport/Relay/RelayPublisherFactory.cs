using Microsoft.Extensions.Logging;
using Sanet.Transport.SignalR.Client.Publishers;

namespace Sanet.MakaMek.Core.Services.Transport.Relay;

/// <summary>
/// Creates <see cref="RelayClientPublisher"/> instances wired to the configured Hub base URL.
/// </summary>
public sealed class RelayPublisherFactory(ILoggerFactory loggerFactory) : IRelayPublisherFactory
{
    public async Task<RelayClientPublisher> CreateAsync(
        string hubUrl,
        string roomCode,
        string sessionToken,
        Guid expectedHostId)
    {
        var logger = loggerFactory.CreateLogger<RelayClientPublisher>();
        var publisher = new RelayClientPublisher(
            hubUrl,
            roomCode,
            sessionToken,
            logger,
            expectedHostId.ToString());
        await publisher.StartAsync();
        return publisher;
    }
}

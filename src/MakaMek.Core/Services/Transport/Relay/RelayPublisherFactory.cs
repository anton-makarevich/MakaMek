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
        Guid expectedHostId,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var logger = loggerFactory.CreateLogger<RelayClientPublisher>();
        var publisher = new RelayClientPublisher(
            hubUrl,
            roomCode,
            sessionToken,
            logger,
            expectedHostId.ToString(),
            apiKey);

        // StartAsync does not accept a token, so link one to abandon the publisher
        // promptly if the caller cancels while the connection is being established.
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var startTask = publisher.StartAsync();
        try
        {
            var completed = await Task.WhenAny(startTask, Task.Delay(Timeout.InfiniteTimeSpan, linkedCts.Token));
            if (completed != startTask)
            {
                linkedCts.Token.ThrowIfCancellationRequested();
            }
            await startTask;
            return publisher;
        }
        catch
        {
            try
            {
                await publisher.DisposeAsync();
            }
            catch
            {
                // Swallow to avoid masking the original failure
            }
            throw;
        }
    }
}

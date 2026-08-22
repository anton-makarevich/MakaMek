using Microsoft.Extensions.Logging;
using Sanet.Transport;

namespace Sanet.MakaMek.Core.Services.Transport;

/// <summary>
/// Cleanup helpers for transport publishers owned by a game lifecycle component.
/// </summary>
public static class TransportPublisherExtensions
{
    extension(ITransportPublisher? publisher)
    {
        /// <summary>
        /// Removes the publisher from the adapter and disposes it, swallowing any exceptions
        /// (logging them as warnings) so cleanup never masks an original failure.
        /// <see cref="ICommandTransportAdapter.RemovePublisher"/> does not dispose the publisher
        /// nor unhook its command subscription — disposing it is what tears those down.
        /// </summary>
        public async Task RemoveAndDisposeAsync(
            ICommandTransportAdapter adapter,
            ILogger logger)
        {
        if (publisher == null) return;
        var publisherType = publisher.GetType().Name;
        try
        {
            adapter.RemovePublisher(publisher);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to remove {PublisherType} during cleanup", publisherType);
        }

        try
        {
            await publisher.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to dispose {PublisherType} during cleanup", publisherType);
        }
        }
    }
}

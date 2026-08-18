using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.Transport;

namespace Sanet.MakaMek.Core.Services.Transport;

public interface ICommandTransportAdapter : IAsyncDisposable
{
    IReadOnlyList<ITransportPublisher> TransportPublishers { get; }

    /// <summary>
    /// Adds a transport publisher to the adapter
    /// </summary>
    /// <param name="publisher">The publisher to add</param>
    void AddPublisher(ITransportPublisher? publisher);

    /// <summary>
    /// Removes a transport publisher from the adapter without disposing it.
    /// </summary>
    /// <param name="publisher">The publisher to remove</param>
    void RemovePublisher(ITransportPublisher publisher);

    /// <summary>
    /// Clears all transport publishers from the adapter and disposes them if they implement IAsyncDisposable
    /// </summary>
    Task ClearPublishers();

    /// <summary>
    /// Converts an IGameCommand to a TransportMessage and publishes it to all publishers
    /// </summary>
    /// <param name="command">The command to publish</param>
    void PublishCommand(IGameCommand command);

    /// <summary>
    /// Converts an IGameCommand to a TransportMessage and publishes it to a single target publisher.
    /// Serialization is performed once and per-publisher error isolation is preserved.
    /// </summary>
    /// <param name="command">The command to publish</param>
    /// <param name="targetPublisher">The specific transport publisher to send to</param>
    void PublishCommand(IGameCommand command, ITransportPublisher targetPublisher);

    /// <summary>
    /// Subscribes to transport messages and converts them back to IGameCommand
    /// </summary>
    /// <param name="onCommandReceived">Callback for received commands</param>
    void Initialize(Action<IGameCommand, ITransportPublisher> onCommandReceived);

    /// <summary>
    /// Registers a callback invoked when a transport publisher reports that its underlying
    /// connection was lost because the remote host disconnected (e.g. relay host loss).
    /// Only publishers that support disconnect notifications will trigger this callback;
    /// publishers that do not are silently ignored.
    /// </summary>
    /// <param name="onPublisherDisconnected">Callback invoked with the publisher that lost its connection.</param>
    void RegisterDisconnectHandler(Action<ITransportPublisher> onPublisherDisconnected);

    /// <summary>
    /// Dispatches a locally-originated command through the same receive path used for
    /// inbound transport messages. This lets locally synthesized commands (e.g. a
    /// game-ended command raised when the relay host disconnects) reach local subscribers
    /// even when no transport publisher is connected, without relying on an outbound
    /// publish being echoed back.
    /// </summary>
    /// <param name="command">The command to dispatch to local subscribers.</param>
    /// <param name="sourcePublisher">The publisher the command is attributed to.</param>
    void DispatchLocalCommand(IGameCommand command, ITransportPublisher sourcePublisher);
}
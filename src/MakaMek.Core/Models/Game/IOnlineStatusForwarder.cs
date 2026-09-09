using Sanet.MakaMek.Core.Services.Transport;

namespace Sanet.MakaMek.Core.Models.Game;

/// <summary>
/// Forwards the transport adapter's connection status into a session-scoped observable
/// stream. The stream only reflects the adapter's status while a session (hosting or
/// joining) is active; it is reset otherwise so lobby UI never sees stale status from a
/// previous session. Implemented as a singleton shared between the host and client flows.
/// </summary>
public interface IOnlineStatusForwarder
{
    /// <summary>
    /// Gets the connection status of the current online session. Remains
    /// <see cref="ConnectionStatus.NotConnected"/> while no online session is active.
    /// </summary>
    IObservable<ConnectionStatus> OnlineConnectionStatus { get; }

    /// <summary>
    /// Subscribes the adapter's connection status changes to this forwarder's stream.
    /// Dispose-then-subscribe keeps re-hosting / re-joining idempotent.
    /// </summary>
    /// <param name="adapter">The transport adapter whose status should be forwarded.</param>
    void Start(ICommandTransportAdapter adapter);

    /// <summary>
    /// Stops forwarding and resets the stream to <see cref="ConnectionStatus.NotConnected"/>
    /// so no stale status leaks into the next session.
    /// </summary>
    void Reset();
}

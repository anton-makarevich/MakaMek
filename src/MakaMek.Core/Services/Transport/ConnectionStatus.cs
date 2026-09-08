namespace Sanet.MakaMek.Core.Services.Transport;

/// <summary>
/// Transport connection status as surfaced to the UI. Deliberately transport-agnostic:
/// the Sanet.Transport enums are only ever mapped inside <see cref="CommandTransportAdapter"/>.
/// </summary>
public enum ConnectionStatus
{
    /// <summary>
    /// The connection is being established.
    /// </summary>
    Connecting,

    /// <summary>
    /// The connection is fully established and usable.
    /// </summary>
    Connected,

    /// <summary>
    /// A previously established connection was lost but is being re-established.
    /// </summary>
    Reconnecting,

    /// <summary>
    /// The connection is lost and is not currently being re-established.
    /// </summary>
    Disconnected,

    /// <summary>
    /// The transport is permanently closed (terminal state). The publisher must be
    /// recreated for a new connection; hosting/joining is no longer possible.
    /// </summary>
    Closed
}
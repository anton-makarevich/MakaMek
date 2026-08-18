using Sanet.Transport.SignalR.Client.Relay;

namespace Sanet.MakaMek.Core.Models.Game;

/// <summary>
/// Owns all client-side connection networking: connecting to a LAN server, joining an
/// online room through the cloud relay, and tearing the connection down. Presentation
/// layers drive networking exclusively through this interface.
/// </summary>
public interface IGameConnector : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Connects to a LAN server as a client using the given server address.
    /// </summary>
    /// <param name="serverAddress">Address of the LAN hub to connect to.</param>
    Task ConnectToLan(string serverAddress);

    /// <summary>
    /// Joins an online game room hosted through the cloud relay.
    /// </summary>
    /// <param name="roomCode">Room code of the lobby to join.</param>
    /// <param name="sessionToken">Session token of the device's existing session, when rejoining.
    /// Null for a new device, which the Hub registers under a freshly minted device session.</param>
    /// <param name="cancellationToken">Cancellation token for the join lifecycle calls.</param>
    Task JoinOnline(
        string roomCode,
        string? sessionToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Leaves the current game connection, removing the client from an online room when
    /// joined through the relay. Best-effort and idempotent: failures are swallowed and
    /// calling this when nothing is connected is a no-op.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the leave calls.</param>
    Task Disconnect(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value indicating whether the client is connected to a game server.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets the game id of the authoritative server host after a successful join.
    /// Null when not connected or when connected via LAN.
    /// </summary>
    Guid? ConnectedHostGameId { get; }

    /// <summary>
    /// Gets the last error reported by the online join flow, if any.
    /// </summary>
    RelayClientError? OnlineError { get; }
}

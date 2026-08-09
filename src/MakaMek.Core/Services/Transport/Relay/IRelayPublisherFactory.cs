using Sanet.Transport.SignalR.Client.Publishers;

namespace Sanet.MakaMek.Core.Services.Transport.Relay;

/// <summary>
/// Factory that creates <see cref="RelayClientPublisher"/> instances for a joined relay room.
/// The returned publisher is already connected to the hub. Implementations are registered as
/// shared services so hosts can publish commands over the relay transport.
/// </summary>
public interface IRelayPublisherFactory
{
    /// <summary>
    /// Creates and connects a relay publisher for the given room.
    /// </summary>
    /// <param name="hubUrl">Base URL of the MakaMek Hub.</param>
    /// <param name="roomCode">Room code the host has created.</param>
    /// <param name="sessionToken">Host session token returned when the room was created.</param>
    /// <param name="expectedHostId">Id of the host ServerGame (GameOriginId) this publisher is expected to act for.</param>
    /// <param name="apiKey">API key required by the MakaMek Hub, if any.</param>
    /// <param name="cancellationToken">Token that cancels publisher creation and connection.</param>
    Task<RelayClientPublisher> Create(
        string hubUrl,
        string roomCode,
        string sessionToken,
        Guid expectedHostId,
        string apiKey,
        CancellationToken cancellationToken = default);
}

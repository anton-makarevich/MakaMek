namespace Sanet.MakaMek.Core.Services.Transport.Relay;

/// <summary>
/// Typed client for the Hub REST room lifecycle (create, join, ready, close, remove member).
/// The Hub boundary deals in Hub-minted device session identities and the host game id;
/// no player identity is sent or received.
/// </summary>
/// <remarks>
/// <see cref="Create"/>, <see cref="Ready"/> and <see cref="Close"/> accept an optional
/// <see cref="RelayClientOptions"/> to pin a room lifecycle to the hub it was started on.
/// When omitted, the currently active hub configuration is resolved for each request.
/// </remarks>
public interface IRelayRoomClient
{
    Task<RoomCreateResult> Create(
        Guid gameId,
        CancellationToken cancellationToken = default,
        RelayClientOptions? options = null);

    Task<RoomJoinResult> Join(
        string roomCode,
        string? sessionToken,
        CancellationToken cancellationToken = default);

    Task<RoomOperationResult> Ready(
        string roomCode,
        string sessionToken,
        CancellationToken cancellationToken = default,
        RelayClientOptions? options = null);

    Task<RoomOperationResult> Close(
        string roomCode,
        string sessionToken,
        CancellationToken cancellationToken = default,
        RelayClientOptions? options = null);

    Task<RoomOperationResult> RemoveMember(
        string roomCode,
        string sessionToken,
        Guid deviceSessionId,
        CancellationToken cancellationToken = default);
}

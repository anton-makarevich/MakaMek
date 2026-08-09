namespace Sanet.MakaMek.Core.Services.Transport.Relay;

/// <summary>
/// Typed client for the Hub REST room lifecycle (create, join, ready, close, remove member).
/// The Hub boundary deals in Hub-minted device session identities and the host game id;
/// no player identity is sent or received.
/// </summary>
public interface IRelayRoomClient
{
    Task<RoomCreateResult> Create(
        Guid gameId,
        CancellationToken cancellationToken = default);

    Task<RoomJoinResult> Join(
        string roomCode,
        string? sessionToken,
        CancellationToken cancellationToken = default);

    Task<RoomOperationResult> Ready(
        string roomCode,
        string sessionToken,
        CancellationToken cancellationToken = default);

    Task<RoomOperationResult> Close(
        string roomCode,
        string sessionToken,
        CancellationToken cancellationToken = default);

    Task<RoomOperationResult> RemoveMember(
        string roomCode,
        string sessionToken,
        Guid deviceSessionId,
        CancellationToken cancellationToken = default);
}

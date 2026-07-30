namespace Sanet.MakaMek.Core.Services.Transport.Relay;

/// <summary>
/// Typed client for the Hub REST room lifecycle (create, join, ready, close, remove member).
/// </summary>
public interface IRelayRoomClient
{
    Task<RoomCreateResult> CreateAsync(
        Guid playerId,
        string playerName,
        CancellationToken cancellationToken = default);

    Task<RoomJoinResult> JoinAsync(
        string roomCode,
        Guid playerId,
        string playerName,
        CancellationToken cancellationToken = default);

    Task<RoomOperationResult> ReadyAsync(
        string roomCode,
        string sessionToken,
        CancellationToken cancellationToken = default);

    Task<RoomOperationResult> CloseAsync(
        string roomCode,
        string sessionToken,
        CancellationToken cancellationToken = default);

    Task<RoomOperationResult> RemoveMemberAsync(
        string roomCode,
        string sessionToken,
        Guid playerId,
        CancellationToken cancellationToken = default);
}

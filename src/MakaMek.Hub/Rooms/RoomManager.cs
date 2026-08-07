using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Sanet.MakaMek.Hub.Configuration;

namespace Sanet.MakaMek.Hub.Rooms;

/// <summary>
/// Thread-safe in-memory implementation of room management for a single relay instance.
/// Members are device sessions minted by the Hub; the Hub never sees player identity.
/// </summary>
public sealed class RoomManager : IRoomManager
{
    private const int MaximumCodeGenerationAttempts = 128;

    private readonly Lock _sync = new();
    private readonly Dictionary<string, Room> _rooms = new(StringComparer.Ordinal);
    private readonly IRoomCodeGenerator _roomCodeGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly int _maxConcurrentRooms;
    private readonly TimeSpan _roomTtl;
    private readonly TimeSpan _dissolutionGracePeriod;
    private readonly ILogger<RoomManager> _logger;

    public RoomManager(
        IRoomCodeGenerator roomCodeGenerator,
        TimeProvider timeProvider,
        IOptions<HubOptions> options,
        ILogger<RoomManager> logger)
    {
        _roomCodeGenerator = roomCodeGenerator ?? throw new ArgumentNullException(nameof(roomCodeGenerator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        ArgumentNullException.ThrowIfNull(options);

        _maxConcurrentRooms = options.Value.MaxConcurrentRooms;
        _roomTtl = TimeSpan.FromSeconds(options.Value.RoomTtlSeconds);
        _dissolutionGracePeriod = TimeSpan.FromSeconds(options.Value.DissolutionGracePeriodSeconds);
        _logger = logger;
    }

    public RoomCreationResult CreateRoom(Guid hostGameId)
    {
        if (hostGameId == Guid.Empty)
        {
            throw new ArgumentException("GameId must be a non-empty GUID.", nameof(hostGameId));
        }

        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            RemoveExpiredRooms(now);

            if (_rooms.Count >= _maxConcurrentRooms)
            {
                _logger.LogWarning(
                    "Room creation rejected: relay capacity reached ({ActiveRooms}/{MaxRooms})",
                    _rooms.Count,
                    _maxConcurrentRooms);
                return RoomCreationResult.AtCapacity(_rooms.Count);
            }

            var roomCode = GenerateAvailableRoomCode();
            var expiresAt = now.Add(_roomTtl);
            var hostDeviceSessionId = Guid.NewGuid();
            var host = new RoomMember(hostDeviceSessionId, RoomRole.Host, now);
            var session = new RoomSession(
                GenerateSessionToken(),
                roomCode,
                hostDeviceSessionId,
                RoomRole.Host,
                expiresAt);
            var room = new Room(roomCode, hostGameId, host, session, now, expiresAt);

            _rooms.Add(roomCode, room);

            _logger.LogInformation(
                "Room {RoomCode} created for device session {DeviceSessionId}; expires {ExpiresAt}; {ActiveRooms} active room(s)",
                roomCode,
                hostDeviceSessionId,
                expiresAt,
                _rooms.Count);

            return RoomCreationResult.Created(room, session, _rooms.Count);
        }
    }

    public RoomJoinResult JoinRoom(string roomCode, string? sessionToken)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();

            if (!_rooms.TryGetValue(roomCode, out var room))
            {
                _logger.LogWarning(
                    "Join rejected for room {RoomCode}: room not found",
                    roomCode);
                return RoomJoinResult.NotFound();
            }

            if (room.IsExpiredAt(now))
            {
                _logger.LogWarning(
                    "Join rejected for room {RoomCode}: room expired",
                    roomCode);
                return RoomJoinResult.Expired();
            }

            // Terminal dissolution deadline: purge and reject.
            if (room.IsDissolvedAt(now))
            {
                _logger.LogWarning(
                    "Join rejected for room {RoomCode}: room dissolved",
                    roomCode);
                room.RevokeAllSessions();
                _rooms.Remove(roomCode);
                return RoomJoinResult.NotFound();
            }

            // A rejoin presents a valid session token that resolves the existing device session.
            Guid? existingDeviceSessionId = null;
            if (!string.IsNullOrWhiteSpace(sessionToken)
                && room.TryGetSession(sessionToken, out var existingSession)
                && existingSession.ExpiresAt > now)
            {
                // Reject host tokens - hosts cannot convert to client sessions
                if (existingSession.Role == RoomRole.Host)
                {
                    _logger.LogWarning(
                        "Join rejected for room {RoomCode}: host token cannot be used for rejoin",
                        roomCode);
                    return RoomJoinResult.Forbidden();
                }

                existingDeviceSessionId = existingSession.DeviceSessionId;
            }

            if (existingDeviceSessionId is not null)
            {
                var session = room.AddClientMember(
                    existingDeviceSessionId.Value,
                    now,
                    _roomTtl,
                    GenerateSessionToken);

                _logger.LogInformation(
                    "Device session {DeviceSessionId} rejoined room {RoomCode}; {MemberCount} member(s) now in the room",
                    existingDeviceSessionId.Value,
                    roomCode,
                    room.Members.Count);

                return RoomJoinResult.Joined(room, session);
            }

            if (room.State == RoomState.Created)
            {
                _logger.LogWarning(
                    "Join rejected for room {RoomCode}: host is not ready to accept joiners",
                    roomCode);
                return RoomJoinResult.NotReady();
            }

            if (room.State == RoomState.Closed)
            {
                _logger.LogWarning(
                    "Join rejected for room {RoomCode}: room is closed and is not accepting new devices",
                    roomCode);
                return RoomJoinResult.Full();
            }

            // New device: mint a fresh device session identity.
            var deviceSessionId = Guid.NewGuid();
            var joinedSession = room.AddClientMember(
                deviceSessionId,
                now,
                _roomTtl,
                GenerateSessionToken);

            _logger.LogInformation(
                "Device session {DeviceSessionId} joined room {RoomCode}; {MemberCount} member(s) now in the room",
                deviceSessionId,
                roomCode,
                room.Members.Count);

            return RoomJoinResult.Joined(room, joinedSession);
        }
    }

    public RoomReadyResult MarkRoomReady(string roomCode, string sessionToken)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();

            if (!_rooms.TryGetValue(roomCode, out var room))
            {
                _logger.LogWarning("Mark-ready failed for room {RoomCode}: room not found", roomCode);
                return RoomReadyResult.NotFound();
            }

            if (room.IsExpiredAt(now))
            {
                _logger.LogWarning("Mark-ready failed for room {RoomCode}: room expired", roomCode);
                return RoomReadyResult.Expired();
            }

            if (!room.ValidateHostSession(sessionToken, now))
            {
                _logger.LogWarning("Mark-ready failed for room {RoomCode}: caller is not the host", roomCode);
                return RoomReadyResult.NotHost();
            }

            if (!room.MarkReady(now, _roomTtl))
            {
                _logger.LogWarning(
                    "Mark-ready failed for room {RoomCode}: room is in state {RoomState}",
                    roomCode,
                    room.State);
                return RoomReadyResult.InvalidState();
            }

            _logger.LogInformation("Room {RoomCode} marked ready to accept joiners", roomCode);
            return RoomReadyResult.Ready();
        }
    }

    public RoomCloseResult CloseRoom(string roomCode, string sessionToken)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();

            if (!_rooms.TryGetValue(roomCode, out var room))
            {
                _logger.LogWarning("Close failed for room {RoomCode}: room not found", roomCode);
                return RoomCloseResult.NotFound();
            }

            if (room.IsExpiredAt(now))
            {
                _logger.LogWarning("Close failed for room {RoomCode}: room expired", roomCode);
                return RoomCloseResult.Expired();
            }

            if (!room.ValidateHostSession(sessionToken, now))
            {
                _logger.LogWarning("Close failed for room {RoomCode}: caller is not the host", roomCode);
                return RoomCloseResult.NotHost();
            }

            if (!room.Close(now, _roomTtl))
            {
                _logger.LogWarning(
                    "Close failed for room {RoomCode}: room is in state {RoomState}",
                    roomCode,
                    room.State);
                return RoomCloseResult.InvalidState();
            }

            _logger.LogInformation("Room {RoomCode} closed", roomCode);
            return RoomCloseResult.Closed();
        }
    }

    public RoomRemoveMemberResult RemoveMember(string roomCode, string sessionToken, Guid targetDeviceSessionId)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();

            if (!_rooms.TryGetValue(roomCode, out var room))
            {
                _logger.LogWarning(
                    "Remove-member failed for room {RoomCode}: room not found",
                    roomCode);
                return RoomRemoveMemberResult.NotFound();
            }

            if (room.IsExpiredAt(now))
            {
                _logger.LogWarning(
                    "Remove-member failed for room {RoomCode}: room expired",
                    roomCode);
                return RoomRemoveMemberResult.Expired();
            }

            if (!room.ValidateHostSession(sessionToken, now))
            {
                _logger.LogWarning(
                    "Remove-member failed for room {RoomCode}: caller is not the host",
                    roomCode);
                return RoomRemoveMemberResult.NotHost();
            }

            if (room.IsHost(targetDeviceSessionId))
            {
                _logger.LogWarning(
                    "Remove-member failed for room {RoomCode}: target device session {DeviceSessionId} is the host",
                    roomCode,
                    targetDeviceSessionId);
                return RoomRemoveMemberResult.CannotRemoveHost();
            }

            if (!room.IsMember(targetDeviceSessionId))
            {
                _logger.LogWarning(
                    "Remove-member failed for room {RoomCode}: target device session {DeviceSessionId} is not a member",
                    roomCode,
                    targetDeviceSessionId);
                return RoomRemoveMemberResult.MemberNotFound();
            }

            room.RemoveMember(targetDeviceSessionId);
            _logger.LogInformation(
                "Device session {DeviceSessionId} removed from room {RoomCode}",
                targetDeviceSessionId,
                roomCode);
            return RoomRemoveMemberResult.Removed();
        }
    }

    public string? RegisterConnection(string roomCode, Guid deviceSessionId, string connectionId)
    {
        lock (_sync)
        {
            if (!_rooms.TryGetValue(roomCode, out var room))
            {
                _logger.LogWarning(
                    "Connection {ConnectionId} not registered for device session {DeviceSessionId}: room {RoomCode} not found",
                    connectionId,
                    deviceSessionId,
                    roomCode);
                return null;
            }

            var replaced = room.RegisterConnection(deviceSessionId, connectionId, _timeProvider.GetUtcNow(), _roomTtl);
            _logger.LogDebug(
                "Connection {ConnectionId} registered for device session {DeviceSessionId} in room {RoomCode}; previous: {PreviousConnectionId}",
                connectionId,
                deviceSessionId,
                roomCode,
                replaced ?? "none");
            return replaced;
        }
    }

    public bool UnregisterConnection(string roomCode, Guid deviceSessionId, string connectionId)
    {
        lock (_sync)
        {
            if (!_rooms.TryGetValue(roomCode, out var room))
            {
                return false;
            }

            return room.RemoveConnection(deviceSessionId, connectionId, _timeProvider.GetUtcNow(), _roomTtl);
        }
    }

    public string? GetHostConnectionId(string roomCode)
    {
        lock (_sync)
        {
            return _rooms.TryGetValue(roomCode, out var room) ? room.GetHostConnectionId() : null;
        }
    }

    public string? GetConnectionId(string roomCode, Guid deviceSessionId)
    {
        lock (_sync)
        {
            return _rooms.TryGetValue(roomCode, out var room) ? room.GetConnectionId(deviceSessionId) : null;
        }
    }

    /// <summary>
    /// Atomically removes the connection and, only when no superseding connection
    /// has taken over, marks the room for host-disconnect dissolution.
    /// Returns true when dissolution was marked (i.e. the host is truly gone).
    /// </summary>
    public bool TryMarkHostDisconnected(string roomCode, Guid deviceSessionId, string connectionId)
    {
        lock (_sync)
        {
            if (!_rooms.TryGetValue(roomCode, out var room))
                return false;

            var now = _timeProvider.GetUtcNow();

            if (room.IsDissolvedAt(now))
            {
                room.RevokeAllSessions();
                _rooms.Remove(roomCode);
                return false;
            }

            var wasActive = room.RemoveConnection(deviceSessionId, connectionId, now, _roomTtl);
            if (!wasActive)
            {
                _logger.LogDebug(
                    "Host connection {ConnectionId} for device session {DeviceSessionId} in room {RoomCode} was not the active connection",
                    connectionId,
                    deviceSessionId,
                    roomCode);
                return false;
            }

            // A newer connection has taken over — skip dissolution.
            if (room.GetConnectionId(deviceSessionId) is not null)
            {
                _logger.LogDebug(
                    "Host connection {ConnectionId} for device session {DeviceSessionId} in room {RoomCode} superseded by a newer connection",
                    connectionId,
                    deviceSessionId,
                    roomCode);
                return false;
            }

            room.MarkForDissolution(now, _dissolutionGracePeriod);
            _logger.LogWarning(
                "Host device session {DeviceSessionId} is gone from room {RoomCode}; room marked for dissolution",
                deviceSessionId,
                roomCode);
            return true;
        }
    }

    public void MarkRoomForDissolution(string roomCode)
    {
        lock (_sync)
        {
            if (!_rooms.TryGetValue(roomCode, out var room))
            {
                return;
            }

            var now = _timeProvider.GetUtcNow();

            // Terminal deadline: purge instead of mutating a dissolved room.
            if (room.IsDissolvedAt(now))
            {
                room.RevokeAllSessions();
                _rooms.Remove(roomCode);
                _logger.LogInformation(
                    "Room {RoomCode} purged after dissolution deadline passed",
                    roomCode);
                return;
            }

            room.MarkForDissolution(now, _dissolutionGracePeriod);
            _logger.LogWarning(
                "Room {RoomCode} marked for dissolution (grace period {GracePeriodSeconds} seconds)",
                roomCode,
                _dissolutionGracePeriod.TotalSeconds);
        }
    }

    public void CancelRoomDissolution(string roomCode)
    {
        lock (_sync)
        {
            if (!_rooms.TryGetValue(roomCode, out var room))
            {
                return;
            }

            var now = _timeProvider.GetUtcNow();

            _logger.LogWarning(
                "TMPDIAG CancelRoomDissolution room {RoomCode}: now {Now}, deadline {Deadline}, dissolving {IsDissolving}",
                roomCode,
                now,
                room.DissolutionDeadline,
                room.IsDissolving);

            // Terminal deadline: purge instead of mutating a dissolved room.
            if (room.IsDissolvedAt(now))
            {
                room.RevokeAllSessions();
                _rooms.Remove(roomCode);
                _logger.LogInformation(
                    "Room {RoomCode} purged after dissolution deadline passed",
                    roomCode);
                return;
            }

            if (room.IsDissolving)
            {
                _logger.LogInformation(
                    "Dissolution of room {RoomCode} cancelled (host reconnected)",
                    roomCode);
            }

            room.CancelDissolution();
        }
    }

    public RoomSession? AuthenticateSession(string sessionToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return null;
        }

        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            RemoveExpiredRooms(now);

            foreach (var room in _rooms.Values)
            {
                if (!room.TryGetSession(sessionToken, out var session))
                {
                    continue;
                }

                // Defense in depth: token must still be bound to the room that holds it.
                if (!string.Equals(session.RoomCode, room.RoomCode, StringComparison.Ordinal))
                {
                    _logger.LogWarning(
                        "Session token for device session {DeviceSessionId} rejected: token is not bound to room {RoomCode}",
                        session.DeviceSessionId,
                        room.RoomCode);
                    return null;
                }

                if (room.IsExpiredAt(now) || session.ExpiresAt <= now)
                {
                    _logger.LogWarning(
                        "Session token for device session {DeviceSessionId} rejected: room {RoomCode} expired",
                        session.DeviceSessionId,
                        room.RoomCode);
                    return null;
                }

                _logger.LogDebug(
                    "Session authenticated for device session {DeviceSessionId} in room {RoomCode} as {Role}",
                    session.DeviceSessionId,
                    room.RoomCode,
                    session.Role);

                return session;
            }

            _logger.LogWarning(
                "Session token rejected: no matching session found in any room");
            return null;
        }
    }

    private string GenerateAvailableRoomCode()
    {
        for (var attempt = 0; attempt < MaximumCodeGenerationAttempts; attempt++)
        {
            var roomCode = _roomCodeGenerator.Generate();

            if (!_rooms.ContainsKey(roomCode))
            {
                return roomCode;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique room code.");
    }

    private void RemoveExpiredRooms(DateTimeOffset now)
    {
        var expiredRoomCodes = _rooms
            .Where(entry => entry.Value.IsExpiredAt(now) || entry.Value.IsDissolvedAt(now))
            .Select(entry => entry.Key)
            .ToArray();

        foreach (var roomCode in expiredRoomCodes)
        {
            _rooms[roomCode].RevokeAllSessions();
            _rooms.Remove(roomCode);
            _logger.LogInformation("Room {RoomCode} garbage-collected (expired or dissolved)", roomCode);
        }
    }

    private static string GenerateSessionToken() =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
}

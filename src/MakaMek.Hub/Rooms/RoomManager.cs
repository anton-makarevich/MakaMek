using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Sanet.MakaMek.Hub.Configuration;

namespace Sanet.MakaMek.Hub.Rooms;

/// <summary>
/// Thread-safe in-memory implementation of room management for a single relay instance.
/// </summary>
public sealed class RoomManager : IRoomManager
{
    private const int MaximumCodeGenerationAttempts = 128;
    private static readonly TimeSpan RoomTtl = TimeSpan.FromHours(2);

    private readonly Lock _sync = new();
    private readonly Dictionary<string, Room> _rooms = new(StringComparer.Ordinal);
    private readonly IRoomCodeGenerator _roomCodeGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly int _maxConcurrentRooms;

    public RoomManager(
        IRoomCodeGenerator roomCodeGenerator,
        TimeProvider timeProvider,
        IOptions<HubOptions> options)
    {
        _roomCodeGenerator = roomCodeGenerator ?? throw new ArgumentNullException(nameof(roomCodeGenerator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        ArgumentNullException.ThrowIfNull(options);

        _maxConcurrentRooms = options.Value.MaxConcurrentRooms;
    }

    public RoomCreationResult CreateRoom(string playerName, Guid playerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerName);

        if (playerId == Guid.Empty)
        {
            throw new ArgumentException("PlayerId must be a non-empty GUID.", nameof(playerId));
        }

        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            RemoveExpiredRooms(now);

            if (_rooms.Count >= _maxConcurrentRooms)
            {
                return RoomCreationResult.AtCapacity(_rooms.Count);
            }

            var roomCode = GenerateAvailableRoomCode();
            var expiresAt = now.Add(RoomTtl);
            var host = new RoomMember(playerId, playerName, RoomRole.Host, now);
            var session = new RoomSession(
                GenerateSessionToken(),
                roomCode,
                playerId,
                RoomRole.Host,
                expiresAt);
            var room = new Room(roomCode, host, session, now, expiresAt);

            _rooms.Add(roomCode, room);

            return RoomCreationResult.Created(room, session, _rooms.Count);
        }
    }

    public RoomJoinResult JoinRoom(string roomCode, string playerName, Guid playerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerName);

        if (playerId == Guid.Empty)
        {
            throw new ArgumentException("PlayerId must be a non-empty GUID.", nameof(playerId));
        }

        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();

            if (!_rooms.TryGetValue(roomCode, out var room))
            {
                return RoomJoinResult.NotFound();
            }

            if (room.IsExpiredAt(now))
            {
                return RoomJoinResult.Expired();
            }

            if (room.IsHost(playerId))
            {
                return RoomJoinResult.HostPlayerIdConflict();
            }

            if (room.State == RoomState.Created)
            {
                return RoomJoinResult.NotReady();
            }

            if (room.State == RoomState.Closed)
            {
                if (!room.IsMember(playerId))
                {
                    return RoomJoinResult.Full();
                }
            }

            var session = room.AddClientMember(
                playerName,
                playerId,
                now,
                RoomTtl,
                GenerateSessionToken);

            return RoomJoinResult.Joined(room, session);
        }
    }

    public RoomReadyResult MarkRoomReady(string roomCode, string sessionToken)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();

            if (!_rooms.TryGetValue(roomCode, out var room))
            {
                return RoomReadyResult.NotFound();
            }

            if (room.IsExpiredAt(now))
            {
                return RoomReadyResult.Expired();
            }

            if (!room.ValidateHostSession(sessionToken, now))
            {
                return RoomReadyResult.NotHost();
            }

            if (!room.MarkReady(now, RoomTtl))
            {
                return RoomReadyResult.InvalidState();
            }

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
                return RoomCloseResult.NotFound();
            }

            if (room.IsExpiredAt(now))
            {
                return RoomCloseResult.Expired();
            }

            if (!room.ValidateHostSession(sessionToken, now))
            {
                return RoomCloseResult.NotHost();
            }

            if (!room.Close(now, RoomTtl))
            {
                return RoomCloseResult.InvalidState();
            }

            return RoomCloseResult.Closed();
        }
    }

    public RoomRemoveMemberResult RemoveMember(string roomCode, string sessionToken, Guid targetPlayerId)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();

            if (!_rooms.TryGetValue(roomCode, out var room))
            {
                return RoomRemoveMemberResult.NotFound();
            }

            if (room.IsExpiredAt(now))
            {
                return RoomRemoveMemberResult.Expired();
            }

            if (!room.ValidateHostSession(sessionToken, now))
            {
                return RoomRemoveMemberResult.NotHost();
            }

            if (room.IsHost(targetPlayerId))
            {
                return RoomRemoveMemberResult.CannotRemoveHost();
            }

            if (!room.IsMember(targetPlayerId))
            {
                return RoomRemoveMemberResult.MemberNotFound();
            }

            room.RemoveMember(targetPlayerId);
            return RoomRemoveMemberResult.Removed();
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
                    return null;
                }

                if (room.State == RoomState.Closed || room.IsExpiredAt(now) || session.ExpiresAt <= now)
                {
                    return null;
                }

                return session;
            }

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
            .Where(entry => entry.Value.IsExpiredAt(now))
            .Select(entry => entry.Key)
            .ToArray();

        foreach (var roomCode in expiredRoomCodes)
        {
            _rooms.Remove(roomCode);
        }
    }

    private static string GenerateSessionToken() =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
}

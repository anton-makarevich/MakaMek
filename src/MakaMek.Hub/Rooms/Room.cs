namespace Sanet.MakaMek.Hub.Rooms;

/// <summary>
/// Transient relay-room state. It contains membership and session metadata only, never game state.
/// </summary>
public sealed class Room
{
    private readonly Dictionary<Guid, RoomMember> _members;
    private readonly Dictionary<string, RoomSession> _sessions;

    internal Room(
        string roomCode,
        RoomMember host,
        RoomSession hostSession,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        RoomCode = roomCode;
        HostPlayerId = host.PlayerId;
        CreatedAt = createdAt;
        LastActivityAt = createdAt;
        ExpiresAt = expiresAt;
        _members = new Dictionary<Guid, RoomMember> { [host.PlayerId] = host };
        _sessions = new Dictionary<string, RoomSession>(StringComparer.Ordinal)
        {
            [hostSession.Token] = hostSession
        };
    }

    public string RoomCode { get; }

    public Guid HostPlayerId { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset LastActivityAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public RoomState State { get; private set; } = RoomState.Created;

    public IReadOnlyCollection<RoomMember> Members => _members.Values;

    internal bool IsExpiredAt(DateTimeOffset now) => ExpiresAt <= now;

    private void Touch(DateTimeOffset now, TimeSpan ttl)
    {
        LastActivityAt = now;
        ExpiresAt = now.Add(ttl);
    }

    internal bool IsHost(Guid playerId) => HostPlayerId == playerId;

    internal bool ValidateHostSession(string token, DateTimeOffset now)
    {
        return _sessions.TryGetValue(token, out var session)
               && session.Role == RoomRole.Host
               && session.ExpiresAt > now;
    }

    internal void MarkReady(DateTimeOffset now, TimeSpan ttl)
    {
        State = RoomState.Active;
        Touch(now, ttl);
    }

    internal RoomSession AddClientMember(
        string playerName,
        Guid playerId,
        DateTimeOffset now,
        TimeSpan ttl,
        Func<string> generateToken)
    {
        Touch(now, ttl);

        var member = new RoomMember(playerId, playerName, RoomRole.Client, now);
        _members[playerId] = member;

        var expiresAt = ExpiresAt;
        var session = new RoomSession(
            generateToken(),
            RoomCode,
            playerId,
            RoomRole.Client,
            expiresAt);
        _sessions[session.Token] = session;

        return session;
    }
}

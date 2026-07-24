namespace Sanet.MakaMek.Hub.Rooms;

/// <summary>
/// Transient relay-room state. It contains membership and session metadata only, never game state.
/// </summary>
public sealed class Room
{
    private readonly Dictionary<Guid, RoomMember> _members;
    private readonly Dictionary<string, RoomSession> _sessions;
    private readonly Dictionary<Guid, string> _connections = new();

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

    public DateTimeOffset? DissolutionDeadline { get; private set; }

    public bool IsDissolving => DissolutionDeadline.HasValue;

    public IReadOnlyCollection<RoomMember> Members => _members.Values;

    internal bool IsExpiredAt(DateTimeOffset now) => ExpiresAt <= now;

    internal bool IsDissolvedAt(DateTimeOffset now) =>
        DissolutionDeadline.HasValue && now >= DissolutionDeadline.Value;

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

    internal bool HasSession(string token) => _sessions.ContainsKey(token);

    internal bool TryGetSession(string token, out RoomSession session) =>
        _sessions.TryGetValue(token, out session!);

    internal bool IsMember(Guid playerId) => _members.ContainsKey(playerId);

    internal string? RegisterConnection(Guid playerId, string connectionId, DateTimeOffset now, TimeSpan ttl)
    {
        _connections.TryGetValue(playerId, out var previousConnectionId);
        _connections[playerId] = connectionId;
        Touch(now, ttl);
        return previousConnectionId;
    }

    internal bool RemoveConnection(Guid playerId, string connectionId, DateTimeOffset now, TimeSpan ttl)
    {
        if (!_connections.TryGetValue(playerId, out var activeConnectionId)
            || !string.Equals(activeConnectionId, connectionId, StringComparison.Ordinal))
        {
            return false;
        }

        _connections.Remove(playerId);
        Touch(now, ttl);
        return true;
    }

    internal string? GetConnectionId(Guid playerId) =>
        _connections.GetValueOrDefault(playerId);

    internal string? GetHostConnectionId() => GetConnectionId(HostPlayerId);

    internal int LiveConnectionCount => _connections.Count;

    internal void MarkForDissolution(DateTimeOffset now, TimeSpan gracePeriod) =>
        DissolutionDeadline = now.Add(gracePeriod);

    internal void CancelDissolution() => DissolutionDeadline = null;

    internal void RevokeAllSessions() => _sessions.Clear();

    /// <summary>
    /// Transitions Created → Active. Returns false when the room is not in Created.
    /// </summary>
    internal bool MarkReady(DateTimeOffset now, TimeSpan ttl)
    {
        if (State != RoomState.Created)
        {
            return false;
        }

        State = RoomState.Active;
        Touch(now, ttl);
        return true;
    }

    /// <summary>
    /// Transitions Active → Closed. Returns false when the room is not in Active.
    /// </summary>
    internal bool Close(DateTimeOffset now, TimeSpan ttl)
    {
        if (State != RoomState.Active)
        {
            return false;
        }

        State = RoomState.Closed;
        Touch(now, ttl);
        return true;
    }

    /// <summary>
    /// Removes a non-host roster entry and revokes all of that player's sessions.
    /// Returns false when the target is the host or is not a member.
    /// </summary>
    internal bool RemoveMember(Guid playerId)
    {
        if (IsHost(playerId))
        {
            return false;
        }

        if (!_members.Remove(playerId))
        {
            return false;
        }

        var tokensToRevoke = _sessions
            .Where(entry => entry.Value.PlayerId == playerId)
            .Select(entry => entry.Key)
            .ToArray();

        foreach (var token in tokensToRevoke)
        {
            _sessions.Remove(token);
        }

        return true;
    }

    internal RoomSession AddClientMember(
        string playerName,
        Guid playerId,
        DateTimeOffset now,
        TimeSpan ttl,
        Func<string> generateToken)
    {
        Touch(now, ttl);

        var staleTokens = _sessions
            .Where(entry => entry.Value.PlayerId == playerId)
            .Select(entry => entry.Key)
            .ToArray();

        foreach (var token in staleTokens)
        {
            _sessions.Remove(token);
        }

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

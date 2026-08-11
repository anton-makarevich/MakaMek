namespace Sanet.MakaMek.Hub.Rooms;

/// <summary>
/// Transient relay-room state. It contains device-session membership, session metadata,
/// and connection routing only, never game state and never player identity.
/// </summary>
public sealed class Room
{
    private readonly Dictionary<Guid, RoomMember> _members;
    private readonly Dictionary<string, RoomSession> _sessions;
    private readonly Dictionary<Guid, string> _connections = new();

    internal Room(
        string roomCode,
        Guid hostGameId,
        RoomMember host,
        RoomSession hostSession,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        RoomCode = roomCode;
        HostGameId = hostGameId;
        HostDeviceSessionId = host.DeviceSessionId;
        CreatedAt = createdAt;
        LastActivityAt = createdAt;
        ExpiresAt = expiresAt;
        _members = new Dictionary<Guid, RoomMember> { [host.DeviceSessionId] = host };
        _sessions = new Dictionary<string, RoomSession>(StringComparer.Ordinal)
        {
            [hostSession.Token] = hostSession
        };
    }

    public string RoomCode { get; }

    /// <summary>
    /// Id of the host's ServerGame, reported by the host when the room was created.
    /// This identifies the game, not a device; it is deliberately separate from
    /// <see cref="HostDeviceSessionId"/>, <see cref="_members"/>, <see cref="_sessions"/>,
    /// and <see cref="_connections"/>.
    /// </summary>
    public Guid HostGameId { get; }

    public Guid HostDeviceSessionId { get; }

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

    internal bool IsHost(Guid deviceSessionId) => HostDeviceSessionId == deviceSessionId;

    internal bool ValidateHostSession(string token, DateTimeOffset now)
    {
        return _sessions.TryGetValue(token, out var session)
               && session.Role == RoomRole.Host
               && session.ExpiresAt > now;
    }

    /// <summary>
    /// Validates that <paramref name="token"/> is a live client session bound to
    /// <paramref name="deviceSessionId"/>. Used to allow a member to remove themselves.
    /// </summary>
    internal bool ValidateMemberSession(string token, Guid deviceSessionId, DateTimeOffset now)
    {
        return _sessions.TryGetValue(token, out var session)
               && session.Role == RoomRole.Client
               && session.DeviceSessionId == deviceSessionId
               && session.ExpiresAt > now;
    }

    internal bool HasSession(string token) => _sessions.ContainsKey(token);

    internal bool TryGetSession(string token, out RoomSession session) =>
        _sessions.TryGetValue(token, out session!);

    internal bool IsMember(Guid deviceSessionId) => _members.ContainsKey(deviceSessionId);

    internal string? RegisterConnection(Guid deviceSessionId, string connectionId, DateTimeOffset now, TimeSpan ttl)
    {
        _connections.TryGetValue(deviceSessionId, out var previousConnectionId);
        _connections[deviceSessionId] = connectionId;
        Touch(now, ttl);
        return previousConnectionId;
    }

    internal bool RemoveConnection(Guid deviceSessionId, string connectionId, DateTimeOffset now, TimeSpan ttl)
    {
        if (!_connections.TryGetValue(deviceSessionId, out var activeConnectionId)
            || !string.Equals(activeConnectionId, connectionId, StringComparison.Ordinal))
        {
            return false;
        }

        _connections.Remove(deviceSessionId);
        Touch(now, ttl);
        return true;
    }

    internal string? GetConnectionId(Guid deviceSessionId) =>
        _connections.GetValueOrDefault(deviceSessionId);

    internal string? GetHostConnectionId() => GetConnectionId(HostDeviceSessionId);

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
    /// Removes a non-host device-session roster entry and revokes all of that device's sessions.
    /// Returns false when the target is the host device or is not a member.
    /// </summary>
    internal bool RemoveMember(Guid deviceSessionId)
    {
        if (IsHost(deviceSessionId))
        {
            return false;
        }

        if (!_members.Remove(deviceSessionId))
        {
            return false;
        }

        var tokensToRevoke = _sessions
            .Where(entry => entry.Value.DeviceSessionId == deviceSessionId)
            .Select(entry => entry.Key)
            .ToArray();

        foreach (var token in tokensToRevoke)
        {
            _sessions.Remove(token);
        }

        return true;
    }

    /// <summary>
    /// Adds or refreshes a client device session. When the device session already exists
    /// (a rejoin), stale tokens are revoked and a fresh token is minted for the same
    /// <see cref="deviceSessionId"/>.
    /// </summary>
    internal RoomSession AddClientMember(
        Guid deviceSessionId,
        DateTimeOffset now,
        TimeSpan ttl,
        Func<string> generateToken)
    {
        if (IsHost(deviceSessionId))
        {
            throw new InvalidOperationException(
                "Cannot add the host device session as a client member.");
        }

        Touch(now, ttl);

        var staleTokens = _sessions
            .Where(entry => entry.Value.DeviceSessionId == deviceSessionId)
            .Select(entry => entry.Key)
            .ToArray();

        foreach (var token in staleTokens)
        {
            _sessions.Remove(token);
        }

        var member = new RoomMember(deviceSessionId, RoomRole.Client, now);
        _members[deviceSessionId] = member;

        var expiresAt = ExpiresAt;
        var session = new RoomSession(
            generateToken(),
            RoomCode,
            deviceSessionId,
            RoomRole.Client,
            expiresAt);
        _sessions[session.Token] = session;

        return session;
    }
}

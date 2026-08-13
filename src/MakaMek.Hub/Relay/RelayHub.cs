using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Sanet.MakaMek.Hub.Rooms;
using Sanet.MakaMek.Hub.Security;
using Sanet.Transport.SignalR.Client.Relay;
using HubError = Sanet.MakaMek.Hub.Contracts.HubError;
using HubErrorCode = Sanet.MakaMek.Hub.Contracts.HubErrorCode;
using HubOptions = Sanet.MakaMek.Hub.Configuration.HubOptions;

namespace Sanet.MakaMek.Hub.Relay;

/// <summary>
/// Transport-only SignalR hub. Connection auth and room binding happen in middleware;
/// this hub attaches the connection to its room group for the authenticated device
/// session and fans out opaque envelopes.
/// </summary>
public sealed class RelayHub : Hub<IRelayHub>
{
    /// <summary>
    /// Extra bytes reserved beyond <see cref="HubOptions.MaxRelayPayloadBytes"/> so the
    /// transport can accept a full serialized <see cref="Transport.SignalR.Client.Relay.RelayEnvelope"/> without disconnecting.
    /// Precise payload enforcement still happens inside <see cref="Relay"/>.
    /// </summary>
    public const int ReceiveMessageSizeOverheadBytes = 64 * 1024;

    private readonly IRelayRateLimiter _rateLimiter;
    private readonly IRoomManager _roomManager;
    private readonly IPeerNotificationScheduler _notificationScheduler;
    private readonly IOptions<HubOptions> _options;
    private readonly ILogger<RelayHub> _logger;

    public RelayHub(
        IRelayRateLimiter rateLimiter,
        IRoomManager roomManager,
        IPeerNotificationScheduler notificationScheduler,
        IOptions<HubOptions> options,
        ILogger<RelayHub> logger)
    {
        _rateLimiter = rateLimiter;
        _roomManager = roomManager;
        _notificationScheduler = notificationScheduler;
        _options = options;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext?.Items[RelayAuthenticationDefaults.AuthenticatedSessionItemKey]
            is not RoomSession session)
        {
            _logger.LogWarning(
                "Relay connection {ConnectionId} rejected: no authenticated session",
                Context.ConnectionId);
            Context.Abort();
            return;
        }

        _logger.LogInformation(
            "Relay connection {ConnectionId} connected for device session {DeviceSessionId} in room {RoomCode} as {Role}",
            Context.ConnectionId,
            session.DeviceSessionId,
            session.RoomCode,
            session.Role);

        var replacedConnectionId = _roomManager.RegisterConnection(
            session.RoomCode,
            session.DeviceSessionId,
            Context.ConnectionId);

        if (session.Role == RoomRole.Host)
        {
            _roomManager.CancelRoomDissolution(session.RoomCode);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, session.RoomCode);

        if (replacedConnectionId is not null)
        {
            _logger.LogInformation(
                "Relay connection {ConnectionId} replaced superseded connection {OldConnectionId} for device session {DeviceSessionId} in room {RoomCode}",
                Context.ConnectionId,
                replacedConnectionId,
                session.DeviceSessionId,
                session.RoomCode);

            await Groups.RemoveFromGroupAsync(replacedConnectionId, session.RoomCode);
        }

        if (session.Role == RoomRole.Client)
        {
            var hostConnectionId = _roomManager.GetHostConnectionId(session.RoomCode);
            if (hostConnectionId is not null)
            {
                // A reconnect cancels any pending disconnect notification for the same
                // device session, then announces the peer under its stable identity.
                _notificationScheduler.CancelDisconnectNotification(
                    session.RoomCode, session.DeviceSessionId);

                await Clients.Client(hostConnectionId)
                    .OnPeerConnected(session.DeviceSessionId.ToString());
            }
            else
            {
                _logger.LogWarning(
                    "Relay connection {ConnectionId} for device session {DeviceSessionId} found no host connection in room {RoomCode}",
                    Context.ConnectionId,
                    session.DeviceSessionId,
                    session.RoomCode);
            }
        }

        await base.OnConnectedAsync();
    }

    public async Task Relay(string roomCode, RelayEnvelope? message)
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext?.Items[RelayAuthenticationDefaults.AuthenticatedSessionItemKey]
            is not RoomSession session)
        {
            _logger.LogWarning(
                "Relay call from connection {ConnectionId} rejected: authenticated session is missing",
                Context.ConnectionId);
            throw new HubException("Authenticated session is missing.");
        }

        if (!string.Equals(roomCode, session.RoomCode, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Relay call from connection {ConnectionId} rejected: room {RoomCode} does not match the caller's room {SessionRoomCode}",
                Context.ConnectionId,
                roomCode,
                session.RoomCode);
            throw new HubException("Caller is not a member of the specified room.");
        }

        if (message?.Payload is null)
        {
            _logger.LogWarning(
                "Relay call from connection {ConnectionId} in room {RoomCode} rejected: payload must not be null",
                Context.ConnectionId,
                session.RoomCode);
            throw new HubException("Payload must not be null.");
        }

        var payloadBytes = Encoding.UTF8.GetByteCount(message.Payload);
        if (payloadBytes > _options.Value.MaxRelayPayloadBytes)
        {
            _logger.LogWarning(
                "Relay call from connection {ConnectionId} in room {RoomCode} rejected: payload of {PayloadBytes} bytes exceeds the {MaxPayloadBytes} byte limit",
                Context.ConnectionId,
                session.RoomCode,
                payloadBytes,
                _options.Value.MaxRelayPayloadBytes);
            throw new HubException(nameof(HubErrorCode.MessageTooLarge));
        }

        if (!_rateLimiter.TryConsume(Context.ConnectionId))
        {
            _logger.LogWarning(
                "Relay call from connection {ConnectionId} in room {RoomCode} rejected: per-connection rate limit exceeded",
                Context.ConnectionId,
                session.RoomCode);
            throw new HubException(nameof(HubErrorCode.RateLimited));
        }

        // Reject calls from a superseded (stale) connection.
        var activeConnectionId = _roomManager.GetConnectionId(session.RoomCode, session.DeviceSessionId);
        if (!string.Equals(activeConnectionId, Context.ConnectionId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Relay call from connection {ConnectionId} in room {RoomCode} rejected: connection was superseded by {ActiveConnectionId}",
                Context.ConnectionId,
                session.RoomCode,
                activeConnectionId);
            throw new HubException(nameof(HubErrorCode.ConnectionSuperseded));
        }

        // Hub-tagged identity: overwrite any client-supplied SenderId.
        var outbound = message with { SenderId = Context.ConnectionId };

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Relaying {MessageType} message ({PayloadBytes} bytes) from connection {ConnectionId} to room {RoomCode} (seq {SequenceNumber})",
                TryGetMessageType(message.Payload),
                payloadBytes,
                Context.ConnectionId,
                session.RoomCode,
                message.SequenceNumber);
        }

        await Clients.OthersInGroup(session.RoomCode).OnReceive(outbound);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _rateLimiter.RemoveConnection(Context.ConnectionId);

        var httpContext = Context.GetHttpContext();
        if (httpContext?.Items[RelayAuthenticationDefaults.AuthenticatedSessionItemKey]
            is RoomSession session)
        {
            if (session.Role == RoomRole.Host)
            {
                // Atomically remove the connection, check for a superseding
                // connection, and mark dissolution only when the host is truly gone.
                var hostDisconnected = _roomManager.TryMarkHostDisconnected(
                    session.RoomCode, session.DeviceSessionId, Context.ConnectionId);

                if (hostDisconnected)
                {
                    _logger.LogWarning(
                        "Host device session {DeviceSessionId} disconnected from room {RoomCode} (connection {ConnectionId}); notifying clients",
                        session.DeviceSessionId,
                        session.RoomCode,
                        Context.ConnectionId);

                    await Clients.Group(session.RoomCode).OnError(new HubError(
                        HubErrorCode.HostDisconnected,
                        "The room host disconnected.",
                        DeviceSessionId: session.DeviceSessionId));
                }
                else
                {
                    _logger.LogInformation(
                        "Host device session {DeviceSessionId} connection {ConnectionId} closed in room {RoomCode}; superseded connection remains active",
                        session.DeviceSessionId,
                        Context.ConnectionId,
                        session.RoomCode);
                }
            }
            else
            {
                var wasActive = _roomManager.UnregisterConnection(
                    session.RoomCode,
                    session.DeviceSessionId,
                    Context.ConnectionId);

                if (wasActive)
                {
                    _logger.LogInformation(
                        "Client device session {DeviceSessionId} disconnected from room {RoomCode} (connection {ConnectionId})",
                        session.DeviceSessionId,
                        session.RoomCode,
                        Context.ConnectionId);

                    // Defer the host notification so a quick reconnect of the same
                    // device session can cancel it (see PeerNotificationScheduler).
                    _notificationScheduler.ScheduleDisconnectNotification(
                        session.RoomCode, session.DeviceSessionId);
                }
            }
        }
        else
        {
            _logger.LogDebug(
                "Relay connection {ConnectionId} closed without an authenticated session",
                Context.ConnectionId);
        }

        if (exception is not null)
        {
            _logger.LogWarning(
                exception,
                "Relay connection {ConnectionId} closed with error",
                Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Best-effort extraction of the message type from the opaque envelope payload for logging
    /// only. The payload is a serialized <see cref="Sanet.Transport.TransportMessage"/> whose
    /// <c>MessageType</c> identifies the game command. Never throws and never affects relay.
    /// </summary>
    private static string? TryGetMessageType(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("MessageType", out var messageType)
                && messageType.ValueKind == JsonValueKind.String)
            {
                return messageType.GetString();
            }
        }
        catch (JsonException)
        {
            // Non-JSON payloads are relayed untouched; nothing to log.
        }

        return null;
    }
}

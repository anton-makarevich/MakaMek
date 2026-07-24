using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Sanet.MakaMek.Hub.Contracts;
using Sanet.MakaMek.Hub.Rooms;
using Sanet.MakaMek.Hub.Security;
using HubOptions = Sanet.MakaMek.Hub.Configuration.HubOptions;

namespace Sanet.MakaMek.Hub.Relay;

/// <summary>
/// Transport-only SignalR hub. Connection auth and room binding happen in middleware;
/// this hub attaches the connection to its room group and fans out opaque envelopes.
/// </summary>
public sealed class RelayHub : Hub<IRelayHub>
{
    /// <summary>
    /// Extra bytes reserved beyond <see cref="HubOptions.MaxRelayPayloadBytes"/> so the
    /// transport can accept a full serialized <see cref="RelayEnvelope"/> without disconnecting.
    /// Precise payload enforcement still happens inside <see cref="Relay"/>.
    /// </summary>
    public const int ReceiveMessageSizeOverheadBytes = 64 * 1024;

    private readonly IRelayRateLimiter _rateLimiter;
    private readonly IRoomManager _roomManager;
    private readonly IOptions<HubOptions> _options;

    public RelayHub(
        IRelayRateLimiter rateLimiter,
        IRoomManager roomManager,
        IOptions<HubOptions> options)
    {
        _rateLimiter = rateLimiter;
        _roomManager = roomManager;
        _options = options;
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext?.Items[RelayAuthenticationDefaults.AuthenticatedSessionItemKey]
            is not RoomSession session)
        {
            Context.Abort();
            return;
        }

        var replacedConnectionId = _roomManager.RegisterConnection(
            session.RoomCode,
            session.PlayerId,
            Context.ConnectionId);

        if (session.Role == RoomRole.Host)
        {
            _roomManager.CancelRoomDissolution(session.RoomCode);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, session.RoomCode);

        if (replacedConnectionId is not null)
        {
            await Groups.RemoveFromGroupAsync(replacedConnectionId, session.RoomCode);
        }

        if (session.Role == RoomRole.Client)
        {
            var hostConnectionId = _roomManager.GetHostConnectionId(session.RoomCode);
            if (hostConnectionId is not null)
            {
                if (replacedConnectionId is not null)
                {
                    await Clients.Client(hostConnectionId).OnPeerDisconnected(replacedConnectionId);
                }

                await Clients.Client(hostConnectionId).OnPeerConnected(Context.ConnectionId);
            }
        }

        await base.OnConnectedAsync();
    }

    public async Task Relay(string roomCode, RelayEnvelope message)
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext?.Items[RelayAuthenticationDefaults.AuthenticatedSessionItemKey]
            is not RoomSession session)
        {
            throw new HubException("Authenticated session is missing.");
        }

        if (!string.Equals(roomCode, session.RoomCode, StringComparison.Ordinal))
        {
            throw new HubException("Caller is not a member of the specified room.");
        }

        if (message.Payload is null)
        {
            throw new HubException("Payload must not be null.");
        }

        var payloadBytes = Encoding.UTF8.GetByteCount(message.Payload);
        if (payloadBytes > _options.Value.MaxRelayPayloadBytes)
        {
            throw new HubException(nameof(HubErrorCode.MessageTooLarge));
        }

        if (!_rateLimiter.TryConsume(Context.ConnectionId))
        {
            throw new HubException(nameof(HubErrorCode.RateLimited));
        }

        // Reject calls from a superseded (stale) connection.
        var activeConnectionId = _roomManager.GetConnectionId(session.RoomCode, session.PlayerId);
        if (!string.Equals(activeConnectionId, Context.ConnectionId, StringComparison.Ordinal))
        {
            throw new HubException(nameof(HubErrorCode.ConnectionSuperseded));
        }

        // Hub-tagged identity: overwrite any client-supplied SenderId.
        var outbound = message with { SenderId = Context.ConnectionId };

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
                    session.RoomCode, session.PlayerId, Context.ConnectionId);

                if (hostDisconnected)
                {
                    await Clients.Group(session.RoomCode).OnError(new HubError(
                        HubErrorCode.HostDisconnected,
                        "The room host disconnected."));
                }
            }
            else
            {
                var wasActive = _roomManager.UnregisterConnection(
                    session.RoomCode,
                    session.PlayerId,
                    Context.ConnectionId);

                if (wasActive)
                {
                    var hostConnectionId = _roomManager.GetHostConnectionId(session.RoomCode);
                    if (hostConnectionId is not null)
                    {
                        await Clients.Client(hostConnectionId).OnPeerDisconnected(Context.ConnectionId);
                    }
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}

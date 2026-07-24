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
    private readonly IOptions<HubOptions> _options;
    private readonly TimeProvider _timeProvider;

    public RelayHub(
        IRelayRateLimiter rateLimiter,
        IOptions<HubOptions> options,
        TimeProvider timeProvider)
    {
        _rateLimiter = rateLimiter;
        _options = options;
        _timeProvider = timeProvider;
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

        await Groups.AddToGroupAsync(Context.ConnectionId, session.RoomCode);
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

        var payloadLength = message.Payload.Length;
        if (payloadLength > _options.Value.MaxRelayPayloadBytes)
        {
            throw new HubException(HubErrorCode.MessageTooLarge.ToString());
        }

        if (!_rateLimiter.TryConsume(Context.ConnectionId))
        {
            throw new HubException(HubErrorCode.RateLimited.ToString());
        }

        // Hub-tagged identity: overwrite any client-supplied SenderId.
        var outbound = message with { SenderId = Context.ConnectionId };

        await Clients.OthersInGroup(session.RoomCode).OnReceive(outbound);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _rateLimiter.RemoveConnection(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

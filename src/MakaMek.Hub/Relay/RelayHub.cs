using Microsoft.AspNetCore.SignalR;
using Sanet.MakaMek.Hub.Rooms;
using Sanet.MakaMek.Hub.Security;

namespace Sanet.MakaMek.Hub.Relay;

/// <summary>
/// Transport-only SignalR hub. Connection auth and room binding happen in middleware;
/// this hub only attaches the connection to its room group.
/// </summary>
public sealed class RelayHub : Microsoft.AspNetCore.SignalR.Hub
{
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
}

using Sanet.Transport.SignalR.Client.Relay;

namespace Sanet.MakaMek.Hub.Relay;

using Contracts;

/// <summary>
/// Client-callback contract for relay fan-out. Hub methods live on <see cref="RelayHub"/>.
/// </summary>
public interface IRelayHub
{
    Task OnReceive(RelayEnvelope message);
    Task OnPeerConnected(string peerId);
    Task OnPeerDisconnected(string peerId);
    Task OnError(HubError error);
}

namespace Sanet.MakaMek.Hub.Relay;

/// <summary>
/// Client-callback contract for relay fan-out. Hub methods live on <see cref="RelayHub"/>.
/// </summary>
public interface IRelayHub
{
    Task OnReceive(RelayEnvelope message);
}

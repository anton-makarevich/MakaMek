namespace Sanet.MakaMek.Core.Services.Transport.Relay;

/// <summary>
/// Well-known paths of the MakaMek Hub used by relay transports.
/// </summary>
public static class RelayHubDefaults
{
    /// <summary>
    /// Route of the SignalR <c>RelayHub</c> (see MakaMek.Hub <c>RelayAuthenticationDefaults</c>).
    /// </summary>
    public const string HubPath = "/hubs/relay";
}

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

    /// <summary>
    /// Builds the relay hub URL from a hub base URL by appending <see cref="HubPath"/>.
    /// </summary>
    /// <param name="baseUrl">Base URL of the MakaMek Hub (may include a trailing slash).</param>
    public static string BuildHubUrl(string baseUrl)
    {
        return $"{baseUrl.TrimEnd('/')}{HubPath}";
    }
}

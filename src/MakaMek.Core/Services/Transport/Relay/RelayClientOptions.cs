namespace Sanet.MakaMek.Core.Services.Transport.Relay;

/// <summary>
/// Configuration for the typed Hub REST room-management client.
/// Bound via <c>IOptions&lt;RelayClientOptions&gt;</c>; platforms supply values through their own configuration source.
/// </summary>
public sealed class RelayClientOptions
{
    public const string SectionName = "RelayClient";

    /// <summary>
    /// Base URL of the MakaMek Hub (e.g. <c>https://hub.example.com</c>).
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Shared API key sent as the <c>X-Api-Key</c> header on every REST call.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}

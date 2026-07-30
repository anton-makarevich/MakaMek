namespace Sanet.MakaMek.Core.Services.Transport.Relay;

/// <summary>
/// Client-facing error. <see cref="Message"/> is always safe for display and logging —
/// it never embeds API keys or session tokens.
/// </summary>
public sealed record RelayClientError(
    RelayClientErrorCode Code,
    string Message);

namespace Sanet.MakaMek.Core.Services.Transport.Relay;

/// <summary>
/// Reachability state of a relay hub as surfaced to the UI.
/// </summary>
public enum HubStatus
{
    /// <summary>No probe has completed for this hub yet.</summary>
    Unknown,

    /// <summary>A health probe is currently in flight.</summary>
    Checking,

    /// <summary>The last health probe reported the hub as reachable.</summary>
    Online,

    /// <summary>The last health probe reported the hub as not reachable.</summary>
    Offline
}

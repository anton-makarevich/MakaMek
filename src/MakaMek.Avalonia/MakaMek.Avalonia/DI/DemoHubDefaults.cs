namespace Sanet.MakaMek.Avalonia.DI;

/// <summary>
/// Resolves the built-in Demo hub connection defaults.
/// Values are embedded at build time via the DemoHubBaseUrl/DemoHubApiKey MSBuild properties,
/// emitted as constants in a generated partial (see DemoHubDefaults.targets) — no reflection.
/// For development, add/edit hubs in the app Settings instead of overriding these values.
/// Note: build-time values are extractable from client binaries by design; the demo hub
/// relies on rate limiting and short-lived ticket auth for abuse protection.
/// </summary>
public static partial class DemoHubDefaults
{
    private const string DefaultLocalBaseUrl = "http://localhost:8080";

    public static string BaseUrl => BuildTimeBaseUrl ?? DefaultLocalBaseUrl;

    public static string ApiKey => BuildTimeApiKey ?? string.Empty;
}

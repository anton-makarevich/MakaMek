using System;
using System.Linq;
using System.Reflection;

namespace Sanet.MakaMek.Avalonia.DI;

/// <summary>
/// Resolves the built-in Demo hub connection defaults.
/// Values embedded at build time (via the DemoHubBaseUrl/DemoHubApiKey MSBuild properties,
/// emitted as AssemblyMetadata) are used unless overridden by environment variables
/// (MAKAMEK_RELAY_BASE_URL / MAKAMEK_RELAY_API_KEY), which take precedence for local development.
/// Note: build-time values are extractable from client binaries by design; the demo hub
/// relies on rate limiting and short-lived ticket auth for abuse protection.
/// </summary>
public static class DemoHubDefaults
{
    private const string BaseUrlEnvVar = "MAKAMEK_RELAY_BASE_URL";
    private const string ApiKeyEnvVar = "MAKAMEK_RELAY_API_KEY";
    private const string DefaultLocalBaseUrl = "http://localhost:8080";

    private static readonly Lazy<string?> BuildTimeBaseUrl = new(() => ReadMetadata("DemoHubBaseUrl"));
    private static readonly Lazy<string?> BuildTimeApiKey = new(() => ReadMetadata("DemoHubApiKey"));

    public static string BaseUrl =>
        Environment.GetEnvironmentVariable(BaseUrlEnvVar)
        ?? BuildTimeBaseUrl.Value
        ?? DefaultLocalBaseUrl;

    public static string ApiKey =>
        Environment.GetEnvironmentVariable(ApiKeyEnvVar)
        ?? BuildTimeApiKey.Value
        ?? string.Empty;

    private static string? ReadMetadata(string key)
    {
        // Referencing the attribute type keeps it alive under trimming so assembly-level
        // metadata survives linker/AOT processing.
        _ = typeof(AssemblyMetadataAttribute);
        var value = Assembly.GetEntryAssembly()?.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == key)?.Value
            ?? Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == key)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

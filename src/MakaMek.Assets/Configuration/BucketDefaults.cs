namespace Sanet.MakaMek.Assets.Configuration;

/// <summary>
/// Resolves the default data bucket (assets provider) base URL.
/// Values are embedded at build time via the DataBucketBaseUrl MSBuild property,
/// emitted as a constant in a generated partial (see BucketDefaults.targets) — no reflection.
/// Release/prod builds pass -p:DataBucketBaseUrl (CI) and point at the bucket provisioned by
/// the data-release pipeline; dev builds fall back to the public bucket URL.
/// The bucket is the single built-in assets provider for every head (the GitHub Contents API is
/// rate limited for anonymous callers); users can still add further providers in Settings
/// (see issues #1332 / #1333).
/// </summary>
public static partial class BucketDefaults
{
    private const string DefaultDataBucketBaseUrl = "https://data.makamek.nl";

    /// <summary>
    /// Base URL of the bucket that serves the per-asset-type manifests and file content.
    /// </summary>
    public static string BaseUrl => Resolve(BuildTimeBaseUrl);

    /// <summary>
    /// Resolves the effective base URL: falls back to the public bucket origin when no build-time
    /// value is embedded (null, empty or whitespace-only) and trims whitespace so a stray space in
    /// the build-time property cannot leak into the manifest URLs served by the bucket.
    /// </summary>
    /// <param name="buildTimeBaseUrl">The build-time embedded URL, or blank for dev/test builds</param>
    internal static string Resolve(string? buildTimeBaseUrl)
    {
        return string.IsNullOrWhiteSpace(buildTimeBaseUrl)
            ? DefaultDataBucketBaseUrl
            : buildTimeBaseUrl.Trim();
    }
}

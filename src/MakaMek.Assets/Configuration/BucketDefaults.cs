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
    /// Whitespace is trimmed so a stray space in the build-time property cannot leak into the
    /// URLs written by the data-release manifest generator.
    /// </summary>
    public static string BaseUrl => (BuildTimeBaseUrl ?? DefaultDataBucketBaseUrl).Trim();
}

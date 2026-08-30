namespace Sanet.MakaMek.Avalonia.DI;

/// <summary>
/// Resolves the default data bucket (assets provider) base URL.
/// Values are embedded at build time via the DataBucketBaseUrl MSBuild property,
/// emitted as a constant in a generated partial (see BucketDefaults.targets) — no reflection.
/// Release/prod builds pass -p:DataBucketBaseUrl (CI) and use the bucket provider as the
/// single default assets provider; dev builds fall back to the public bucket URL.
/// In the future users will be able to replace/extend providers via the app Settings
/// (see issues #1332 / #1333).
/// </summary>
public static partial class BucketDefaults
{
    private const string DefaultDataBucketBaseUrl = "https://data.makamek.nl";

    /// <summary>
    /// True when a bucket URL was embedded at build time (release/prod builds).
    /// Used to select the bucket provider as the default assets provider.
    /// </summary>
    public static bool IsConfigured => BuildTimeBaseUrl != null;

    public static string BaseUrl => BuildTimeBaseUrl ?? DefaultDataBucketBaseUrl;
}

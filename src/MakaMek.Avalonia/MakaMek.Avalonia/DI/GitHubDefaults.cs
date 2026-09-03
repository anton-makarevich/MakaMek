namespace Sanet.MakaMek.Avalonia.DI;

/// <summary>
/// Provides the default GitHub data-folder Contents API base URL used as the dev fallback
/// assets provider (see <see cref="BucketDefaults"/>). The asset-type subfolder
/// (e.g. "units/mechs") is appended by <see cref="ResourceStreamProviderFactory"/>.
/// </summary>
public static class GitHubDefaults
{
    /// <summary>
    /// Root Contents API URL pointing to the "data" folder of the default assets repository.
    /// Per-asset-type subfolders are resolved from the asset type.
    /// </summary>
    public const string BaseUrl = "https://api.github.com/repos/anton-makarevich/MakaMek/contents/data";
}

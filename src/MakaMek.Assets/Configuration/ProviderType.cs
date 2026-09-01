namespace Sanet.MakaMek.Assets.Configuration;

/// <summary>
/// The type of an asset source provider, which determines how its assets are resolved.
/// </summary>
public enum ProviderType
{
    /// <summary>
    /// Remote assets served from a public S3-compatible bucket (e.g. Cloudflare R2),
    /// consumed through a per-asset-type manifest.json.
    /// </summary>
    Bucket,

    /// <summary>
    /// Remote assets served from a GitHub repository via the Contents API.
    /// </summary>
    GitHub,

    /// <summary>
    /// Local assets served from a directory on the local filesystem.
    /// </summary>
    Filesystem
}

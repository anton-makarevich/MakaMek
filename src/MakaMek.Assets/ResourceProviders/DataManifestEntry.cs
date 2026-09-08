using System.Text.Json.Serialization;

namespace Sanet.MakaMek.Assets.ResourceProviders;

/// <summary>
/// Entry in a <see cref="DataManifest"/> describing a single published file
/// </summary>
public sealed class DataManifestEntry
{
    /// <summary>
    /// Path relative to the data/ root, using forward slashes
    /// </summary>
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    /// <summary>
    /// File name of the asset
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Lowercase SHA-256 content hash of the file, used for cache versioning
    /// </summary>
    [JsonPropertyName("hash")]
    public string? Hash { get; set; }

    /// <summary>
    /// Public download URL of the file
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
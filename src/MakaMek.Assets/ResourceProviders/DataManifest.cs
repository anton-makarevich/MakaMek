using System.Text.Json.Serialization;

namespace Sanet.MakaMek.Assets.ResourceProviders;

/// <summary>
/// Describes the manifest.json file written at the root of each asset type
/// (e.g. data/units/manifest.json) by the data-release pipeline. Shared between the
/// bucket resource provider (consumer) and the manifest generation script (producer).
/// </summary>
public sealed class DataManifest
{
    /// <summary>
    /// Version of the released data set
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>
    /// UTC timestamp of when the manifest was generated
    /// </summary>
    [JsonPropertyName("generatedAtUtc")]
    public string? GeneratedAtUtc { get; set; }

    /// <summary>
    /// Total number of files described by the manifest
    /// </summary>
    [JsonPropertyName("fileCount")]
    public int FileCount { get; set; }

    /// <summary>
    /// Per-file entries for every asset published for this asset type
    /// </summary>
    [JsonPropertyName("files")]
    public List<DataManifestEntry>? Files { get; set; }
}
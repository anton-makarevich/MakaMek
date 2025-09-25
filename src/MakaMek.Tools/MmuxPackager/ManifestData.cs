using System.Text.Json.Serialization;

namespace MakaMek.Tools.MmuxPackager;

/// <summary>
/// Represents the manifest.json structure for .mmux packages
/// </summary>
public record struct ManifestData
{
    /// <summary>
    /// Version of the manifest format
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; }

    /// <summary>
    /// Unique identifier for the unit (matches the Model property)
    /// </summary>
    [JsonPropertyName("unitId")]
    public string UnitId { get; init; }

    /// <summary>
    /// Author of the unit data (can be empty)
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; init; }

    /// <summary>
    /// Source publication or reference (can be empty)
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; init; }

    /// <summary>
    /// Required MakaMek version for this package
    /// </summary>
    [JsonPropertyName("requiredMakaMekVersion")]
    public string RequiredMakaMekVersion { get; init; }

    /// <summary>
    /// Constructor for ManifestData
    /// </summary>
    public ManifestData(string version, string unitId, string requiredMakaMekVersion, string author = "", string source = "")
    {
        Version = version;
        UnitId = unitId;
        RequiredMakaMekVersion = requiredMakaMekVersion;
        Author = author;
        Source = source;
    }
}

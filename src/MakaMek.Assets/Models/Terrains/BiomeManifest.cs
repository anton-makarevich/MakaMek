using System.Text.Json.Serialization;

namespace Sanet.MakaMek.Assets.Models.Terrains;

/// <summary>
/// Represents the manifest.json file inside an MMTX terrain package
/// </summary>
public class BiomeManifest
{
    /// <summary>
    /// Unique identifier for this terrain theme
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Human-readable name of the theme
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Version of the theme package (semver format)
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
    
    /// <summary>
    /// Minimum required MakaMek version to use this theme
    /// </summary>
    [JsonPropertyName("requiredMakaMekVersion")]
    public string? RequiredMakaMekVersion { get; set; }
    
    /// <summary>
    /// Description of the theme
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    /// <summary>
    /// Author/creator of the theme
    /// </summary>
    [JsonPropertyName("author")]
    public string? Author { get; set; }
}

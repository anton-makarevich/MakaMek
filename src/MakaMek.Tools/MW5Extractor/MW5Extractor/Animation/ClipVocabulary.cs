using System.Text.Json;
using System.Text.Json.Serialization;

namespace MakaMek.Tools.MW5Extractor.Animation;

/// <summary>One canonical clip and the MW5 asset-name patterns that resolve to it.</summary>
public sealed record ClipDefinition
{
    /// <summary>Canonical name from the design vocabulary: <c>Idle</c>, <c>Walk</c>, <c>Run</c>, …</summary>
    public required string Name { get; init; }

    public required bool Loop { get; init; }

    /// <summary><c>last</c> for one-shot clips that stay on their final pose, otherwise <c>null</c>.</summary>
    public string? Hold { get; init; }

    /// <summary>Ranked name suffixes; the first pattern that matches wins.</summary>
    public required IReadOnlyList<string> Patterns { get; init; }
}

/// <summary>
/// The canonical clip vocabulary, loaded from <c>clip-map.json</c>.
/// </summary>
/// <remarks>
/// Source clip names are chassis-specific aliases (<c>AS7_Stand_ANI</c>, <c>AS7_WalkForward_Straight_ANI</c>, …).
/// Keeping the mapping in data - and the pattern list ordered - means a new chassis or a better alias is a file
/// edit, not a code change.
/// </remarks>
public sealed class ClipVocabulary
{
    private readonly List<ClipDefinition> _clips;
    private readonly List<string> _exclude;
    private readonly Dictionary<string, string> _absent;

    private ClipVocabulary(List<ClipDefinition> clips, List<string> exclude, Dictionary<string, string> absent)
    {
        _clips = clips;
        _exclude = exclude;
        _absent = absent;
    }

    public IReadOnlyList<ClipDefinition> Clips => _clips;

    /// <summary>Clips MW5 does not ship, with the reason they have to be authored instead.</summary>
    public IReadOnlyDictionary<string, string> Absent => _absent;

    public IReadOnlyList<string> Exclude => _exclude;

    /// <summary>The committed default map, next to the CLI's assemblies.</summary>
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "clip-map.json");

    public static ClipVocabulary Load(string? path = null)
    {
        var file = string.IsNullOrWhiteSpace(path) ? DefaultPath : Path.GetFullPath(path);
        if (!File.Exists(file))
        {
            throw new FileNotFoundException(
                $"The clip map was not found at '{file}'. It ships next to the CLI (clip-map.json); " +
                "pass --clip-map to use a different vocabulary.",
                file);
        }

        var document = JsonSerializer.Deserialize<ClipMapDocument>(File.ReadAllText(file), JsonOptions)
                       ?? throw new InvalidDataException($"'{file}' is empty or not a clip map.");

        if (document.Clips.Count == 0)
        {
            throw new InvalidDataException($"'{file}' defines no clips.");
        }

        foreach (var clip in document.Clips)
        {
            if (string.IsNullOrWhiteSpace(clip.Name) || clip.Patterns.Count == 0)
            {
                throw new InvalidDataException($"'{file}': every clip needs a name and at least one pattern.");
            }
        }

        return new ClipVocabulary(
            document.Clips.ToList(),
            document.Exclude.ToList(),
            new Dictionary<string, string>(document.Absent, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>True when the asset name is a variant the vocabulary ignores (first person, limping, turning, …).</summary>
    public bool IsExcluded(string assetName) =>
        _exclude.Any(token => assetName.Contains(token, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Resolves an asset name to a canonical clip. Patterns are tried in their declared order, so a more specific
    /// alias (<c>_WalkForward_Straight_ANI</c>) always wins over the looser fallback (<c>_Walk_ANI</c>).
    /// </summary>
    public (ClipDefinition Definition, int Rank)? Match(string assetName)
    {
        foreach (var clip in _clips)
        {
            for (var rank = 0; rank < clip.Patterns.Count; rank++)
            {
                if (assetName.EndsWith(clip.Patterns[rank], StringComparison.OrdinalIgnoreCase))
                {
                    // The rank is the pattern index: a lower number is the more specific alias.
                    return (clip, rank);
                }
            }
        }

        return null;
    }

    private sealed record ClipMapDocument
    {
        [JsonPropertyName("version")]
        public int Version { get; init; }

        [JsonPropertyName("exclude")]
        public List<string> Exclude { get; init; } = [];

        [JsonPropertyName("clips")]
        public List<ClipDefinition> Clips { get; init; } = [];

        [JsonPropertyName("absent")]
        public Dictionary<string, string> Absent { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}

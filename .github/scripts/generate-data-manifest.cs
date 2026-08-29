// Generates manifest.json for the data/ folder: relative path, SHA-256 content hash,
// and public download URL for every file (recursively). Used by deploy-data-release.yml.
// Run with: dotnet run --file .github/scripts/generate-data-manifest.cs -- data manifest.json
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

static IEnumerable<string> Walk(string dir)
{
    foreach (var entry in Directory.EnumerateFileSystemEntries(dir))
    {
        if (Directory.Exists(entry))
        {
            foreach (var file in Walk(entry))
            {
                yield return file;
            }
        }
        else
        {
            yield return entry;
        }
    }
}

var dataDir = args.Length > 0 ? args[0] : "data";
var outputFile = args.Length > 1 ? args[1] : "manifest.json";
var baseUrl = Environment.GetEnvironmentVariable("DATA_R2_BASE_URL");

if (string.IsNullOrEmpty(baseUrl))
{
    Console.Error.WriteLine("DATA_R2_BASE_URL is not set.");
    return 1;
}

var normalizedBase = baseUrl.TrimEnd('/');

if (!Directory.Exists(dataDir))
{
    Console.Error.WriteLine($"Data directory not found: {dataDir}");
    return 1;
}

string ToUrl(string relativePath)
{
    var encoded = string.Join('/',
        relativePath.Split('/').Select(Uri.EscapeDataString));
    return $"{normalizedBase}/{encoded}";
}

try
{
    var filePaths = Walk(dataDir).OrderBy(p => p, StringComparer.Ordinal).ToList();
    var entries = new List<ManifestEntry>();

    foreach (var fullPath in filePaths)
    {
        var relativePath = Path.GetRelativePath(dataDir, fullPath).Replace('\\', '/');
        var sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fullPath))).ToLowerInvariant();
        entries.Add(new ManifestEntry
        {
            Path = relativePath,
            Name = relativePath[(relativePath.LastIndexOf('/') + 1)..],
            Hash = sha256,
            Url = ToUrl(relativePath),
        });
    }

    var manifest = new Manifest
    {
        Version = 1,
        GeneratedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
        FileCount = entries.Count,
        Files = entries,
    };

    using (var stream = File.Create(outputFile))
    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
    {
        JsonSerializer.Serialize(writer, manifest, ManifestJsonContext.Default.Manifest);
        writer.Flush();
        stream.WriteByte((byte)'\n');
    }
    Console.WriteLine($"Generated {outputFile} with {entries.Count} file(s) from {dataDir}/");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("Failed to generate data manifest: " + ex);
    return 1;
}

[JsonSerializable(typeof(Manifest))]
[JsonSerializable(typeof(ManifestEntry))]
internal partial class ManifestJsonContext : JsonSerializerContext
{
}

internal sealed class Manifest
{
    [JsonPropertyName("version")]
    public int Version { get; set; }
    [JsonPropertyName("generatedAtUtc")]
    public string? GeneratedAtUtc { get; set; }
    [JsonPropertyName("fileCount")]
    public int FileCount { get; set; }
    [JsonPropertyName("files")]
    public List<ManifestEntry>? Files { get; set; }
}

internal sealed class ManifestEntry
{
    [JsonPropertyName("path")]
    public string? Path { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("hash")]
    public string? Hash { get; set; }
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

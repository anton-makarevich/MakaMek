using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Sanet.MakaMek.Services.ResourceProviders;

/// <summary>
/// Resource stream provider that fetches assets from a public S3-compatible bucket
/// (e.g. Cloudflare R2) over plain HttpClient, consuming the manifest.json produced by the
/// data-release pipeline. No S3 SDK dependency — keeps WASM compatibility.
/// </summary>
public class BucketResourceStreamProvider : RemoteResourceStreamProvider
{
    private readonly string _manifestUrl;
    private readonly string _pathPrefix;
    private readonly string _fileExtension;

    /// <summary>
    /// Initializes a new instance of BucketResourceStreamProvider
    /// </summary>
    /// <param name="pathPrefix">Only files whose manifest path starts with this prefix are included (e.g. "units/mechs")</param>
    /// <param name="fileExtension">Files with this extension will be included</param>
    /// <param name="baseUrl">Base URL of the bucket serving manifest.json and file content</param>
    /// <param name="logger">Logger for class</param>
    /// <param name="httpClient">HTTP client to use for requests. If null, create a new one.</param>
    /// <param name="cachingService">Caching service to cache downloaded files</param>
    public BucketResourceStreamProvider(
        string pathPrefix,
        string fileExtension,
        string baseUrl,
        IFileCachingService cachingService,
        ILogger<BucketResourceStreamProvider> logger,
        HttpClient? httpClient = null)
        : base(cachingService, logger, httpClient)
    {
        _manifestUrl = $"{baseUrl.TrimEnd('/')}/manifest.json";
        _pathPrefix = pathPrefix;
        _fileExtension = fileExtension;
    }

    protected override string ListingDescription => "bucket manifest";

    protected override string CachedListingDescription => "manifest";

    /// <summary>
    /// Loads available resource IDs by fetching the bucket manifest
    /// </summary>
    /// <returns>List of (download URL, hash) tuples for files matching prefix and extension</returns>
    protected override Task<List<(string Url, string Sha)>> LoadAvailableResourceIds()
    {
        return FetchListingAsync<Manifest>(_manifestUrl, manifest =>
        [
            .. (manifest?.Files ?? [])
            .Where(file => file.Path?.StartsWith(_pathPrefix, StringComparison.OrdinalIgnoreCase) == true &&
                           file.Name?.EndsWith($".{_fileExtension}", StringComparison.OrdinalIgnoreCase) == true &&
                           !string.IsNullOrEmpty(file.Url))
            .Select(file => (Url: file.Url!, Sha: file.Hash ?? string.Empty))
        ]);
    }

    private class Manifest
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("generatedAtUtc")]
        public string? GeneratedAtUtc { get; set; }

        [JsonPropertyName("fileCount")]
        public int FileCount { get; set; }

        [JsonPropertyName("files")]
        public List<ManifestEntry>? Files { get; set; }
    }

    private class ManifestEntry
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
}

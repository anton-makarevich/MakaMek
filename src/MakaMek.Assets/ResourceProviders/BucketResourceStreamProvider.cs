using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Assets.ResourceProviders;

/// <summary>
/// Resource stream provider that fetches assets from a public S3-compatible bucket
/// (e.g. Cloudflare R2) over plain HttpClient, consuming the per-asset-type manifest.json
/// (e.g. "units/manifest.json") produced by the data-release pipeline. No S3 SDK dependency —
/// keeps WASM compatibility.
/// </summary>
public class BucketResourceStreamProvider : RemoteResourceStreamProvider
{
    private readonly string _manifestUrl;
    private readonly string _fileExtension;

    /// <summary>
    /// Initializes a new instance of BucketResourceStreamProvider
    /// </summary>
    /// <param name="manifestPath">Bucket-relative path of the asset type's manifest (e.g. "units/manifest.json")</param>
    /// <param name="fileExtension">Files with this extension will be included</param>
    /// <param name="baseUrl">Base URL of the bucket serving per-asset-type manifests and file content</param>
    /// <param name="logger">Logger for class</param>
    /// <param name="httpClient">HTTP client to use for requests. If null, create a new one.</param>
    /// <param name="cachingService">Caching service to cache downloaded files</param>
    public BucketResourceStreamProvider(
        string manifestPath,
        string fileExtension,
        string baseUrl,
        IFileCachingService cachingService,
        ILogger<BucketResourceStreamProvider> logger,
        HttpClient? httpClient = null)
        : base(cachingService, logger, httpClient)
    {
        _manifestUrl = $"{baseUrl.TrimEnd('/')}/{manifestPath}";
        _fileExtension = fileExtension;
    }

    protected override string ListingDescription => "bucket manifest";

    protected override string CachedListingDescription => "manifest";

    /// <summary>
    /// Loads available resource IDs by fetching the asset type's manifest
    /// </summary>
    /// <returns>List of (download URL, hash) tuples for files matching the extension</returns>
    protected override Task<List<(string Url, string Sha)>> LoadAvailableResourceIds()
    {
        return FetchListingAsync<Manifest>(_manifestUrl, manifest =>
        [
            .. (manifest?.Files ?? [])
            .Where(file => file.Name?.EndsWith($".{_fileExtension}", StringComparison.OrdinalIgnoreCase) == true &&
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

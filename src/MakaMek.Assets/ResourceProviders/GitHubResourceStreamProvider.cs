using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Assets.ResourceProviders;

public class GitHubResourceStreamProvider : RemoteResourceStreamProvider
{
    private readonly string _apiUrl;
    private readonly string _fileExtension;

    /// <summary>
    /// Initializes a new instance of GitHubResourceStreamProvider
    /// </summary>
    /// <param name="apiUrl">GitHub API URL pointing to the folder with resources.</param>
    /// <param name="fileExtension">Files with this extension will be included</param>
    /// <param name="logger">Logger for class</param>
    /// <param name="httpClient">HTTP client to use for requests. If null, create a new one.</param>
    /// <param name="cachingService">Caching service to cache downloaded files</param>
    public GitHubResourceStreamProvider(
        string fileExtension,
        string apiUrl,
        IFileCachingService cachingService,
        ILogger<GitHubResourceStreamProvider> logger,
        HttpClient? httpClient = null)
        : base(cachingService, logger, httpClient)
    {
        _apiUrl = apiUrl;
        _fileExtension = fileExtension;
    }

    protected override string ListingDescription => "GitHub contents";

    protected override string CachedListingDescription => "API manifest";

    /// <summary>
    /// Loads available resource IDs by querying the GitHub Contents API
    /// </summary>
    /// <returns>List of (download URL, SHA) tuples for files with the specified extension</returns>
    protected override Task<List<(string Url, string Sha)>> LoadAvailableResourceIds()
    {
        return FetchListingAsync<GitHubContentItem[]>(_apiUrl, contentItems =>
            (contentItems ?? [])
                .Where(item => item.Type == "file" &&
                               item.Name.EndsWith($".{_fileExtension}", StringComparison.OrdinalIgnoreCase) &&
                               !string.IsNullOrEmpty(item.DownloadUrl))
                .Select(item => (Url: item.DownloadUrl!, Sha: item.Sha ?? string.Empty))
                .ToList());
    }

    private class GitHubContentItem
    {
        /// <summary>
        /// The name of the file or directory
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The download URL for the raw content (only available for files)
        /// </summary>
        [JsonPropertyName("download_url")]
        public string? DownloadUrl { get; set; }

        /// <summary>
        /// The type of content (file, dir, symlink, submodule)
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// The SHA hash of the file content for versioning
        /// </summary>
        [JsonPropertyName("sha")]
        public string? Sha { get; set; }
    }
}

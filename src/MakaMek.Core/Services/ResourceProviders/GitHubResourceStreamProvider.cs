using System.Text.Json;
using System.Text.Json.Serialization;
using AsyncAwaitBestPractices;
using Microsoft.Extensions.Logging;

namespace Sanet.MakaMek.Core.Services.ResourceProviders;

/// <summary>
/// Resource stream provider that downloads unit files from a GitHub repository with caching support
/// </summary>
public class GitHubResourceStreamProvider : IResourceStreamProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;
    private readonly string _fileExtension;
    private readonly bool _disposeHttpClient;
    private readonly Lazy<Task<List<(string Url, string Sha)>>> _availableResourceIds;
    private readonly IFileCachingService _cachingService;
    private readonly ILogger<GitHubResourceStreamProvider> _logger;

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
    {
        _apiUrl = apiUrl;
        _fileExtension = fileExtension;
        _disposeHttpClient = httpClient == null;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "MakaMek-Game");
        _cachingService = cachingService;
        _logger = logger;

        _availableResourceIds = new Lazy<Task<List<(string Url, string Sha)>>>(LoadAvailableResourceIds);
    }

    /// <summary>
    /// Gets all available resource identifiers from the GitHub repository
    /// </summary>
    /// <returns>Collection of download URLs that serve as resource identifiers</returns>
    public async Task<IEnumerable<string>> GetAvailableResourceIds()
    {
        var resources = await _availableResourceIds.Value;
        return resources.Select(r => r.Url);
    }

    /// <summary>
    /// Gets a stream for the specified resource identifier (download URL)
    /// </summary>
    /// <param name="resourceId">The download URL from GitHub</param>
    /// <returns>Stream containing the file data, or null if not found</returns>
    public async Task<Stream?> GetResourceStream(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
        {
            return null;
        }

        // Look up the current SHA for this resource
        var resources = await _availableResourceIds.Value;
        var resourceInfo = resources.FirstOrDefault(r => r.Url == resourceId);
        var currentSha = resourceInfo.Sha;

        // If we have SHA info, check for stale cache
        if (!string.IsNullOrEmpty(currentSha))
        {
            var cachedVersion = await _cachingService.GetCacheVersion(resourceId);
            if (cachedVersion != currentSha && await _cachingService.IsCached(resourceId))
            {
                _logger.LogInformation(
                    "Cache version mismatch for {ResourceId}: cached {CachedVersion} vs current {CurrentVersion}. Invalidating cache.",
                    resourceId, cachedVersion ?? "<none>", currentSha);
                await _cachingService.RemoveFromCache(resourceId);
            }
        }

        // Try to get from the cache first (either fresh or after invalidation)
        var cachedBytes = await _cachingService.TryGetCachedFile(resourceId);
        if (cachedBytes != null)
        {
            return new MemoryStream(cachedBytes);
        }

        try
        {
            var response = await _httpClient.GetAsync(resourceId);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to download file from {ResourceId}: {StatusCode}", resourceId, response.StatusCode);
                return null;
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync();

            // Read the content into memory so we can cache it
            using var memoryStream = new MemoryStream();
            await contentStream.CopyToAsync(memoryStream);
            var contentBytes = memoryStream.ToArray();

            // Cache the content with version metadata if available (fire and forget)
            Task.Run(async () =>
            {
                try
                {
                    await _cachingService.SaveToCache(
                        resourceId,
                        contentBytes,
                        string.IsNullOrEmpty(currentSha) ? null : currentSha);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error caching file from {ResourceId}", resourceId);
                }
            }).SafeFireAndForget(ex => _logger.LogError(ex, "Error caching file from {ResourceId}", resourceId)); 
            return new MemoryStream(contentBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file from {ResourceId}", resourceId);
            return null;
        }
    }

    /// <summary>
    /// Loads available resource IDs by querying the GitHub Contents API
    /// </summary>
    /// <returns>List of (download URL, SHA) tuples for files with the specified extension</returns>
    private async Task<List<(string Url, string Sha)>> LoadAvailableResourceIds()
    {
        var resourceIds = new List<(string Url, string Sha)>();

        try
        {
            var response = await _httpClient.GetAsync(_apiUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch GitHub contents: {StatusCode}", response.StatusCode);
                return resourceIds;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var contentItems = JsonSerializer.Deserialize<GitHubContentItem[]>(jsonContent);

            if (contentItems == null)
            {
                _logger.LogWarning("Failed to deserialize GitHub contents response");
                return resourceIds;
            }

            foreach (var item in contentItems)
            {
                // Only include files (not directories) with the specified extension
                if (item.Type == "file" &&
                    item.Name.EndsWith($".{_fileExtension}", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(item.DownloadUrl))
                {
                    resourceIds.Add((item.DownloadUrl, item.Sha ?? string.Empty));
                }
            }

            _logger.LogInformation("Found {Count} {FileExtension} files in GitHub repository", resourceIds.Count, _fileExtension);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading GitHub contents");
        }

        return resourceIds;
    }

    /// <summary>
    /// Disposes the HTTP client if it was created internally
    /// </summary>
    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
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

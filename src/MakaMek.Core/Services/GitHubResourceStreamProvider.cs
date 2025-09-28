using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sanet.MakaMek.Core.Services;

/// <summary>
/// Resource stream provider that downloads unit files from a GitHub repository
/// </summary>
public class GitHubResourceStreamProvider : IResourceStreamProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;
    private readonly string _fileExtension;
    private readonly bool _disposeHttpClient;
    private readonly Lazy<Task<List<string>>> _availableResourceIds;

    /// <summary>
    /// Initializes a new instance of GitHubResourceStreamProvider
    /// </summary>
    /// <param name="apiUrl">GitHub API URL pointing to the folder with resources.</param>
    /// <param name="fileExtension">Files with this extension will be included</param>
    /// <param name="httpClient">HTTP client to use for requests. If null, create a new one.</param>
    public GitHubResourceStreamProvider(
        string fileExtension,
        string apiUrl,
        HttpClient? httpClient = null)
    {
        _apiUrl = apiUrl;
        _fileExtension = fileExtension;
        _disposeHttpClient = httpClient == null;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "MakaMek-Game");

        _availableResourceIds = new Lazy<Task<List<string>>>(LoadAvailableResourceIds);
    }

    /// <summary>
    /// Gets all available resource identifiers from the GitHub repository
    /// </summary>
    /// <returns>Collection of download URLs that serve as resource identifiers</returns>
    public async Task<IEnumerable<string>> GetAvailableResourceIds()
    {
        try
        {
            return await _availableResourceIds.Value;
        }
        catch (AggregateException ex)
        {
            // Unwrap the aggregate exception and log the inner exception
            var innerException = ex.InnerException ?? ex;
            Console.WriteLine($"Error loading resource IDs from GitHub: {innerException.Message}");
            return [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading resource IDs from GitHub: {ex.Message}");
            return [];
        }
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

        try
        {
            var response = await _httpClient.GetAsync(resourceId);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to download file from {resourceId}: {response.StatusCode}");
                return null;
            }

            return await response.Content.ReadAsStreamAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading file from {resourceId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Loads available resource IDs by querying the GitHub Contents API
    /// </summary>
    /// <returns>List of download URLs for files with the specified extension</returns>
    private async Task<List<string>> LoadAvailableResourceIds()
    {
        var resourceIds = new List<string>();

        try
        {
            var response = await _httpClient.GetAsync(_apiUrl);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to fetch GitHub contents: {response.StatusCode}");
                return resourceIds;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var contentItems = JsonSerializer.Deserialize<GitHubContentItem[]>(jsonContent);

            if (contentItems == null)
            {
                Console.WriteLine("Failed to deserialize GitHub contents response");
                return resourceIds;
            }

            foreach (var item in contentItems)
            {
                // Only include files (not directories) with the specified extension
                if (item.Type == "file" &&
                    item.Name.EndsWith($".{_fileExtension}", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(item.DownloadUrl))
                {
                    resourceIds.Add(item.DownloadUrl);
                }
            }

            Console.WriteLine($"Found {resourceIds.Count} {_fileExtension} files in GitHub repository");
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Network error fetching GitHub contents: {ex.Message}");
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Error parsing GitHub contents response: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading GitHub contents: {ex.Message}");
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
    }
}

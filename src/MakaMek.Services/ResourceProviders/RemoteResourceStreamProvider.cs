using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Sanet.MakaMek.Services.ResourceProviders;

/// <summary>
/// Base class for network-backed resource stream providers.
/// Fetches a remote JSON listing of resources over HTTP, downloads file content, and shares
/// caching, hash-based cache versioning and stale/offline fallback semantics through
/// <see cref="IFileCachingService"/>.
/// </summary>
public abstract class RemoteResourceStreamProvider : IResourceStreamProvider
{
    private readonly HttpClient _httpClient;
    private readonly bool _disposeHttpClient;
    private readonly Lazy<Task<List<(string Url, string Sha)>>> _availableResourceIds;
    private readonly IFileCachingService _cachingService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="RemoteResourceStreamProvider"/>
    /// </summary>
    /// <param name="cachingService">Caching service to cache downloaded files and listings</param>
    /// <param name="logger">Logger for the derived provider type</param>
    /// <param name="httpClient">HTTP client to use for requests. If null, create a new one.</param>
    protected RemoteResourceStreamProvider(
        IFileCachingService cachingService,
        ILogger logger,
        HttpClient? httpClient = null)
    {
        _cachingService = cachingService;
        _logger = logger;
        if (httpClient != null)
        {
            _httpClient = httpClient;
            _disposeHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MakaMek-Game");
            _disposeHttpClient = true;
        }

        _availableResourceIds = new Lazy<Task<List<(string Url, string Sha)>>>(LoadAvailableResourceIds);
    }

    /// <summary>
    /// Description of the remote listing, used in log messages (e.g. "GitHub contents")
    /// </summary>
    protected abstract string ListingDescription { get; }

    /// <summary>
    /// Short name of the cached listing, used in log messages (e.g. "API manifest")
    /// </summary>
    protected abstract string CachedListingDescription { get; }

    /// <summary>
    /// Loads available resource IDs from the remote listing
    /// </summary>
    /// <returns>List of (resource id, version hash) tuples for matching files</returns>
    protected abstract Task<List<(string Url, string Sha)>> LoadAvailableResourceIds();

    /// <summary>
    /// Gets all available resource identifiers from the remote source
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
    /// <param name="resourceId">The download URL of the resource</param>
    /// <returns>Stream containing the file data, or null if not found</returns>
    public async Task<Stream?> GetResourceStream(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
        {
            return null;
        }

        // Look up the current hash for this resource
        var resources = await _availableResourceIds.Value;
        var resourceInfo = resources.FirstOrDefault(r => r.Url == resourceId);
        var currentSha = resourceInfo.Sha;

        var needsFreshDownload = false;
        if (!string.IsNullOrEmpty(currentSha))
        {
            var cachedVersion = await _cachingService.GetCacheVersion(resourceId);
            if (cachedVersion != currentSha)
            {
                _logger.LogInformation(
                    "Cache version mismatch for {ResourceId}: cached {CachedVersion} vs current {CurrentVersion}.",
                    resourceId, cachedVersion ?? "<none>", currentSha);
                needsFreshDownload = true;
            }
        }

        // Return fresh cached content immediately (skip if stale and needs refresh)
        if (!needsFreshDownload)
        {
            var cachedBytes = await _cachingService.TryGetCachedFile(resourceId);
            if (cachedBytes != null)
            {
                return new MemoryStream(cachedBytes);
            }
        }

        try
        {
            using var response = await _httpClient.GetAsync(resourceId);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to download file from {ResourceId}: {StatusCode}", resourceId, response.StatusCode);

                var fallbackBytes = await _cachingService.TryGetCachedFile(resourceId);
                if (fallbackBytes == null) return null;
                _logger.LogInformation("Serving stale cached content for {ResourceId} due to download failure", resourceId);
                return new MemoryStream(fallbackBytes);

            }

            await using var contentStream = await response.Content.ReadAsStreamAsync();

            // Read the content into memory so we can cache it
            using var memoryStream = new MemoryStream();
            await contentStream.CopyToAsync(memoryStream);
            var contentBytes = memoryStream.ToArray();

            // Cache the content with version metadata if available
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
            return new MemoryStream(contentBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file from {ResourceId}", resourceId);

            var fallbackBytes = await _cachingService.TryGetCachedFile(resourceId);
            if (fallbackBytes == null) return null;
            _logger.LogInformation("Serving stale cached content for {ResourceId} due to download failure", resourceId);
            return new MemoryStream(fallbackBytes);
        }
    }

    /// <summary>
    /// Fetches a JSON listing from <paramref name="listingUrl"/>, caches the raw response for
    /// offline use and extracts resource ids using <paramref name="selector"/>.
    /// Falls back to the cached listing when the fetch or deserialization fails.
    /// </summary>
    /// <param name="listingUrl">URL of the JSON listing</param>
    /// <param name="selector">Extracts (resource id, version hash) pairs from the deserialized listing</param>
    protected async Task<List<(string Url, string Sha)>> FetchListingAsync<TListing>(
        string listingUrl,
        Func<TListing?, List<(string Url, string Sha)>> selector)
    {
        try
        {
            using var response = await _httpClient.GetAsync(listingUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch {ListingDescription} from {ListingUrl}: {StatusCode}",
                    ListingDescription, listingUrl, response.StatusCode);
                return await TryLoadCachedListing(listingUrl, selector);
            }

            var jsonContent = await response.Content.ReadAsStringAsync();

            var listing = JsonSerializer.Deserialize<TListing>(jsonContent);

            if (listing == null)
            {
                _logger.LogWarning("Failed to deserialize {ListingDescription} response", ListingDescription);
                return await TryLoadCachedListing(listingUrl, selector);
            }

            // Cache the listing response for offline use
            try
            {
                await _cachingService.SaveToCache(listingUrl, Encoding.UTF8.GetBytes(jsonContent));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching {CachedListingDescription} for {ListingUrl}",
                    CachedListingDescription, listingUrl);
            }

            var resourceIds = selector(listing);

            _logger.LogInformation("Found {Count} resources in {ListingDescription} at {ListingUrl}",
                resourceIds.Count, ListingDescription, listingUrl);
            return resourceIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading {ListingDescription}", ListingDescription);
            return await TryLoadCachedListing(listingUrl, selector);
        }
    }

    private async Task<List<(string Url, string Sha)>> TryLoadCachedListing<TListing>(
        string listingUrl,
        Func<TListing?, List<(string Url, string Sha)>> selector)
    {
        try
        {
            var cachedJson = await _cachingService.TryGetCachedFile(listingUrl);
            if (cachedJson == null)
            {
                return [];
            }

            var listing = JsonSerializer.Deserialize<TListing>(Encoding.UTF8.GetString(cachedJson));
            if (listing == null)
            {
                return [];
            }

            _logger.LogInformation("Using cached {CachedListingDescription} for offline resource discovery",
                CachedListingDescription);
            return selector(listing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading cached {CachedListingDescription}", CachedListingDescription);
            return [];
        }
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
}

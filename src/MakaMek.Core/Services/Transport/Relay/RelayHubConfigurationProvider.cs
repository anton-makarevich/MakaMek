using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Core.Services.Transport.Relay;

/// <summary>
/// Singleton that owns the active relay hub configuration. The built-in Demo hub is
/// seeded from <see cref="RelayClientOptions"/> (the environment-variable values), while
/// user-defined hubs and the active selection are persisted through <see cref="IFileCachingService"/>.
/// </summary>
public sealed class RelayHubConfigurationProvider : IRelayHubConfigurationProvider
{
    private const string CacheKey = "HubConfigurations";
    private const string DemoHubId = "demo";

    private readonly IFileCachingService _cachingService;
    private readonly ILogger<RelayHubConfigurationProvider> _logger;
    private readonly Dictionary<string, HubConfigData> _hubs = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly Task _loadTask;
    private string _activeHubId = DemoHubId;

    public RelayHubConfigurationProvider(
        IOptions<RelayClientOptions> relayOptions,
        IFileCachingService cachingService,
        ILogger<RelayHubConfigurationProvider> logger)
    {
        _cachingService = cachingService;
        _logger = logger;

        var demoHub = new HubConfigData(
            DemoHubId,
            "Demo Hub",
            relayOptions.Value.BaseUrl,
            relayOptions.Value.ApiKey,
            IsBuiltIn: true);
        _hubs[DemoHubId] = demoHub;

        _loadTask = LoadAsync();
    }

    public string ActiveHubId
    {
        get
        {
            lock (_gate)
            {
                return _activeHubId;
            }
        }
    }

    public string ActiveBaseUrl
    {
        get
        {
            lock (_gate)
            {
                return _hubs[_activeHubId].BaseUrl;
            }
        }
    }

    public string ActiveApiKey
    {
        get
        {
            lock (_gate)
            {
                return _hubs[_activeHubId].ApiKey;
            }
        }
    }

    public IReadOnlyList<HubConfigData> Hubs
    {
        get
        {
            lock (_gate)
            {
                return _hubs.Values
                    .OrderByDescending(h => h.IsBuiltIn)
                    .ThenBy(h => h.Name, StringComparer.Ordinal)
                    .ToList();
            }
        }
    }

    public Task EnsureLoadedAsync() => _loadTask;

    public async Task AddHubAsync(HubConfigData hub)
    {
        if (string.IsNullOrWhiteSpace(hub.Id))
        {
            throw new ArgumentException("Hub id is required.", nameof(hub));
        }

        await EnsureLoadedAsync();
        lock (_gate)
        {
            if (hub.Id == DemoHubId || _hubs.ContainsKey(hub.Id))
            {
                throw new ArgumentException($"Hub '{hub.Id}' already exists.", nameof(hub));
            }

            _hubs[hub.Id] = hub with { IsBuiltIn = false };
        }

        try
        {
            await PersistAsync();
        }
        catch
        {
            // Roll back the in-memory add so a failed save can be retried by the caller.
            lock (_gate)
            {
                _hubs.Remove(hub.Id);
            }
            throw;
        }
    }

    public async Task UpdateHubAsync(string id, string name, string baseUrl, string apiKey)
    {
        await EnsureLoadedAsync();
        lock (_gate)
        {
            if (!_hubs.TryGetValue(id, out var existing))
            {
                throw new ArgumentException($"Hub '{id}' does not exist.", nameof(id));
            }

            if (existing.IsBuiltIn)
            {
                throw new InvalidOperationException("The built-in Demo hub cannot be edited.");
            }

            _hubs[id] = existing with { Name = name, BaseUrl = baseUrl, ApiKey = apiKey };
        }
        await PersistAsync();
    }

    public async Task RemoveHubAsync(string id)
    {
        await EnsureLoadedAsync();
        lock (_gate)
        {
            if (!_hubs.TryGetValue(id, out var existing))
            {
                return;
            }

            if (existing.IsBuiltIn)
            {
                throw new InvalidOperationException("The built-in Demo hub cannot be removed.");
            }

            _hubs.Remove(id);
            if (_activeHubId == id)
            {
                _activeHubId = DemoHubId;
            }
        }
        await PersistAsync();
    }

    public async Task SelectHubAsync(string id)
    {
        await EnsureLoadedAsync();
        lock (_gate)
        {
            if (!_hubs.ContainsKey(id))
            {
                throw new ArgumentException($"Hub '{id}' does not exist.", nameof(id));
            }

            _activeHubId = id;
        }
        await PersistAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var cachedData = await _cachingService.TryGetCachedFile(CacheKey);
            if (cachedData is null)
            {
                return;
            }

            var json = Encoding.UTF8.GetString(cachedData);
            var state = JsonSerializer.Deserialize<HubConfigurationsState>(json);
            if (state?.Hubs is null)
            {
                return;
            }

            lock (_gate)
            {
                var knownIds = new HashSet<string>(StringComparer.Ordinal) { DemoHubId };
                foreach (var hub in state.Hubs)
                {
                    if (string.IsNullOrWhiteSpace(hub.Id) || !knownIds.Add(hub.Id))
                    {
                        continue;
                    }

                    _hubs[hub.Id] = hub with { IsBuiltIn = false };
                }

                if (!string.IsNullOrWhiteSpace(state.ActiveHubId) && _hubs.ContainsKey(state.ActiveHubId))
                {
                    _activeHubId = state.ActiveHubId;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load relay hub configurations from cache");
        }
    }

    private async Task PersistAsync()
    {
        try
        {
            HubConfigurationsState state;
            lock (_gate)
            {
                state = new HubConfigurationsState
                {
                    Hubs = _hubs.Values
                        .Where(h => !h.IsBuiltIn)
                        .ToList(),
                    ActiveHubId = _activeHubId
                };
            }

            var json = JsonSerializer.Serialize(state);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _cachingService.SaveToCache(CacheKey, bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save relay hub configurations to cache");
            throw;
        }
    }

    /// <summary>
    /// Serializable snapshot of user-defined hubs and the active selection.
    /// </summary>
    private sealed class HubConfigurationsState
    {
        public List<HubConfigData> Hubs { get; set; } = [];
        public string? ActiveHubId { get; set; }
    }
}

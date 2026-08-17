using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sanet.MakaMek.Services;
using Sanet.Transport.SignalR.Client.Relay;

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
    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
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

        _loadTask = Load();
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

    public async Task<RelayClientOptions> GetActiveOptions()
    {
        await EnsureLoadedAsync();
        lock (_gate)
        {
            var active = _hubs[_activeHubId];
            return new RelayClientOptions
            {
                BaseUrl = active.BaseUrl,
                ApiKey = active.ApiKey
            };
        }
    }

    public async Task<string> GetActiveHubId()
    {
        await EnsureLoadedAsync();
        lock (_gate)
        {
            return _activeHubId;
        }
    }

    public async Task<IReadOnlyList<HubConfigData>> GetHubs()
    {
        await EnsureLoadedAsync();
        lock (_gate)
        {
            return _hubs.Values
                .OrderByDescending(h => h.IsBuiltIn)
                .ThenBy(h => h.Name, StringComparer.Ordinal)
                .ToList();
        }
    }

    public async Task AddHub(HubConfigData hub)
    {
        await _operationGate.WaitAsync();
        try
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
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task UpdateHub(string id, string name, string baseUrl, string apiKey)
    {
        await _operationGate.WaitAsync();
        try
        {
            await EnsureLoadedAsync();
            HubConfigData previous;
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

                previous = existing;
                _hubs[id] = existing with { Name = name, BaseUrl = baseUrl, ApiKey = apiKey };
            }

            try
            {
                await PersistAsync();
            }
            catch
            {
                // Restore the previous entry so a failed save leaves the cache unchanged.
                lock (_gate)
                {
                    _hubs[id] = previous;
                }
                throw;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task RemoveHub(string id)
    {
        await _operationGate.WaitAsync();
        try
        {
            await EnsureLoadedAsync();
            HubConfigData? previous;
            bool wasActive;
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

                previous = existing;
                wasActive = _activeHubId == id;
                _hubs.Remove(id);
                if (wasActive)
                {
                    _activeHubId = DemoHubId;
                }
            }

            try
            {
                await PersistAsync();
            }
            catch
            {
                // Restore the removed entry and any active-selection fallback.
                lock (_gate)
                {
                    _hubs[id] = previous;
                    if (wasActive)
                    {
                        _activeHubId = id;
                    }
                }
                throw;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task SelectHub(string id)
    {
        await _operationGate.WaitAsync();
        try
        {
            await EnsureLoadedAsync();
            string previousActiveHubId;
            lock (_gate)
            {
                if (!_hubs.ContainsKey(id))
                {
                    throw new ArgumentException($"Hub '{id}' does not exist.", nameof(id));
                }

                previousActiveHubId = _activeHubId;
                _activeHubId = id;
            }

            try
            {
                await PersistAsync();
            }
            catch
            {
                // Restore the previous active selection so a failed save leaves the cache unchanged.
                lock (_gate)
                {
                    _activeHubId = previousActiveHubId;
                }
                throw;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task Load()
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
        public List<HubConfigData> Hubs { get; init; } = [];
        public string? ActiveHubId { get; init; }
    }
}

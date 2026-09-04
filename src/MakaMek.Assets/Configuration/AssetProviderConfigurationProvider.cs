using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Assets.Configuration;

/// <summary>
/// Singleton that owns the asset source provider configuration. Built-in defaults are seeded
/// from the constructor-injected <paramref name="defaultProviders"/> the first time the cache is
/// empty, while the (possibly mutated) set of providers is persisted through
/// <see cref="IFileCachingService"/>.
/// </summary>
public sealed class AssetProviderConfigurationProvider : IAssetProviderConfigurationProvider
{
    private const string CacheKey = "AssetProviders";

    private readonly List<AssetProviderConfigData> _defaultProviders;
    private readonly IFileCachingService _cachingService;
    private readonly ILogger<AssetProviderConfigurationProvider> _logger;
    private readonly Dictionary<string, AssetProviderConfigData> _providers = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly Task _loadTask;

    public AssetProviderConfigurationProvider(
        IEnumerable<AssetProviderConfigData> defaultProviders,
        IFileCachingService cachingService,
        ILogger<AssetProviderConfigurationProvider> logger)
    {
        _defaultProviders = (defaultProviders ?? throw new ArgumentNullException(nameof(defaultProviders))).ToList();
        _cachingService = cachingService ?? throw new ArgumentNullException(nameof(cachingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _loadTask = Load();
    }

    /// <summary>
    /// Ensures the initial load has completed so all operations observe seeded/loaded state.
    /// </summary>
    public Task EnsureLoadedAsync() => _loadTask;

    public async Task<IReadOnlyList<AssetProviderConfigData>> GetProviders()
    {
        await EnsureLoadedAsync();
        lock (_gate)
        {
            return _providers.Values
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Id, StringComparer.Ordinal)
                .ToList();
        }
    }

    public async Task<AssetProviderConfigData?> GetProvider(string id)
    {
        await EnsureLoadedAsync();
        lock (_gate)
        {
            return _providers.TryGetValue(id, out var provider) ? provider : null;
        }
    }

    public async Task AddProvider(AssetProviderConfigData provider)
    {
        await _operationGate.WaitAsync();
        try
        {
            if (string.IsNullOrWhiteSpace(provider.Id))
            {
                throw new ArgumentException("Provider id is required.", nameof(provider));
            }

            await EnsureLoadedAsync();
            lock (_gate)
            {
                if (_providers.ContainsKey(provider.Id))
                {
                    throw new ArgumentException($"Provider '{provider.Id}' already exists.", nameof(provider));
                }

                _providers[provider.Id] = provider with { IsDefault = false };
            }

            try
            {
                await PersistAsync();
            }
            catch
            {
                lock (_gate)
                {
                    _providers.Remove(provider.Id);
                }
                throw;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task UpdateProvider(string id, AssetProviderConfigData updated)
    {
        await _operationGate.WaitAsync();
        try
        {
            await EnsureLoadedAsync();
            AssetProviderConfigData previous;
            lock (_gate)
            {
                if (!_providers.TryGetValue(id, out var existing))
                {
                    throw new ArgumentException($"Provider '{id}' does not exist.", nameof(id));
                }

                previous = existing;
                if (existing.IsActive &&
                    (updated.AssetType != existing.AssetType || !updated.IsActive) &&
                    !HasOtherActiveProvider(existing.AssetType, id))
                {
                    throw new InvalidOperationException(
                        "Cannot update the only active provider for an asset type; at least one provider must remain active.");
                }
                _providers[id] = updated with { Id = existing.Id, IsDefault = existing.IsDefault };
            }

            try
            {
                await PersistAsync();
            }
            catch
            {
                lock (_gate)
                {
                    _providers[id] = previous;
                }
                throw;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task RemoveProvider(string id)
    {
        await _operationGate.WaitAsync();
        try
        {
            await EnsureLoadedAsync();
            AssetProviderConfigData? previous;
            lock (_gate)
            {
                if (!_providers.TryGetValue(id, out var existing))
                {
                    return;
                }

                if (existing.IsDefault)
                {
                    throw new InvalidOperationException("Default providers cannot be removed.");
                }

                if (existing.IsActive && !HasOtherActiveProvider(existing.AssetType, id))
                {
                    throw new InvalidOperationException(
                        "Cannot remove the only active provider for an asset type; at least one provider must remain active.");
                }

                previous = existing;
                _providers.Remove(id);
            }

            try
            {
                await PersistAsync();
            }
            catch
            {
                lock (_gate)
                {
                    _providers[id] = previous!;
                }
                throw;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task SetProviderActive(string id, bool isActive)
    {
        await _operationGate.WaitAsync();
        try
        {
            await EnsureLoadedAsync();
            AssetProviderConfigData previous;
            lock (_gate)
            {
                if (!_providers.TryGetValue(id, out var existing))
                {
                    throw new ArgumentException($"Provider '{id}' does not exist.", nameof(id));
                }

                if (existing.IsActive == isActive)
                {
                    return;
                }

                if (!isActive && !HasOtherActiveProvider(existing.AssetType, id))
                {
                    throw new InvalidOperationException(
                        "Cannot deactivate the only active provider for an asset type; at least one provider must remain active.");
                }

                previous = existing;
                _providers[id] = existing with { IsActive = isActive };
            }

            try
            {
                await PersistAsync();
            }
            catch
            {
                lock (_gate)
                {
                    _providers[id] = previous;
                }
                throw;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<IReadOnlyList<AssetProviderConfigData>> GetActiveProviders(AssetType assetType)
    {
        await EnsureLoadedAsync();
        lock (_gate)
        {
            return _providers.Values
                .Where(p => p.IsActive && p.AssetType == assetType)
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Id, StringComparer.Ordinal)
                .ToList();
        }
    }

    private bool HasOtherActiveProvider(AssetType assetType, string exceptId)
    {
        return _providers.Values.Any(p => p.Id != exceptId && p.IsActive && p.AssetType == assetType);
    }

    private async Task Load()
    {
        try
        {
            var cachedData = await _cachingService.TryGetCachedFile(CacheKey);
            lock (_gate)
            {
                if (cachedData is null)
                {
                    SeedDefaults();
                    return;
                }

                var json = Encoding.UTF8.GetString(cachedData);
                var state = JsonSerializer.Deserialize<AssetProvidersState>(json);
                var knownIds = new HashSet<string>(StringComparer.Ordinal);

                foreach (var provider in state?.Providers ?? [])
                {
                    if (string.IsNullOrWhiteSpace(provider.Id) || !knownIds.Add(provider.Id))
                    {
                        continue;
                    }

                    var normalized = NormalizeLegacyUrl(provider);
                    var isDefault = IsDefaultId(normalized.Id);
                    _providers[normalized.Id] = normalized with { IsDefault = isDefault };
                }

                // Ensure every built-in default still exists, even if absent from a stale cache.
                foreach (var defaultProvider in _defaultProviders)
                {
                    if (!_providers.ContainsKey(defaultProvider.Id))
                    {
                        _providers[defaultProvider.Id] = defaultProvider;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load asset provider configurations from cache");
            lock (_gate)
            {
                SeedDefaults();
            }
        }
    }

    private bool IsDefaultId(string id)
    {
        return _defaultProviders.Any(p => string.Equals(p.Id, id, StringComparison.Ordinal));
    }

    private static readonly string[] GitHubLegacySuffixes = ["/units/mechs", "/hexes/biomes"];

    /// <summary>
    /// Strips known asset-type sub-paths from a legacy GitHub provider's <c>UrlOrPath</c>
    /// so the factory does not append them a second time.
    /// </summary>
    private static AssetProviderConfigData NormalizeLegacyUrl(AssetProviderConfigData provider)
    {
        if (provider.ProviderType != ProviderType.GitHub)
        {
            return provider;
        }

        var url = provider.UrlOrPath;
        foreach (var suffix in GitHubLegacySuffixes)
        {
            if (url.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return provider with { UrlOrPath = url[..^suffix.Length] };
            }
        }

        return provider;
    }

    private void SeedDefaults()
    {
        _providers.Clear();
        foreach (var defaultProvider in _defaultProviders)
        {
            _providers[defaultProvider.Id] = defaultProvider with { IsDefault = true };
        }
    }

    private async Task PersistAsync()
    {
        try
        {
            AssetProvidersState state;
            lock (_gate)
            {
                state = new AssetProvidersState
                {
                    Providers = _providers.Values
                        .OrderBy(p => p.SortOrder)
                        .ThenBy(p => p.Id, StringComparer.Ordinal)
                        .ToList()
                };
            }

            var json = JsonSerializer.Serialize(state);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _cachingService.SaveToCache(CacheKey, bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save asset provider configurations to cache");
            throw;
        }
    }

    /// <summary>
    /// Serializable snapshot of all configured asset providers.
    /// </summary>
    private sealed class AssetProvidersState
    {
        public List<AssetProviderConfigData> Providers { get; init; } = [];
    }
}

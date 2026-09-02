using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.ResourceProviders;

namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Shared infrastructure for package-backed caching services (unit MMUX packages,
/// terrain MMTX packages). Owns the provider enumeration and the initialization
/// lifecycle: <see cref="SemaphoreSlim"/> + <see cref="PackageCacheState"/> snapshot
/// publication, lazy initialization, <see cref="SetProviders"/>, <see cref="ReloadProviders"/>
/// and <see cref="ClearCache"/>, parallel batched loading with progress reporting and the
/// duplicate/conflict policy.
/// Per-format parsing is delegated to format-specific package readers by the concrete
/// services via <see cref="LoadResource"/>.
/// </summary>
/// <typeparam name="TState">Type of the immutable-by-publication cache state snapshot</typeparam>
public abstract class PackageCacheCore<TState> : IProgressReporting
    where TState : PackageCacheState, new()
{
    /// <summary>
    /// The maximum number of resources to load in parallel
    /// </summary>
    private const int MaxDegreeOfParallelism = 10;

    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private IReadOnlyList<IResourceStreamProvider> _streamProviders;
    private volatile TState _state = new();
    private Func<Task<IReadOnlyList<IResourceStreamProvider>>>? _lazyProviders;

    /// <inheritdoc />
    public event EventHandler<ResourceLoadProgressEventArgs>? LoadProgress;

    /// <summary>
    /// Initializes the shared cache infrastructure
    /// </summary>
    /// <param name="streamProviders">Collection of stream providers to load resources from</param>
    protected PackageCacheCore(IEnumerable<IResourceStreamProvider> streamProviders)
    {
        ArgumentNullException.ThrowIfNull(streamProviders);
        _streamProviders = [.. streamProviders];
    }

    /// <summary>
    /// Domain-specific kind of the cached resource (e.g. "unit", "terrain biome"),
    /// used in log messages
    /// </summary>
    protected abstract string ResourceKind { get; }

    /// <summary>
    /// Logger used by the shared cache lifecycle
    /// </summary>
    protected abstract ILogger Logger { get; }

    /// <summary>
    /// The currently published cache state snapshot
    /// </summary>
    protected TState CurrentState => _state;

    /// <summary>
    /// Sets an asynchronous factory that resolves the stream providers on first access.
    /// Used by DI registrations that want to defer config resolution until the cache
    /// is actually used, avoiding sync-over-async in the DI factory.
    /// </summary>
    protected void SetLazyProviders(Func<Task<IReadOnlyList<IResourceStreamProvider>>> factory)
    {
        _lazyProviders = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Loads a single package resource from the given provider into the state.
    /// Format-specific parsing is delegated to package readers by the concrete service.
    /// </summary>
    protected abstract Task LoadResource(
        IResourceStreamProvider provider,
        string resourceId,
        Stream stream,
        TState state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the cache is initialized by loading resources from all available sources
    /// </summary>
    protected async Task<TState> EnsureInitialized()
    {
        var state = _state;
        if (state.IsInitialized) return state;

        await _initializationLock.WaitAsync();
        try
        {
            state = _state;
            if (state.IsInitialized) return state; // double-check after acquiring a lock
            await ResolveLazyProvidersIfNeeded();
            await LoadFromStreamProviders(state);
            state.IsInitialized = true;
            return state;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <summary>
    /// Loads resources from all configured stream providers in parallel batches,
    /// reporting progress as each task actually completes
    /// </summary>
    private async Task LoadFromStreamProviders(TState state)
    {
        // Enumerate all providers up front so the total is finalized before loading begins.
        // This keeps TotalCount stable and ensures reported progress cannot decrease between providers.
        // Each provider keeps its positional index in the configured list (even when the same
        // provider instance appears more than once) so repeated providers are treated as distinct
        // precedence positions rather than being merged together.
        var resources = new List<(int ProviderIndex, IResourceStreamProvider Provider, string ResourceId)>();
        var providerIndex = 0;
        foreach (var provider in _streamProviders)
        {
            try
            {
                var resourceIds = await provider.GetAvailableResourceIds();
                resources.AddRange(resourceIds.Select(resourceId => (providerIndex, provider, resourceId)));
            }
            catch (Exception ex)
            {
                // Log error but continue with other providers
                Logger.LogError(ex, "Error enumerating {ResourceKind} resources from provider {ProviderType}",
                    ResourceKind, provider.GetType().Name);
            }
            providerIndex++;
        }

        var totalResources = resources.Count;
        var processedResources = 0;
        RaiseLoadProgress(processedResources, totalResources);

        // Process providers sequentially in their configured precedence order so a provider lower
        // in the list always loads after (and therefore overwrites) the providers above it,
        // independent of parallel completion order. Grouping by the positional index keeps repeated
        // providers as separate positions, so the final occurrence of a provider remains
        // authoritative. Resources belonging to the same provider position may still load in
        // parallel, while reporting progress as each task actually completes.
        foreach (var providerGroup in resources.GroupBy(r => r.ProviderIndex))
        {
            foreach (var batch in providerGroup.Chunk(MaxDegreeOfParallelism))
            {
                var batchTasks = batch
                    .Select(r => LoadResourceSafe(r.Provider, r.ResourceId, state))
                    .ToList();

                while (batchTasks.Count > 0)
                {
                    var completedTask = await Task.WhenAny(batchTasks);
                    batchTasks.Remove(completedTask);
                    processedResources++;
                    RaiseLoadProgress(processedResources, totalResources);
                }
            }
        }
    }

    private async Task LoadResourceSafe(
        IResourceStreamProvider provider,
        string resourceId,
        TState state,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = await provider.GetResourceStream(resourceId);
            if (stream != null)
            {
                await LoadResource(provider, resourceId, stream, state, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Log error but continue processing other resources
            Logger.LogError(ex, "Error loading {ResourceKind} '{ResourceId}' from provider {ProviderType}",
                ResourceKind, resourceId, provider.GetType().Name);
        }
    }

    /// <summary>
    /// Shared duplicate/conflict policy: the resource loaded from the provider lower in the
    /// provider list overwrites the one loaded earlier, and the conflict is logged.
    /// </summary>
    /// <returns>True when the key was newly added, false when an existing value was overwritten</returns>
    protected bool TryCache<TKey, TValue>(ConcurrentDictionary<TKey, TValue> cache, TKey key, TValue value)
        where TKey : notnull
    {
        if (cache.TryAdd(key, value)) return true;

        Logger.LogWarning("Duplicate {ResourceKind} '{Key}' found; overwriting with the value from a lower provider",
            ResourceKind, key);
        cache[key] = value;
        return false;
    }

    /// <summary>
    /// Clears all cached data (useful for testing or reloading)
    /// </summary>
    public async Task ClearCache()
    {
        await _initializationLock.WaitAsync();
        try
        {
            ClearCacheLocked();
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <summary>
    /// Replaces the provider set and forces a lazy re-initialization on next access.
    /// </summary>
    public async Task SetProviders(IEnumerable<IResourceStreamProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        await _initializationLock.WaitAsync();
        try
        {
            _lazyProviders = null;
            _streamProviders = providers.ToList();
            ClearCacheLocked();
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <summary>
    /// Clears all cached data and re-runs initialization from the current provider set.
    /// </summary>
    public async Task ReloadProviders()
    {
        await _initializationLock.WaitAsync();
        try
        {
            await ResolveLazyProvidersIfNeeded();
            // Build the replacement state completely before publishing it, so readers
            // keep seeing the previous complete cache until the swap.
            var freshState = new TState();
            await LoadFromStreamProviders(freshState);
            freshState.IsInitialized = true;
            _state = freshState;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <summary>
    /// Replaces the cached state with an empty, uninitialized one. Readers already
    /// holding the previous state continue to see complete data; new readers wait
    /// for re-initialization. Must be called while holding the initialization lock.
    /// </summary>
    private void ClearCacheLocked()
    {
        _state = new TState();
    }

    /// <summary>
    /// If a lazy provider factory was set, resolves it and replaces the current
    /// provider list. The factory is consumed on first use and cleared afterward.
    /// Must be called while holding the initialization lock.
    /// </summary>
    private async Task ResolveLazyProvidersIfNeeded()
    {
        if (_lazyProviders is not { } factory) return;
        _lazyProviders = null;
        _streamProviders = await factory();
    }

    /// <summary>
    /// Raises the load progress event
    /// </summary>
    protected void RaiseLoadProgress(int loadedCount, int totalCount)
    {
        LoadProgress?.Invoke(this, new ResourceLoadProgressEventArgs(loadedCount, totalCount));
    }
}
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Serialization.Converters;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Assets.ResourceProviders;

namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Service for caching unit data and images loaded from various sources including MMUX packages
/// </summary>
public class UnitCachingService : IUnitCachingService
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private IReadOnlyList<IResourceStreamProvider> _streamProviders;
    private readonly ILogger<UnitCachingService> _logger;

    /// <summary>
    /// Snapshot of all cached data. A new instance is built completely and then
    /// published via a single volatile write to <see cref="_state"/>, so readers
    /// either observe the previous complete cache or the new complete cache,
    /// never a cleared or partially rebuilt state.
    /// </summary>
    private sealed class CacheState
    {
        public readonly ConcurrentDictionary<string, UnitData> UnitDataCache = new();
        public readonly ConcurrentDictionary<string, byte[]> ImageCache = new();
        public volatile bool IsInitialized;
    }

    private volatile CacheState _state = new();

    public event EventHandler<ResourceLoadProgressEventArgs>? LoadProgress;
    
    /// <summary>
    /// The maximum number of units to load in parallel
    /// </summary>
    private const int MaxDegreeOfParallelism = 10;
    
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new EnumConverter<MakaMekComponent>(),
            new EnumConverter<PartLocation>(),
            new EnumConverter<MovementType>(),
            new EnumConverter<UnitStatus>(),
            new EnumConverter<WeightClass>()
        }
    };

    /// <summary>
    /// Initializes a new instance of UnitCachingService
    /// </summary>
    /// <param name="streamProviders">Collection of stream providers to load units from</param>
    /// <param name="loggerFactory">Logger factory for logging</param>
    public UnitCachingService(IEnumerable<IResourceStreamProvider> streamProviders, ILoggerFactory loggerFactory)
    {
        _streamProviders = [.. streamProviders];
        _logger = loggerFactory.CreateLogger<UnitCachingService>();
    }
    
    /// <summary>
    /// Gets unit data by model name
    /// </summary>
    /// <param name="model">The unit model identifier</param>
    /// <returns>Unit data if found, null otherwise</returns>
    public async Task<UnitData?> GetUnitData(string model)
    {
        var state = await EnsureInitialized();
        return state.UnitDataCache.TryGetValue(model, out var unitData) ? unitData : null;
    }

    /// <summary>
    /// Gets unit image by model name
    /// </summary>
    /// <param name="model">The unit model identifier</param>
    /// <returns>Image bytes if found, null otherwise</returns>
    public async Task<byte[]?> GetUnitImage(string model)
    {
        var state = await EnsureInitialized();
        return state.ImageCache.GetValueOrDefault(model);
    }

    /// <summary>
    /// Gets all available unit models
    /// </summary>
    /// <returns>Collection of unit model identifiers</returns>
    public async Task<IEnumerable<string>> GetAvailableModels()
    {
        var state = await EnsureInitialized();
        return state.UnitDataCache.Keys;
    }

    /// <summary>
    /// Gets all cached unit data
    /// </summary>
    /// <returns>Collection of all unit data</returns>
    public async Task<IEnumerable<UnitData>> GetAllUnits()
    {
        var state = await EnsureInitialized();
        return state.UnitDataCache.Values;
    }

    /// <summary>
    /// Ensures the cache is initialized by loading units from all available sources
    /// </summary>
    private async Task<CacheState> EnsureInitialized()
    {
        var state = _state;
        if (state.IsInitialized) return state;

        await _initializationLock.WaitAsync();
        try
        {
            state = _state;
            if (state.IsInitialized) return state; // double-check after acquiring a lock
            await LoadUnitsFromStreamProviders(state);
            state.IsInitialized = true;
            return state;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <summary>
    /// Loads units from all configured stream providers
    /// </summary>
    private async Task LoadUnitsFromStreamProviders(CacheState state)
    {
        // Enumerate all providers up front so the total is finalized before loading begins.
        // This keeps TotalCount stable and ensures reported progress cannot decrease between providers.
        var units = new List<(IResourceStreamProvider Provider, string UnitId)>();
        foreach (var provider in _streamProviders)
        {
            try
            {
                var unitIds = await provider.GetAvailableResourceIds();
                foreach (var unitId in unitIds)
                {
                    units.Add((provider, unitId));
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with other providers
                _logger.LogError(ex, "Error loading units from provider {ProviderType}", provider.GetType().Name);
            }
        }

        var totalUnits = units.Count;
        var processedUnits = 0;
        RaiseLoadProgress(processedUnits, totalUnits);

        // Process units in parallel batches, reporting progress as each task actually completes
        var batches = units.Chunk(MaxDegreeOfParallelism);
        foreach (var batch in batches)
        {
            var batchTasks = batch
                .Select(u => ProcessUnitAsync(u.Provider, u.UnitId, state))
                .ToList();

            while (batchTasks.Count > 0)
            {
                var completedTask = await Task.WhenAny(batchTasks);
                batchTasks.Remove(completedTask);
                processedUnits++;
                RaiseLoadProgress(processedUnits, totalUnits);
            }
        }
    }

    private void RaiseLoadProgress(int loadedCount, int totalCount)
    {
        LoadProgress?.Invoke(this, new ResourceLoadProgressEventArgs(loadedCount, totalCount));
    }
    
    private async Task ProcessUnitAsync(IResourceStreamProvider provider, string unitId, CacheState state)
    {
        try
        {
            await using var stream = await provider.GetResourceStream(unitId);
            if (stream != null)
            {
                await LoadUnitFromMmuxStreamAsync(stream, state);
            }
        }
        catch (Exception ex)
        {
            // Log error but continue processing other units
            _logger.LogError(ex, "Error loading unit '{UnitId}' from provider {ProviderType}", unitId, provider.GetType().Name);
        }
    }

    /// <summary>
    /// Loads a unit from an MMUX package stream asynchronously
    /// </summary>
    /// <param name="mmuxStream">Stream containing the MMUX package data</param>
    /// <returns>Task representing the async operation</returns>
    private async Task LoadUnitFromMmuxStreamAsync(Stream mmuxStream, CacheState state)
    {
        await using var archive = new ZipArchive(mmuxStream, ZipArchiveMode.Read);

        // Find and load unit.json
        var unitJsonEntry = archive.GetEntry("unit.json");
        if (unitJsonEntry == null)
        {
            throw new InvalidOperationException("MMUX package missing unit.json");
        }

        UnitData unitData;
        await using (var unitJsonStream = await unitJsonEntry.OpenAsync())
        using (var reader = new StreamReader(unitJsonStream))
        {
            var jsonContent = await reader.ReadToEndAsync();
            unitData = JsonSerializer.Deserialize<UnitData>(jsonContent, _jsonOptions);
            if (string.IsNullOrEmpty(unitData.Model) )
            {
                throw new InvalidOperationException("Failed to deserialize unit.json");
            }
        }

        // Find and load unit.png
        var unitImageEntry = archive.GetEntry("unit.png");
        if (unitImageEntry == null)
        {
            throw new InvalidOperationException("MMUX package missing unit.png");
        }

        byte[] imageBytes;
        await using (var imageStream = await unitImageEntry.OpenAsync())
        using (var memoryStream = new MemoryStream())
        {
            await imageStream.CopyToAsync(memoryStream);
            imageBytes = memoryStream.ToArray();
        }

        // Cache both unit data and image using model name as a key
        state.UnitDataCache.TryAdd(unitData.Model, unitData);
        state.ImageCache.TryAdd(unitData.Model, imageBytes);
    }

    /// <summary>
    /// Clears all cached data (useful for testing or reloading)
    /// </summary>
    public void ClearCache()
    {
        _initializationLock.Wait();
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
    public void SetProviders(IEnumerable<IResourceStreamProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _initializationLock.Wait();
        try
        {
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
            // Build the replacement state completely before publishing it, so readers
            // keep seeing the previous complete cache until the swap.
            var freshState = new CacheState();
            await LoadUnitsFromStreamProviders(freshState);
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
    /// for re-initialization. Must be called while holding <see cref="_initializationLock"/>.
    /// </summary>
    private void ClearCacheLocked()
    {
        _state = new CacheState();
    }
}

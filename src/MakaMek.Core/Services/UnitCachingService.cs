using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using Sanet.MakaMek.Core.Data.Serialization.Converters;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Services;

/// <summary>
/// Service for caching unit data and images loaded from various sources including MMUX packages
/// </summary>
public class UnitCachingService
{
    private readonly ConcurrentDictionary<string, UnitData> _unitDataCache = new();
    private readonly ConcurrentDictionary<string, byte[]> _imageCache = new();
    private readonly IEnumerable<IResourceStreamProvider> _streamProviders;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters =
        {
            new EnumConverter<MakaMekComponent>(),
            new EnumConverter<PartLocation>(),
            new EnumConverter<MovementType>(),
            new EnumConverter<UnitStatus>(),
            new EnumConverter<WeightClass>()
        }
    };
    private bool _isInitialized;
    private readonly Lock _initLock = new();

    /// <summary>
    /// Initializes a new instance of UnitCachingService
    /// </summary>
    /// <param name="streamProviders">Collection of stream providers to load units from</param>
    public UnitCachingService(IEnumerable<IResourceStreamProvider> streamProviders)
    {
        _streamProviders = streamProviders;
    }
    
    /// <summary>
    /// Gets unit data by model name
    /// </summary>
    /// <param name="model">The unit model identifier</param>
    /// <returns>Unit data if found, null otherwise</returns>
    public async Task<UnitData?> GetUnitData(string model)
    {
        await EnsureInitialized();
        return _unitDataCache.TryGetValue(model, out var unitData) ? unitData : null;
    }

    /// <summary>
    /// Gets unit image by model name
    /// </summary>
    /// <param name="model">The unit model identifier</param>
    /// <returns>Image bytes if found, null otherwise</returns>
    public async Task<byte[]?> GetUnitImage(string model)
    {
        await EnsureInitialized();
        return _imageCache.GetValueOrDefault(model);
    }

    /// <summary>
    /// Gets all available unit models
    /// </summary>
    /// <returns>Collection of unit model identifiers</returns>
    public async Task<IEnumerable<string>> GetAvailableModels()
    {
        await EnsureInitialized();
        return _unitDataCache.Keys;
    }

    /// <summary>
    /// Gets all cached unit data
    /// </summary>
    /// <returns>Collection of all unit data</returns>
    public async Task<IEnumerable<UnitData>> GetAllUnits()
    {
        await EnsureInitialized();
        return _unitDataCache.Values;
    }

    /// <summary>
    /// Ensures the cache is initialized by loading units from all available sources
    /// </summary>
    private async Task EnsureInitialized()
    {
        if (_isInitialized) return;

        if (_isInitialized) return;

        await LoadUnitsFromStreamProviders();
        _isInitialized = true;
    }

    /// <summary>
    /// Loads units from all configured stream providers
    /// </summary>
    private async Task LoadUnitsFromStreamProviders()
    {
        foreach (var provider in _streamProviders)
        {
            try
            {
                var unitIds = provider.GetAvailableResourceIds();

                foreach (var unitId in unitIds)
                {
                    try
                    {
                        await using var stream =await provider.GetResourceStream(unitId);
                        if (stream != null)
                        {
                            LoadUnitFromMmuxStreamAsync(stream).Wait();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue processing other units
                        Console.WriteLine($"Error loading unit '{unitId}' from provider: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with other providers
                Console.WriteLine($"Error loading units from provider {provider.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Loads a unit from an MMUX package stream asynchronously
    /// </summary>
    /// <param name="mmuxStream">Stream containing the MMUX package data</param>
    /// <returns>Task representing the async operation</returns>
    private async Task LoadUnitFromMmuxStreamAsync(Stream mmuxStream)
    {
        using var archive = new ZipArchive(mmuxStream, ZipArchiveMode.Read);

        // Find and load unit.json
        var unitJsonEntry = archive.GetEntry("unit.json");
        if (unitJsonEntry == null)
        {
            throw new InvalidOperationException("MMUX package missing unit.json");
        }

        UnitData unitData;
        await using (var unitJsonStream = unitJsonEntry.Open())
        using (var reader = new StreamReader(unitJsonStream))
        {
            var jsonContent = await reader.ReadToEndAsync();
            unitData = JsonSerializer.Deserialize<UnitData>(jsonContent, _jsonOptions);
        }

        // Find and load unit.png
        var unitImageEntry = archive.GetEntry("unit.png");
        if (unitImageEntry == null)
        {
            throw new InvalidOperationException("MMUX package missing unit.png");
        }

        byte[] imageBytes;
        await using (var imageStream = unitImageEntry.Open())
        using (var memoryStream = new MemoryStream())
        {
            await imageStream.CopyToAsync(memoryStream);
            imageBytes = memoryStream.ToArray();
        }

        // Cache both unit data and image using model name as a key
        _unitDataCache.TryAdd(unitData.Model, unitData);
        _imageCache.TryAdd(unitData.Model, imageBytes);
    }

    /// <summary>
    /// Clears all cached data (useful for testing or reloading)
    /// </summary>
    public void ClearCache()
    {
        lock (_initLock)
        {
            _unitDataCache.Clear();
            _imageCache.Clear();
            _isInitialized = false;
        }
    }
}

using System.Collections.Concurrent;
using System.IO.Compression;
using System.Reflection;
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
    /// Gets unit data by model name
    /// </summary>
    /// <param name="model">The unit model identifier</param>
    /// <returns>Unit data if found, null otherwise</returns>
    public UnitData? GetUnitData(string model)
    {
        EnsureInitialized();
        return _unitDataCache.TryGetValue(model, out var unitData) ? unitData : null;
    }

    /// <summary>
    /// Gets unit image by model name
    /// </summary>
    /// <param name="model">The unit model identifier</param>
    /// <returns>Image bytes if found, null otherwise</returns>
    public byte[]? GetUnitImage(string model)
    {
        EnsureInitialized();
        return _imageCache.GetValueOrDefault(model);
    }

    /// <summary>
    /// Gets all available unit models
    /// </summary>
    /// <returns>Collection of unit model identifiers</returns>
    public IEnumerable<string> GetAvailableModels()
    {
        EnsureInitialized();
        return _unitDataCache.Keys;
    }

    /// <summary>
    /// Gets all cached unit data
    /// </summary>
    /// <returns>Collection of all unit data</returns>
    public IEnumerable<UnitData> GetAllUnits()
    {
        EnsureInitialized();
        return _unitDataCache.Values;
    }

    /// <summary>
    /// Ensures the cache is initialized by loading units from all available sources
    /// </summary>
    private void EnsureInitialized()
    {
        if (_isInitialized) return;

        lock (_initLock)
        {
            if (_isInitialized) return;

            LoadUnitsFromEmbeddedResources();
            _isInitialized = true;
        }
    }

    /// <summary>
    /// Loads units from embedded MMUX packages in the assembly
    /// </summary>
    private void LoadUnitsFromEmbeddedResources()
    {
        // Look for assemblies that might contain MMUX resources
        var assemblies = new[]
        {
            Assembly.GetEntryAssembly(),
            Assembly.GetExecutingAssembly(),
            Assembly.GetCallingAssembly()
        }.Where(a => a != null).Distinct();

        // Also look for assemblies by name that might contain MMUX files
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
        var avaloniaAssemblies = loadedAssemblies.Where(a =>
            a.GetName().Name?.Contains("MakaMek.Avalonia", StringComparison.OrdinalIgnoreCase) == true);

        assemblies = assemblies.Concat(avaloniaAssemblies).Distinct();

        foreach (var assembly in assemblies)
        {
            var resources = assembly!.GetManifestResourceNames();

            foreach (var resourceName in resources)
            {
                if (!resourceName.EndsWith(".mmux", StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream == null) continue;

                    LoadUnitFromMmuxStream(stream);
                }
                catch (Exception ex)
                {
                    // Log error but continue processing other packages
                    Console.WriteLine($"Error loading MMUX package '{resourceName}': {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Loads a unit from an MMUX package stream
    /// </summary>
    /// <param name="mmuxStream">Stream containing the MMUX package data</param>
    private void LoadUnitFromMmuxStream(Stream mmuxStream)
    {
        using var archive = new ZipArchive(mmuxStream, ZipArchiveMode.Read);

        // Find and load unit.json
        var unitJsonEntry = archive.GetEntry("unit.json");
        if (unitJsonEntry == null)
        {
            throw new InvalidOperationException("MMUX package missing unit.json");
        }

        UnitData unitData;
        using (var unitJsonStream = unitJsonEntry.Open())
        using (var reader = new StreamReader(unitJsonStream))
        {
            var jsonContent = reader.ReadToEnd();
            unitData = JsonSerializer.Deserialize<UnitData>(jsonContent, _jsonOptions);
        }

        // Find and load unit.png
        var unitImageEntry = archive.GetEntry("unit.png");
        if (unitImageEntry == null)
        {
            throw new InvalidOperationException("MMUX package missing unit.png");
        }

        byte[] imageBytes;
        using (var imageStream = unitImageEntry.Open())
        using (var memoryStream = new MemoryStream())
        {
            imageStream.CopyTo(memoryStream);
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

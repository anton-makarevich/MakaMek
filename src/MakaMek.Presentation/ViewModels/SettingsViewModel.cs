using System.Windows.Input;
using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Localization;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

public class SettingsViewModel : BaseViewModel
{
    private readonly IFileCachingService _fileCachingService;
    private readonly IUnitCachingService _unitCachingService;
    private readonly ITerrainAssetService _terrainAssetService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<SettingsViewModel> _logger;

    public SettingsViewModel(
        IFileCachingService fileCachingService,
        IUnitCachingService unitCachingService,
        ITerrainAssetService terrainAssetService,
        ILocalizationService localizationService,
        ILogger<SettingsViewModel> logger)
    {
        _fileCachingService = fileCachingService;
        _unitCachingService = unitCachingService;
        _terrainAssetService = terrainAssetService;
        _localizationService = localizationService;
        _logger = logger;

        ClearCacheCommand = new AsyncCommand(ClearCacheAsync);

        // Initialize cache status
        InitializeCacheStatusAsync().SafeFireAndForget(ex => 
        {
            _logger.LogError(ex, "Failed to initialize cache status");
        });
    }

    public ICommand ClearCacheCommand { get; }

    public string CacheStatus
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    // Localized string properties
    public string DataSectionTitle => _localizationService.GetString("Settings_Data_SectionTitle");
    public string ClearCacheButton => _localizationService.GetString("Settings_Data_ClearCache");
    public string ClearCacheDescription => _localizationService.GetString("Settings_Data_ClearCacheDescription");

    private async Task InitializeCacheStatusAsync()
    {
        try
        {
            var models = await _unitCachingService.GetAvailableModels();
            var biomes = await _terrainAssetService.GetLoadedBiomes();
            var unitCount = models.Count();
            var biomeCount = biomes.Count();

            CacheStatus = string.Format(
                _localizationService.GetString("Settings_Data_CacheStatus"),
                unitCount,
                biomeCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize cache status");
            CacheStatus = _localizationService.GetString("Settings_Data_CacheStatus");
        }
    }

    private async Task ClearCacheAsync()
    {
        IsBusy = true;
        try
        {
            CacheStatus = _localizationService.GetString("Settings_Data_Clearing");

            // Clear all caches
            await _fileCachingService.ClearCache();
            _unitCachingService.ClearCache();
            _terrainAssetService.ClearCache();

            CacheStatus = _localizationService.GetString("Settings_Data_Cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear cache");
            CacheStatus = _localizationService.GetString("Settings_Data_CacheStatus");
        }
        finally
        {
            IsBusy = false;
        }
    }
}

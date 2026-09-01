using AsyncAwaitBestPractices;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Services;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

public class AssetLoadingViewModel : BaseViewModel
{
    private readonly IUnitCachingService _unitCachingService;
    private readonly ITerrainAssetService _terrainAssetService;
    private readonly ILocalizationService _localizationService;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILogger _logger;
    private string _unitLoadingStatus = string.Empty;
    private string _biomeLoadingStatus = string.Empty;
    private int _unitLoadedCount;
    private int _unitTotalCount;
    private int _biomeLoadedCount;
    private int _biomeTotalCount;
    private long _loadGeneration;
    private Task? _activeLoadTask;

    public AssetLoadingViewModel(
        IUnitCachingService unitCachingService,
        ITerrainAssetService terrainAssetService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        ILogger logger)
    {
        _unitCachingService = unitCachingService ?? throw new ArgumentNullException(nameof(unitCachingService));
        _terrainAssetService = terrainAssetService ?? throw new ArgumentNullException(nameof(terrainAssetService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsLoading
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string LoadingText
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public double LoadingProgress
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool HasError { get; private set; }

    public void InitializeAsync(int messageDelay = 1000)
    {
        var generation = ++_loadGeneration;

        IsLoading = true;
        HasError = false;
        LoadingProgress = 0;
        _unitLoadedCount = 0;
        _unitTotalCount = 0;
        _biomeLoadedCount = 0;
        _biomeTotalCount = 0;
        _unitLoadingStatus = _localizationService.GetString("MainMenu_Loading_Units");
        _biomeLoadingStatus = _localizationService.GetString("MainMenu_Loading_Biomes");
        UpdateLoadingText();

        _unitCachingService.LoadProgress -= OnUnitLoadProgress;
        _terrainAssetService.LoadProgress -= OnBiomeLoadProgress;
        _unitCachingService.LoadProgress += OnUnitLoadProgress;
        _terrainAssetService.LoadProgress += OnBiomeLoadProgress;

        _activeLoadTask = LoadAsync(messageDelay, generation);
        _activeLoadTask
            .SafeFireAndForget(ex => _logger.LogError(ex, "Error preloading content"));
    }

    public async Task ReloadAsync(int messageDelay = 1000)
    {
        await _unitCachingService.ClearCache();
        await _terrainAssetService.ClearCache();
        InitializeAsync(messageDelay);
        if (_activeLoadTask != null)
            await _activeLoadTask;
    }

    private void UpdateLoadingText()
    {
        LoadingText = $"{_unitLoadingStatus}\n{_biomeLoadingStatus}";
    }

    private void UpdateLoadingProgress()
    {
        var totalCount = _unitTotalCount + _biomeTotalCount;
        LoadingProgress = totalCount > 0
            ? (double)(_unitLoadedCount + _biomeLoadedCount) / totalCount
            : 0;
    }

    private void OnUnitLoadProgress(object? sender, ResourceLoadProgressEventArgs e)
    {
        var generation = _loadGeneration;
        _dispatcherService.RunOnUIThread(() =>
        {
            if (generation != _loadGeneration) return;
            _unitLoadingStatus = string.Format(
                _localizationService.GetString("MainMenu_Loading_UnitsProgress"),
                e.LoadedCount, e.TotalCount);
            _unitLoadedCount = e.LoadedCount;
            _unitTotalCount = e.TotalCount;
            UpdateLoadingText();
            UpdateLoadingProgress();
        });
    }

    private void OnBiomeLoadProgress(object? sender, ResourceLoadProgressEventArgs e)
    {
        var generation = _loadGeneration;
        _dispatcherService.RunOnUIThread(() =>
        {
            if (generation != _loadGeneration) return;
            _biomeLoadingStatus = string.Format(
                _localizationService.GetString("MainMenu_Loading_BiomesProgress"),
                e.LoadedCount, e.TotalCount);
            _biomeLoadedCount = e.LoadedCount;
            _biomeTotalCount = e.TotalCount;
            UpdateLoadingText();
            UpdateLoadingProgress();
        });
    }

    private async Task LoadAsync(int messageDelay, long generation)
    {
        await Task.WhenAll(PreloadUnits(generation), PreloadBiomes(generation));
        if (generation != _loadGeneration) return;
        if (!HasError)
        {
            await Task.Delay(messageDelay);
            if (generation != _loadGeneration) return;
            IsLoading = false;
        }
    }

    private async Task PreloadUnits(long generation)
    {
        try
        {
            var models = await _unitCachingService.GetAvailableModels();
            var modelCount = models.Count();

            if (generation != _loadGeneration) return;
            _unitLoadingStatus = modelCount == 0
                ? _localizationService.GetString("MainMenu_Loading_NoUnitsFound")
                : string.Format(_localizationService.GetString("MainMenu_Loading_UnitsLoaded"), modelCount);

            if (modelCount == 0)
                throw new Exception(_localizationService.GetString("MainMenu_Loading_NoUnitsFound"));
        }
        catch (Exception ex)
        {
            if (generation != _loadGeneration) return;
            HasError = true;
            _unitLoadingStatus = string.Format(_localizationService.GetString("MainMenu_Loading_UnitsError"), ex.Message);
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                _unitLoadedCount = _unitTotalCount;
                UpdateLoadingProgress();
                UpdateLoadingText();
            }
        }
    }

    private async Task PreloadBiomes(long generation)
    {
        try
        {
            var biomes = await _terrainAssetService.GetLoadedBiomes();
            var biomeCount = biomes.Count();

            if (generation != _loadGeneration) return;
            _biomeLoadingStatus = biomeCount == 0
                ? _localizationService.GetString("MainMenu_Loading_NoBiomesFound")
                : string.Format(_localizationService.GetString("MainMenu_Loading_BiomesLoaded"), biomeCount);

            if (biomeCount == 0)
                throw new Exception(_localizationService.GetString("MainMenu_Loading_NoBiomesFound"));
        }
        catch (Exception ex)
        {
            if (generation != _loadGeneration) return;
            HasError = true;
            _biomeLoadingStatus = string.Format(_localizationService.GetString("MainMenu_Loading_BiomesError"), ex.Message);
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                _biomeLoadedCount = _biomeTotalCount;
                UpdateLoadingProgress();
                UpdateLoadingText();
            }
        }
    }
}

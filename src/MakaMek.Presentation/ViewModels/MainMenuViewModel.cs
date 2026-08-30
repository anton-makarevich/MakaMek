using System.Windows.Input;
using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Services;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

public class MainMenuViewModel : BaseViewModel
{
    private readonly IUnitCachingService _unitCachingService;
    private readonly ITerrainAssetService _terrainAssetService;
    private readonly ILocalizationService _localizationService;
    private readonly IDispatcherService _dispatcherService;
    private bool _hasError;
    private string _unitLoadingStatus;
    private string _biomeLoadingStatus;
    private int _unitLoadedCount;
    private int _unitTotalCount;
    private int _biomeLoadedCount;
    private int _biomeTotalCount;

    public MainMenuViewModel(IUnitCachingService unitCachingService,
        ITerrainAssetService terrainAssetService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        ILogger<MainMenuViewModel> logger,
        int messageDelay = 1000)
    {
        _unitCachingService = unitCachingService ?? throw new ArgumentNullException(nameof(unitCachingService));
        _terrainAssetService = terrainAssetService ?? throw new ArgumentNullException(nameof(terrainAssetService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
        var currentLogger = logger;

        // Get version from entry assembly
        var assembly = GetType().Assembly;
        Version = $"v{assembly.GetName().Version?.ToString()}";

        StartNewGameCommand = new AsyncCommand(NavigateToNewGame);
        JoinGameCommand = new AsyncCommand(NavigateToJoinGame);
        AboutCommand = new AsyncCommand(NavigateToAbout);
        SettingsCommand = new AsyncCommand(NavigateToSettings);

        // Start preloading units and biomes
        IsLoading = true;
        _unitLoadingStatus = _localizationService.GetString("MainMenu_Loading_Units");
        _biomeLoadingStatus = _localizationService.GetString("MainMenu_Loading_Biomes");
        UpdateLoadingText();

        _unitCachingService.LoadProgress += OnUnitLoadProgress;
        _terrainAssetService.LoadProgress += OnBiomeLoadProgress;

        InitializeAsync(messageDelay)
            .SafeFireAndForget(ex => currentLogger.LogError(ex, "Error preloading content"));
    }

    public ICommand StartNewGameCommand { get; }
    public ICommand JoinGameCommand { get; }
    public ICommand AboutCommand { get; }
    public ICommand SettingsCommand { get; }
    public string Version { get; }

    /// <summary>
    /// Gets a value indicating whether the application is currently loading unit data
    /// </summary>
    public bool IsLoading
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Gets the current loading status text
    /// </summary>
    public string LoadingText
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Gets the current loading progress as a value between 0 and 1
    /// </summary>
    public double LoadingProgress
    {
        get;
        private set => SetProperty(ref field, value);
    }

    private void UpdateLoadingText()
    {
        LoadingText = $"{_unitLoadingStatus}\n{_biomeLoadingStatus}";
    }

    /// <summary>
    /// Derives overall loading progress from both preload operations so one completing cannot
    /// report overall completion while the other is still active.
    /// </summary>
    private void UpdateLoadingProgress()
    {
        var totalCount = _unitTotalCount + _biomeTotalCount;
        LoadingProgress = totalCount > 0
            ? (double)(_unitLoadedCount + _biomeLoadedCount) / totalCount
            : 0;
    }

    private void OnUnitLoadProgress(object? sender, ResourceLoadProgressEventArgs e)
    {
        _dispatcherService.RunOnUIThread(() =>
        {
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
        _dispatcherService.RunOnUIThread(() =>
        {
            _biomeLoadingStatus = string.Format(
                _localizationService.GetString("MainMenu_Loading_BiomesProgress"),
                e.LoadedCount, e.TotalCount);
            _biomeLoadedCount = e.LoadedCount;
            _biomeTotalCount = e.TotalCount;
            UpdateLoadingText();
            UpdateLoadingProgress();
        });
    }

    private async Task InitializeAsync(int messageDelay)
    {
        await Task.WhenAll(PreloadUnits(), PreloadBiomes());
        if (!_hasError)
        {
            await Task.Delay(messageDelay);
            IsLoading = false;
        }
    }

    private async Task NavigateToViewModel<TViewModel>() where TViewModel : BaseViewModel
    {
        var viewModel = await NavigationService.GetNewViewModelAsync<TViewModel>();
        if (viewModel == null)
        {
            throw new InvalidOperationException($"{typeof(TViewModel).Name} is not registered");
        }
        await NavigationService.NavigateToViewModelAsync(viewModel);
    }

    private Task NavigateToNewGame() => NavigateToViewModel<StartNewGameViewModel>();

    private Task NavigateToJoinGame() => NavigateToViewModel<JoinGameViewModel>();

    private Task NavigateToAbout() => NavigateToViewModel<AboutViewModel>();

    private Task NavigateToSettings() => NavigateToViewModel<SettingsViewModel>();

    /// <summary>
    /// Preloads unit data from all configured providers
    /// </summary>
    private async Task PreloadUnits()
    {
        try
        {
            // Trigger initialization of the unit caching service
            var models = await _unitCachingService.GetAvailableModels();
            var modelCount = models.Count();

            _unitLoadingStatus = modelCount == 0
                ? _localizationService.GetString("MainMenu_Loading_NoUnitsFound")
                : string.Format(_localizationService.GetString("MainMenu_Loading_UnitsLoaded"), modelCount);

            if (modelCount == 0)
                throw new Exception(_localizationService.GetString("MainMenu_Loading_NoUnitsFound"));
        }
        catch (Exception ex)
        {
            _hasError = true;
            _unitLoadingStatus = string.Format(_localizationService.GetString("MainMenu_Loading_UnitsError"), ex.Message);
        }
        finally
        {
            // Mark the unit preload as complete so it cannot report overall completion on its own
            _unitLoadedCount = _unitTotalCount;
            UpdateLoadingProgress();
            UpdateLoadingText();
        }
    }

    /// <summary>
    /// Preloads biome data from all configured providers
    /// </summary>
    private async Task PreloadBiomes()
    {
        try
        {
            // Trigger initialization of the terrain caching service
            var biomes = await _terrainAssetService.GetLoadedBiomes();
            var biomeCount = biomes.Count();

            _biomeLoadingStatus = biomeCount == 0
                ? _localizationService.GetString("MainMenu_Loading_NoBiomesFound")
                : string.Format(_localizationService.GetString("MainMenu_Loading_BiomesLoaded"), biomeCount);

            if (biomeCount == 0)
                throw new Exception(_localizationService.GetString("MainMenu_Loading_NoBiomesFound"));
        }
        catch (Exception ex)
        {
            _hasError = true;
            _biomeLoadingStatus = string.Format(_localizationService.GetString("MainMenu_Loading_BiomesError"), ex.Message);
        }
        finally
        {
            // Mark the biome preload as complete so it cannot report overall completion on its own
            _biomeLoadedCount = _biomeTotalCount;
            UpdateLoadingProgress();
            UpdateLoadingText();
        }
    }
}

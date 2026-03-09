using System.Windows.Input;
using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

public class MainMenuViewModel : BaseViewModel
{
    private readonly IUnitCachingService _unitCachingService;
    private readonly ITerrainAssetService _terrainAssetService;
    private readonly ILocalizationService _localizationService;
    private bool _hasError;
    private string _unitLoadingStatus;
    private string _biomeLoadingStatus;

    public MainMenuViewModel(IUnitCachingService unitCachingService,
        ITerrainAssetService terrainAssetService,
        ILocalizationService localizationService,
        ILogger<MainMenuViewModel> logger,
        int messageDelay = 1000)
    {
        _unitCachingService = unitCachingService ?? throw new ArgumentNullException(nameof(unitCachingService));
        _terrainAssetService = terrainAssetService ?? throw new ArgumentNullException(nameof(terrainAssetService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        var logger1 = logger;

        // Get version from entry assembly
        var assembly = GetType().Assembly;
        Version = $"v{assembly.GetName().Version?.ToString()}";

        StartNewGameCommand = new AsyncCommand(NavigateToNewGame);
        JoinGameCommand = new AsyncCommand(NavigateToJoinGame);
        AboutCommand = new AsyncCommand(NavigateToAbout);

        // Start preloading units and biomes
        IsLoading = true;
        _unitLoadingStatus = _localizationService.GetString("MainMenu_Loading_Units");
        _biomeLoadingStatus = _localizationService.GetString("MainMenu_Loading_Biomes");
        UpdateLoadingText();

        Task.WhenAll(PreloadUnits(), PreloadBiomes())
            .ContinueWith(async _ => 
            {
                if (!_hasError)
                {
                    await Task.Delay(messageDelay);
                    IsLoading = false;
                }
            }, TaskScheduler.Default)
            .Unwrap()
            .SafeFireAndForget(ex => logger1.LogError(ex, "Error preloading content"));
    }

    public ICommand StartNewGameCommand { get; }
    public ICommand JoinGameCommand { get; }
    public ICommand AboutCommand { get; }
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

    private void UpdateLoadingText()
    {
        LoadingText = $"{_unitLoadingStatus}\n{_biomeLoadingStatus}";
    }

    private async Task NavigateToViewModel<TViewModel>() where TViewModel : BaseViewModel
    {
        var viewModel = NavigationService.GetNewViewModel<TViewModel>();
        if (viewModel == null)
        {
            throw new InvalidOperationException($"{typeof(TViewModel).Name} is not registered");
        }
        await NavigationService.NavigateToViewModelAsync(viewModel);
    }

    private Task NavigateToNewGame() => NavigateToViewModel<StartNewGameViewModel>();

    private Task NavigateToJoinGame() => NavigateToViewModel<JoinGameViewModel>();

    private Task NavigateToAbout() => NavigateToViewModel<AboutViewModel>();

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
            UpdateLoadingText();
        }
    }
}

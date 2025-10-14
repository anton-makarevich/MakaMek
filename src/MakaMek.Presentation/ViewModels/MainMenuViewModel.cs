using System.Reflection;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

public class MainMenuViewModel : BaseViewModel
{
    private readonly IUnitCachingService _unitCachingService;
    private readonly ILocalizationService _localizationService;
    private readonly int _messageDelay;
    private bool _isLoading;
    private string _loadingText = string.Empty;

    public MainMenuViewModel(IUnitCachingService unitCachingService, ILocalizationService localizationService, int messageDelay = 1000)
    {
        _unitCachingService = unitCachingService ?? throw new ArgumentNullException(nameof(unitCachingService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _messageDelay = messageDelay;

        // Get version from entry assembly
        var assembly = Assembly.GetEntryAssembly();
        Version = $"v{assembly?.GetName().Version?.ToString()}";

        StartNewGameCommand = new AsyncCommand(NavigateToNewGame);
        JoinGameCommand = new AsyncCommand(NavigateToJoinGame);

        // Start preloading units
        IsLoading = true;
        LoadingText = _localizationService.GetString("MainMenu_Loading_Content");
        _ = Task.Run(PreloadUnits);
    }

    public ICommand StartNewGameCommand { get; }
    public ICommand JoinGameCommand { get; }
    public string Version { get; }

    /// <summary>
    /// Gets a value indicating whether the application is currently loading unit data
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    /// Gets the current loading status text
    /// </summary>
    public string LoadingText
    {
        get => _loadingText;
        private set => SetProperty(ref _loadingText, value);
    }

    private async Task NavigateToNewGame()
    {
        var startNewGameViewModel = NavigationService.GetNewViewModel<StartNewGameViewModel>();
        if (startNewGameViewModel != null)
        {
            await NavigationService.NavigateToViewModelAsync(startNewGameViewModel);
        }
    }

    private async Task NavigateToJoinGame()
    {
        await NavigationService.NavigateToViewModelAsync<JoinGameViewModel>();
    }

    /// <summary>
    /// Preloads unit data from all configured providers
    /// </summary>
    private async Task PreloadUnits()
    {
        try
        {
            // Trigger initialization of the unit caching service
            // This will load units from all providers including the GitHub provider
            var models = await _unitCachingService.GetAvailableModels();
            var modelCount = models.Count();

            LoadingText = modelCount == 0
                ? _localizationService.GetString("MainMenu_Loading_NoItemsFound")
                : string.Format(_localizationService.GetString("MainMenu_Loading_ItemsLoaded"), modelCount);

            if (modelCount == 0)
                throw new Exception(_localizationService.GetString("MainMenu_Loading_NoItemsFound"));

            // Small delay to show the completion message
            await Task.Delay(_messageDelay);
            IsLoading = false;
        }
        catch (Exception ex)
        {
            LoadingText = string.Format(_localizationService.GetString("MainMenu_Loading_Error"), ex.Message);
        }
    }
}

using System.Reflection;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Services;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

public class MainMenuViewModel : BaseViewModel
{
    private readonly IUnitCachingService _unitCachingService;
    private readonly int _messageDelay;
    private bool _isLoading;
    private string _loadingText;

    public MainMenuViewModel(IUnitCachingService unitCachingService, int messageDelay = 1000)
    {
        _unitCachingService = unitCachingService;
        _messageDelay = messageDelay;

        // Get version from entry assembly
        var assembly = Assembly.GetEntryAssembly();
        Version = $"v{assembly?.GetName().Version?.ToString()}";

        StartNewGameCommand = new AsyncCommand(NavigateToNewGame);
        JoinGameCommand = new AsyncCommand(NavigateToJoinGame);

        // Start preloading units
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
        await NavigationService.NavigateToViewModelAsync<StartNewGameViewModel>();
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
        IsLoading = true;
        try
        {
            LoadingText = "Loading content...";

            // Trigger initialization of the unit caching service
            // This will load units from all providers including the GitHub provider
            var models = await _unitCachingService.GetAvailableModels();
            var modelCount = models.Count();

            LoadingText = modelCount == 0
                ? "No items found"
                : $"Loaded {modelCount} items";
            
            if (modelCount == 0)
                throw new Exception("No items found");

            // Small delay to show the completion message
            await Task.Delay(_messageDelay);
            IsLoading = false;
        }
        catch (Exception ex)
        {
            LoadingText = $"Error loading units: {ex.Message}";
        }
    }
}

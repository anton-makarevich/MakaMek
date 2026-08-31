using System.ComponentModel;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Services;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

public class MainMenuViewModel : BaseViewModel
{
    public MainMenuViewModel(IUnitCachingService unitCachingService,
        ITerrainAssetService terrainAssetService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        ILogger<MainMenuViewModel> logger,
        int messageDelay = 1000)
    {
        AssetLoading = new AssetLoadingViewModel(
            unitCachingService,
            terrainAssetService,
            localizationService,
            dispatcherService,
            logger);

        AssetLoading.PropertyChanged += OnAssetLoadingPropertyChanged;

        // Get version from entry assembly
        var assembly = GetType().Assembly;
        Version = $"v{assembly.GetName().Version?.ToString()}";

        StartNewGameCommand = new AsyncCommand(NavigateToNewGame);
        JoinGameCommand = new AsyncCommand(NavigateToJoinGame);
        AboutCommand = new AsyncCommand(NavigateToAbout);
        SettingsCommand = new AsyncCommand(NavigateToSettings);

        AssetLoading.InitializeAsync(messageDelay);
    }

    public AssetLoadingViewModel AssetLoading { get; }

    public ICommand StartNewGameCommand { get; }
    public ICommand JoinGameCommand { get; }
    public ICommand AboutCommand { get; }
    public ICommand SettingsCommand { get; }
    public string Version { get; }

    public bool IsLoading => AssetLoading.IsLoading;

    public string LoadingText => AssetLoading.LoadingText;

    public double LoadingProgress => AssetLoading.LoadingProgress;

    private void OnAssetLoadingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AssetLoadingViewModel.IsLoading))
            NotifyPropertyChanged(nameof(IsLoading));
        else if (e.PropertyName == nameof(AssetLoadingViewModel.LoadingText))
            NotifyPropertyChanged(nameof(LoadingText));
        else if (e.PropertyName == nameof(AssetLoadingViewModel.LoadingProgress))
            NotifyPropertyChanged(nameof(LoadingProgress));
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
}

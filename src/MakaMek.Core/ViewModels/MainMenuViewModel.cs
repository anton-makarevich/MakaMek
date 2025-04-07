using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MVVM.Core.ViewModels;
using System.Reflection;

namespace Sanet.MakaMek.Core.ViewModels;

public class MainMenuViewModel : BaseViewModel
{

    public MainMenuViewModel()
    {
        // Get version from entry assembly
        var assembly = Assembly.GetEntryAssembly();
        Version = $"v{assembly?.GetName().Version?.ToString()}"; 

        StartNewGameCommand = new AsyncCommand(NavigateToNewGame);
        // JoinGameCommand can be left empty or show a message for now
    }

    public ICommand StartNewGameCommand { get; }
    public ICommand JoinGameCommand { get; }
    public string Version { get; }

    private async Task NavigateToNewGame()
    {
        // Assuming StartNewGameViewModel is registered and NavigationService can resolve it
        await NavigationService.NavigateToViewModelAsync<StartNewGameViewModel>();
    }
}

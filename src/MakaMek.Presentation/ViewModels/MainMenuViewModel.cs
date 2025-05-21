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
        JoinGameCommand = new AsyncCommand(NavigateToJoinGame); 
    }

    public ICommand StartNewGameCommand { get; }
    public ICommand JoinGameCommand { get; }
    public string Version { get; }

    private async Task NavigateToNewGame()
    {
        await NavigationService.NavigateToViewModelAsync<StartNewGameViewModel>();
    }

    private async Task NavigateToJoinGame()
    {
        await NavigationService.NavigateToViewModelAsync<JoinGameViewModel>();
    }
}

using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MVVM.Core.Services;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Core.ViewModels;

public class MainMenuViewModel : BaseViewModel
{

    public MainMenuViewModel()
    {
        StartNewGameCommand = new AsyncCommand(NavigateToNewGame);
        // JoinGameCommand can be left empty or show a message for now
    }

    public ICommand StartNewGameCommand { get; }
    public ICommand JoinGameCommand { get; }

    private async Task NavigateToNewGame()
    {
        // Assuming StartNewGameViewModel is registered and NavigationService can resolve it
        await NavigationService.NavigateToViewModelAsync<StartNewGameViewModel>();
    }
}

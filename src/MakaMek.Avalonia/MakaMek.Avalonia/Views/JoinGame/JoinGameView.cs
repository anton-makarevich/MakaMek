using Sanet.MakaMek.Core.ViewModels;
using Sanet.MVVM.Views.Avalonia;

namespace Sanet.MakaMek.Avalonia.Views.JoinGame;

public abstract class JoinGameView : BaseView<JoinGameViewModel>
{
    // Called when the ViewModel is set (DataContext changed)
    protected override void OnViewModelSet()
    {
        base.OnViewModelSet();
        // Initialize the client-side logic (create ClientGame, subscribe to commands)
        ViewModel?.InitializeClient();
    }
}

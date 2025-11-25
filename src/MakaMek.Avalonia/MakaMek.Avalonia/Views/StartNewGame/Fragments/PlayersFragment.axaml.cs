using Avalonia.Controls;

namespace Sanet.MakaMek.Avalonia.Views.StartNewGame.Fragments
{
    public partial class PlayersFragment : UserControl
    {
        public PlayersFragment()
        {
            InitializeComponent();
#if !DEBUG
            AddBotButton.IsVisible = false;
#endif
        }
    }
}

using Avalonia.Controls;
using Avalonia.Input;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Avalonia.Views.StartNewGame.Fragments
{
    public partial class MapConfigFragment : UserControl
    {
        public MapConfigFragment()
        {
            InitializeComponent();
        }

        private void OnMapItemPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border { DataContext: MapPreviewItem item } 
                && DataContext is MapConfigViewModel viewModel)
            {
                viewModel.SelectMap(item);
            }
        }
    }
}

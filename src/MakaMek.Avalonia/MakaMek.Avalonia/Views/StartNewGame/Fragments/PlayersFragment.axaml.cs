using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Avalonia.Views.StartNewGame.Fragments
{
    public partial class PlayersFragment : UserControl
    {
        public PlayersFragment()
        {
            InitializeComponent();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (DataContext is NewGameViewModel viewModel)
            {
                viewModel.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(viewModel.IsTableVisible))
                    {
                        // Refresh the view
                        if (viewModel.IsTableVisible)
                        {
                            FlyoutBase.ShowAttachedFlyout(this);
                        } 
                    }
                }; 
            }
        }
    }
}

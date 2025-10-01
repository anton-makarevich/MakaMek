using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Avalonia.Controls;

public partial class AvailableUnitsTable : UserControl
{
    public AvailableUnitsTable()
    {
        InitializeComponent();
    }

    private void OnDataGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        // When a row is double-clicked, trigger the AddUnitCommand
        if (DataContext is PlayerViewModel playerViewModel && playerViewModel.CanAddUnit)
        {
            playerViewModel.AddUnitCommand.Execute(null);
        }
    }
}


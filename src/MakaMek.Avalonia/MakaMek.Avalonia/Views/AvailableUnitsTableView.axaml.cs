using Avalonia.Controls;
using Sanet.MVVM.Core.Views;

namespace Sanet.MakaMek.Avalonia.Views;

public partial class AvailableUnitsTableView : UserControl, IBaseView
{
    public AvailableUnitsTableView()
    {
        InitializeComponent();
    }

    public object? ViewModel
    {
        get => DataContext;
        set => DataContext = value;
    }
}


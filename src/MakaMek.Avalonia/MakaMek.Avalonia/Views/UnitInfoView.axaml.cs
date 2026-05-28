using Avalonia.Controls;
using Sanet.MVVM.Core.Views;

namespace Sanet.MakaMek.Avalonia.Views;

public partial class UnitInfoView : UserControl, IBaseView
{
    public UnitInfoView()
    {
        InitializeComponent();
    }

    public object? ViewModel
    {
        get => DataContext;
        set => DataContext = value;
    }
}

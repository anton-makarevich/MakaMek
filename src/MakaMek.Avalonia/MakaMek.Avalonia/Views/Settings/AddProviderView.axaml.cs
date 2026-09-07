using Avalonia.Controls;
using Sanet.MVVM.Core.Views;

namespace Sanet.MakaMek.Avalonia.Views.Settings;

public partial class AddProviderView : UserControl, IBaseView
{
    public AddProviderView()
    {
        InitializeComponent();
    }

    public object? ViewModel
    {
        get => DataContext;
        set => DataContext = value;
    }
}

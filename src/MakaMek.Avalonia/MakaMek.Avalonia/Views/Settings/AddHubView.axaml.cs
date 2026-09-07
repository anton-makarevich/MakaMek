using Avalonia.Controls;
using Sanet.MVVM.Core.Views;

namespace Sanet.MakaMek.Avalonia.Views.Settings;

public partial class AddHubView : UserControl, IBaseView
{
    public AddHubView()
    {
        InitializeComponent();
    }

    public object? ViewModel
    {
        get => DataContext;
        set => DataContext = value;
    }
}

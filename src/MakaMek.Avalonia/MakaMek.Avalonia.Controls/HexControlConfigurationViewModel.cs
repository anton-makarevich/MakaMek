using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Avalonia.Controls;

public class HexControlConfigurationViewModel : BindableBase
{
    public bool ShowLabels
    {
        get;
        set => SetProperty(ref field, value);
    } = true;

    public bool ShowOutline
    {
        get;
        set => SetProperty(ref field, value);
    } = true;

    public HexControlConfiguration ToConfiguration()
    {
        return new HexControlConfiguration(ShowLabels, ShowOutline);
    }
}

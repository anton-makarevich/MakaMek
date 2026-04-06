using Sanet.MakaMek.Map.Data;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

public class HexRenderConfigurationViewModel : BindableBase
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

    public bool ShowHighlightLabels
    {
        get;
        set => SetProperty(ref field, value);
    }

    public HexRenderConfiguration ToConfiguration()
    {
        return new HexRenderConfiguration(ShowLabels, ShowOutline, ShowHighlightLabels);
    }
}

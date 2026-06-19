using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

public class HexReachabilityViewModel:BindableBase
{
    private readonly HexReachabilityData _hexReachabilityData;

    public HexReachabilityViewModel(HexReachabilityData hexReachabilityData, ILocalizationService localizationService)
    {
        _hexReachabilityData = hexReachabilityData;
        FormattedLabel = FormatSurfaceLabel(localizationService);
    }
    
    private string FormatSurfaceLabel(ILocalizationService localizationService)
    {
        var surfaceName = Surface switch
        {
            HexSurface.Bridge => localizationService.GetString("Surface_Bridge"),
            HexSurface.Ground => localizationService.GetString("Surface_Ground"),
            _ => Surface.ToString()
        };
        return string.Format(localizationService.GetString("Surface_Option_WithCost"), surfaceName, Cost);
    }
    public HexSurface Surface => _hexReachabilityData.Surface;
    public int Cost => _hexReachabilityData.Cost;
    public string FormattedLabel { get; private set; }
}
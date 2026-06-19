using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

public class SurfaceSelectorViewModel : BindableBase
{
    private readonly Action<HexSurface> _onSurfaceSelected;

    public SurfaceSelectorViewModel(
        IEnumerable<HexReachabilityData> availableSurfaces,
        Action<HexSurface> onSurfaceSelected,
        ILocalizationService localizationService,
        Action? onCancel = null)
    {
        _onSurfaceSelected = onSurfaceSelected;
        Options = availableSurfaces.Select(s => new SurfaceOptionViewModel
        {
            Surface = s.Surface,
            Cost = s.Cost,
            FormattedLabel = FormatSurfaceLabel(s.Surface, s.Cost, localizationService)
        }).ToList();
        CancelCommand = new AsyncCommand(() =>
        {
            onCancel?.Invoke();
            return Task.CompletedTask;
        });
    }

    public IReadOnlyList<SurfaceOptionViewModel> Options { get; }

    public ICommand CancelCommand { get; }

    public void SelectSurface(HexSurface surface)
    {
        _onSurfaceSelected(surface);
    }

    private static string FormatSurfaceLabel(HexSurface surface, int cost, ILocalizationService localizationService)
    {
        var surfaceName = surface switch
        {
            HexSurface.Bridge => localizationService.GetString("Surface_Bridge"),
            HexSurface.Ground => localizationService.GetString("Surface_Ground"),
            _ => surface.ToString()
        };
        return string.Format(localizationService.GetString("Surface_Option_WithCost"), surfaceName, cost);
    }
}

public class SurfaceOptionViewModel
{
    public HexSurface Surface { get; set; }
    public int Cost { get; set; }
    public string FormattedLabel { get; set; } = string.Empty;
}

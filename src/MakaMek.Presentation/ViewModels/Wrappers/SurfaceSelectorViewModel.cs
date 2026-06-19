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
        Options = availableSurfaces.Select(s => new HexReachabilityViewModel(s,localizationService)).ToList();
        CancelCommand = new AsyncCommand(() =>
        {
            onCancel?.Invoke();
            return Task.CompletedTask;
        });
    }

    public IReadOnlyList<HexReachabilityViewModel> Options { get; }

    public ICommand CancelCommand { get; }

    public void SelectSurface(HexSurface surface)
    {
        _onSurfaceSelected(surface);
    }
}
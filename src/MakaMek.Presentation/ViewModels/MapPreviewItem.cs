using Sanet.MakaMek.Map.Models;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

/// <summary>
/// Represents a pre-existing map available for selection
/// </summary>
public class MapPreviewItem : BindableBase
{
    public required string Name { get; init; }
    public required BattleMap Map { get; init; }

    public object? PreviewImage
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsSelected
    {
        get;
        set => SetProperty(ref field, value);
    }
}

using Sanet.MakaMek.Core.Models.Units;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// One limb combination inside a physical attack row. Its checkbox is the only way to select the
/// attack: the limbs of one row are mutually exclusive, and checking one selects the row while
/// clearing every other selected attack.
/// </summary>
public class PhysicalAttackLimbOptionViewModel : BindableBase
{
    private readonly PhysicalAttackOptionViewModel _row;
    private bool _isSelected;

    public PhysicalAttackLimbOptionViewModel(PhysicalAttackLimbVariant variant, PhysicalAttackOptionViewModel row)
    {
        Variant = variant;
        _row = row;
    }

    public PhysicalAttackLimbVariant Variant { get; }

    public string Label => Variant.Label;

    public IReadOnlyList<PartLocation> Limbs => Variant.Limbs;

    public bool IsEnabled => _row.IsEnabled;

    public bool IsSelected
    {
        get => _isSelected;
        set => _row.SelectLimb(this, value);
    }

    internal void SetIsSelected(bool value)
    {
        if (value == _isSelected) return;
        _isSelected = value;
        NotifyPropertyChanged(nameof(IsSelected));
    }

    internal void NotifyEnabledChanged() => NotifyPropertyChanged(nameof(IsEnabled));
}

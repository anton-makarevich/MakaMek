using Sanet.Localization;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// One limb checkbox inside a physical attack row. A limb that cannot make the attack stays
/// visible but disabled, and carries the localized reason for an info flyout.
/// </summary>
public class PhysicalAttackLimbOptionViewModel : BindableBase
{
    private readonly PhysicalAttackOptionViewModel _row;
    private readonly ILocalizationService _localizationService;
    private bool _isSelected;

    public PhysicalAttackLimbOptionViewModel(
        PhysicalAttackLimbVariant variant,
        PhysicalAttackOptionViewModel row,
        ILocalizationService localizationService)
    {
        Variant = variant;
        _row = row;
        _localizationService = localizationService;
    }

    public PhysicalAttackLimbVariant Variant { get; }

    public string Label => Variant.Label;

    public IReadOnlyList<PartLocation> Limbs => Variant.Limbs;

    /// <summary>
    /// Whether this limb can be checked: the row has to be available and the limb itself has to
    /// pass its own eligibility checks.
    /// </summary>
    public bool IsEnabled => _row.IsEnabled && Variant.IsAvailable;

    /// <summary>
    /// The damage this limb would deal, or zero when the attack does no damage (a push).
    /// </summary>
    public int Damage => Variant.Damage;

    public bool HasDamage => Variant.HasDamage;

    public string DamageText => Damage.ToString();

    /// <summary>
    /// The localized reason this limb is unavailable, or null when it is available. Falls back to
    /// the row's reason when the row itself is disabled (for example by the displacement budget).
    /// </summary>
    public string? RestrictionReason => Variant.Restriction is { } restriction
        ? _localizationService.GetString($"PhysicalAttackRestriction_{restriction}")
        : IsEnabled ? null : _row.RestrictionReason;

    public bool HasRestrictionReason => RestrictionReason is not null;

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

    internal void NotifyEnabledChanged()
    {
        NotifyPropertyChanged(nameof(IsEnabled));
        NotifyPropertyChanged(nameof(RestrictionReason));
        NotifyPropertyChanged(nameof(HasRestrictionReason));
    }
}

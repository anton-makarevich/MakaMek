using Sanet.MekForge.Core.Models.Units;
using Sanet.MekForge.Core.Models.Units.Components.Weapons;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MekForge.Core.ViewModels.Wrappers;

public class WeaponSelectionViewModel : BindableBase
{
    private bool _isSelected;
    private readonly Action<Weapon, bool> _onSelectionChanged;
    private Unit? _target;
    private bool _isEnabled;
    private bool _isInRange;
    private string _hitProbability = string.Empty;

    public WeaponSelectionViewModel(
        Weapon weapon,
        bool isInRange,
        bool isSelected,
        bool isEnabled,
        Unit? target,
        Action<Weapon, bool> onSelectionChanged)
    {
        Weapon = weapon;
        IsInRange = isInRange;
        IsSelected = isSelected;
        IsEnabled = isEnabled;
        Target = target;
        _onSelectionChanged = onSelectionChanged;
    }

    public Weapon Weapon { get; }

    public bool IsInRange
    {
        get => _isInRange;
        set => SetProperty(ref _isInRange, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!IsEnabled) return;
            if (value==_isSelected) return;
            SetProperty(ref _isSelected, value);
            _onSelectionChanged(Weapon, value);
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public Unit? Target
    {
        get => _target;
        set => SetProperty(ref _target, value);
    }
    
    public string HitProbability
    {
        get => _hitProbability;
        set => SetProperty(ref _hitProbability, value);
    }

    // Additional properties for UI display
    public string Name => Weapon.Name;
    public string RangeInfo => $"{Weapon.MinimumRange}-{Weapon.LongRange}";
    public string Damage => $"Damage: {Weapon.Damage}";
    public string Heat => $"Heat: {Weapon.Heat}";
}

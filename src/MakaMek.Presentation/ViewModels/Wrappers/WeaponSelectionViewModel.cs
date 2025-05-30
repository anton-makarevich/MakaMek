using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Utils;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

public class WeaponSelectionViewModel : BindableBase
{
    private bool _isSelected;
    private readonly Action<Weapon, bool> _onSelectionChanged;
    private Unit? _target;
    private bool _isEnabled;
    private bool _isInRange;
    private ToHitBreakdown? _modifiersBreakdown;
    private readonly ILocalizationService _localizationService;

    public WeaponSelectionViewModel(
        Weapon weapon,
        bool isInRange,
        bool isSelected,
        bool isEnabled,
        Unit? target,
        Action<Weapon, bool> onSelectionChanged,
        ILocalizationService localizationService,
        int remainingAmmoShots = -1)
    {
        Weapon = weapon;
        IsInRange = isInRange;
        IsSelected = isSelected;
        IsEnabled = isEnabled;
        Target = target;
        _onSelectionChanged = onSelectionChanged;
        _localizationService = localizationService;
        RemainingAmmoShots = remainingAmmoShots;
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
        get => _isEnabled && HitProbability > 0 && HasSufficientAmmo && Weapon.IsAvailable;
        set => SetProperty(ref _isEnabled, value);
    }

    public Unit? Target
    {
        get => _target;
        set => SetProperty(ref _target, value);
    }
    
    /// <summary>
    /// Gets or sets the remaining ammo shots for this weapon
    /// </summary>
    public int RemainingAmmoShots { get; }

    /// <summary>
    /// Gets whether the weapon requires ammo
    /// </summary>
    public bool RequiresAmmo => Weapon.RequiresAmmo;
    
    /// <summary>
    /// Gets whether the weapon has sufficient ammo to fire
    /// </summary>
    public bool HasSufficientAmmo => !RequiresAmmo || RemainingAmmoShots > 0;
    
    /// <summary>
    /// Gets or sets the detailed breakdown of hit modifiers
    /// </summary>
    public ToHitBreakdown? ModifiersBreakdown
    {
        get => _modifiersBreakdown;
        set
        {
            SetProperty(ref _modifiersBreakdown, value);
            NotifyPropertyChanged(nameof(HitProbability));
            NotifyPropertyChanged(nameof(HitProbabilityText));
            NotifyPropertyChanged(nameof(AttackPossibilityDescription));
        }
    }
    
    /// <summary>
    /// Gets the hit probability as a value between 0 and 100
    /// </summary>
    public double HitProbability => !_isEnabled || !HasSufficientAmmo ? 0 :
        ModifiersBreakdown is { HasLineOfSight: true, Total: <= 12 }
            ? DiceUtils.Calculate2d6Probability(ModifiersBreakdown.Total)
            : 0;
    
    /// <summary>
    /// Gets the formatted hit probability string for display
    /// </summary>
    public string HitProbabilityText => HitProbability <= 0 ? "-" : $"{HitProbability:F0}%";
    
    /// <summary>
    /// Gets a formatted string describing why an attack is possible or not possible,
    /// including modifiers breakdown, range issues, or targeting issues
    /// </summary>
    public string AttackPossibilityDescription
    {
        get
        {
            // Check if weapon is destroyed
            if (Weapon.IsDestroyed)
                return _localizationService.GetString("Attack_WeaponDestroyed");
            // Check if weapon's location is destroyed
            if (Weapon.MountedOn?.IsDestroyed == true)
                return _localizationService.GetString("Attack_LocationDestroyed");
            // Check if weapon is out of ammo
            if (RequiresAmmo && RemainingAmmoShots <= 0)
                return _localizationService.GetString("Attack_NoAmmo");
            // Check if weapon is in range
            if (!IsInRange)
                return _localizationService.GetString("Attack_OutOfRange");
            // Check if weapon is targetting different target
            if (!IsEnabled && Target != null)
                return string.Format(_localizationService.GetString("Attack_Targeting"),Target.Name);
            // Check if we have modifiers breakdown
            if (ModifiersBreakdown == null)
                return _localizationService.GetString("Attack_NoModifiersCalculated");
            // Check line of sight
            if (!ModifiersBreakdown.HasLineOfSight)
                return _localizationService.GetString("Attack_NoLineOfSight");
            // If we get here, show the modifiers breakdown
            var lines = new List<string>
            {
                $"{_localizationService.GetString("Attack_TargetNumber")}: {ModifiersBreakdown.Total}"
            };
            lines.AddRange(ModifiersBreakdown.AllModifiers.Select(modifier => modifier.Render(_localizationService)));
            // Add all modifiers using their Format method
            return string.Join(Environment.NewLine, lines);
        }
    }

    // Additional properties for UI display
    public string Name => Weapon.Name;
    public string RangeInfo => $"{Weapon.LongRange}";
    
    public string Damage => $"{Weapon.Damage}";
    public string Heat => $"{Weapon.Heat}";
    
    /// <summary>
    /// Gets the formatted remaining ammo shots for display
    /// </summary>
    public string Ammo => RequiresAmmo ? $"{RemainingAmmoShots}" : string.Empty;
}

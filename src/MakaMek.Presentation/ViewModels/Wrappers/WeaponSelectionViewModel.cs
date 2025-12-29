using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Utils;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

public class WeaponSelectionViewModel : BindableBase
{
    private readonly Action<Weapon, bool> _onSelectionChanged;
    private readonly Action<AimedShotLocationSelectorViewModel> _onShowAimedShotLocationSelector;
    private readonly Action _onHideAimedShotLocationSelector;
    private bool _isEnabled;
    private readonly ILocalizationService _localizationService;
    private readonly IToHitCalculator _toHitCalculator;
    private ToHitBreakdown? _originalModifiersBreakdown;

    public WeaponSelectionViewModel(
        Weapon weapon,
        bool isInRange,
        bool isSelected,
        bool isEnabled,
        IUnit? target,
        Action<Weapon, bool> onSelectionChanged,
        Action<AimedShotLocationSelectorViewModel> onShowAimedShotLocationSelector, 
        Action onHideAimedShotLocationSelector,
        ILocalizationService localizationService,
        IToHitCalculator toHitCalculator,
        int remainingAmmoShots = -1)
    {
        Weapon = weapon;
        IsInRange = isInRange;
        IsSelected = isSelected;
        IsEnabled = isEnabled;
        Target = target;
        _onSelectionChanged = onSelectionChanged;
        _localizationService = localizationService;
        _toHitCalculator = toHitCalculator;
        _onShowAimedShotLocationSelector = onShowAimedShotLocationSelector;
        _onHideAimedShotLocationSelector = onHideAimedShotLocationSelector;
        RemainingAmmoShots = remainingAmmoShots;
    }

    public Weapon Weapon { get; }

    public bool IsInRange
    {
        get;
        set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(IsAimedShotAvailable));
        }
    }

    public bool IsSelected
    {
        get;
        set
        {
            if (!IsEnabled) return;
            if (value == field) return;
            SetProperty(ref field, value);
            _onSelectionChanged(Weapon, value);
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled && HitProbability > 0 && HasSufficientAmmo && Weapon.IsAvailable;
        set => SetProperty(ref _isEnabled, value);
    }

    public IUnit? Target
    {
        get;
        set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(IsAimedShotAvailable));
        }
    }

    /// <summary>
    /// Gets or sets the remaining ammo shots for this weapon
    /// </summary>
    private int RemainingAmmoShots { get; }

    /// <summary>
    /// Gets whether the weapon requires ammo
    /// </summary>
    public bool RequiresAmmo => Weapon.RequiresAmmo;

    /// <summary>
    /// Gets whether the weapon has enough ammo to fire
    /// </summary>
    public bool HasSufficientAmmo => !RequiresAmmo || RemainingAmmoShots > 0;

    /// <summary>
    /// Gets or sets the detailed breakdown of hit modifiers
    /// </summary>
    public ToHitBreakdown? ModifiersBreakdown
    {
        get;
        set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(HitProbability));
            NotifyPropertyChanged(nameof(HitProbabilityText));
            NotifyPropertyChanged(nameof(AttackPossibilityDescription));
        }
    }

    public ToHitBreakdown? AimedOtherModifiersBreakdown { get; set; }

    public ToHitBreakdown? AimedHeadModifiersBreakdown { get; set; }

    /// <summary>
    /// Gets the hit probability as a value between 0 and 100
    /// </summary>
    public double HitProbability => !_isEnabled || !HasSufficientAmmo
        ? 0
        : ModifiersBreakdown is { HasLineOfSight: true, Total: <= 12 }
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
            // Check if a weapon is destroyed
            if (Weapon.IsDestroyed)
                return _localizationService.GetString("Attack_WeaponDestroyed");
            // Check if a weapon's location is destroyed
            if (Weapon.FirstMountPart?.IsDestroyed == true)
                return _localizationService.GetString("Attack_LocationDestroyed");
            // Check if a weapon is out of ammo
            if (RequiresAmmo && RemainingAmmoShots <= 0)
                return _localizationService.GetString("Attack_NoAmmo");
            // Check if a weapon is in range
            if (!IsInRange)
                return _localizationService.GetString("Attack_OutOfRange");
            // Check if a weapon is targeting a different target
            if (!IsEnabled && Target != null)
                return string.Format(_localizationService.GetString("Attack_Targeting"), Target.Name);
            // Check if we have modifiers breakdown
            if (ModifiersBreakdown == null)
                return _localizationService.GetString("Attack_NoModifiersCalculated");
            // Check line of sight
            if (!ModifiersBreakdown.HasLineOfSight)
                return _localizationService.GetString("Attack_NoLineOfSight");
            // Check if the weapon is outside the firing arc
            if (ModifiersBreakdown.FiringArc == null)
                return _localizationService.GetString("Attack_OutsideFiringArc");
            // Check if the target number is impossible
            if (ModifiersBreakdown.Total > 12)
                return _localizationService.GetString("Attack_ImpossibleToHit");
            // Unavailable for some other reason
            if (!IsEnabled)
                return Weapon.GetWeaponRestrictionReason(_localizationService);
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

    /// <summary>
    /// Gets or sets the aimed shot target location
    /// </summary>
    public PartLocation? AimedShotTarget
    {
        // Expose the aimed target only when the weapon is actually selected to fire
        get => IsSelected? field:null;
        set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(IsAimedShot));
            NotifyPropertyChanged(nameof(AimedShotText));
        }
    }

    /// <summary>
    /// Gets whether aimed shots are available for this weapon and target combination
    /// </summary>
    public bool IsAimedShotAvailable => IsInRange && CanUseAimedShot(Weapon, Target);

    /// <summary>
    /// Gets whether this weapon is currently configured for an aimed shot
    /// </summary>
    public bool IsAimedShot => AimedShotTarget.HasValue;

    /// <summary>
    /// Gets the display text for aimed shot status
    /// </summary>
    public string AimedShotText => IsAimedShot && IsAimedShotAvailable 
        ? _localizationService.GetString($"MechPart_{AimedShotTarget}_Short") 
        : string.Empty;
    
    /// <summary>
    /// Clears the aimed shot target, reverting to normal shot
    /// </summary>
    public void ClearAimedShot()
    {
        AimedShotTarget = null;
        ModifiersBreakdown = _originalModifiersBreakdown;
        _originalModifiersBreakdown = null;
    }


    /// <summary>
    /// Determines if aimed shots are available for the given weapon and target combination
    /// </summary>
    private bool CanUseAimedShot(Weapon weapon, IUnit? target)
    {
        // Aimed shots require:
        // 1. Target must be immobile
        // 2. Weapon must be aimed shot capable
        return target?.IsImmobile == true &&
               weapon.IsAimShotCapable;
    }

    /// <summary>
    /// Shows the aimed shot selector for the specified weapon
    /// </summary>
    public void ShowAimedShotSelector()
    {
        if (Target == null || Weapon.FirstMountPart?.Unit == null )
            return;

        if (!IsAimedShotAvailable || AimedHeadModifiersBreakdown == null || AimedOtherModifiersBreakdown == null)
            return;

        var bodyPartSelector = new AimedShotLocationSelectorViewModel(
            Target,
            AimedHeadModifiersBreakdown,
            AimedOtherModifiersBreakdown,
            OnAimedShotTargetSelected,
            _localizationService
        );
        // Create and show the body part selector

        _onShowAimedShotLocationSelector(bodyPartSelector);
    }

    /// <summary>
    /// Handles the selection of an aimed shot target
    /// </summary>
    private void OnAimedShotTargetSelected(PartLocation targetLocation)
    {
        AimedShotTarget = targetLocation;

        _originalModifiersBreakdown = ModifiersBreakdown;
        if (_originalModifiersBreakdown != null)
            ModifiersBreakdown = _toHitCalculator.AddAimedShotModifier(_originalModifiersBreakdown, targetLocation);

        _onHideAimedShotLocationSelector();
    }
}

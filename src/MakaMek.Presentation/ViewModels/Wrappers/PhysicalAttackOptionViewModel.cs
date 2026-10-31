using Sanet.Localization;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Utils;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// One selectable attack type in the physical attack declaration dialog. Modelled on
/// <see cref="WeaponSelectionViewModel"/>: the row carries the pending selection, the limb
/// variants that can be declared and the localized answer to "why can or cannot I use this?".
/// The to-hit calculator has no limb input, so one row per attack type is enough; the limb
/// variant only changes the declaration payload.
/// </summary>
public class PhysicalAttackOptionViewModel : BindableBase
{
    private readonly Action<PhysicalAttackOptionViewModel, bool> _onSelectionChanged;
    private readonly ILocalizationService _localizationService;
    private bool _isEnabled;
    private bool _isSelected;
    private PhysicalAttackLimbVariant? _selectedVariant;

    public PhysicalAttackOptionViewModel(
        PhysicalAttackType attackType,
        IReadOnlyList<PhysicalAttackLimbVariant> limbVariants,
        PhysicalAttackLimbVariant? selectedVariant,
        bool isEnabled,
        string? restrictionReason,
        int? targetNumber,
        PhysicalAttackBreakdown? breakdown,
        Action<PhysicalAttackOptionViewModel, bool> onSelectionChanged,
        ILocalizationService localizationService)
    {
        AttackType = attackType;
        LimbVariants = limbVariants;
        _isEnabled = isEnabled;
        RestrictionReason = restrictionReason;
        TargetNumber = targetNumber;
        Breakdown = breakdown;
        _onSelectionChanged = onSelectionChanged;
        _localizationService = localizationService;

        // Initialise directly rather than through the property: the property would also select the
        // row and fire the callback before the state has finished building it.
        _selectedVariant = selectedVariant;
    }

    public PhysicalAttackType AttackType { get; }

    public string AttackTypeName => _localizationService.GetString($"PhysicalAttackType_{AttackType}");

    /// <summary>
    /// The limb combinations this attack can be declared with. Empty when the attack is not
    /// available against the selected target.
    /// </summary>
    public IReadOnlyList<PhysicalAttackLimbVariant> LimbVariants { get; }

    /// <summary>
    /// The limb combination that will be sent in the declaration. Choosing a variant on an enabled
    /// but unselected row selects the row, so the dialog has one active choice.
    /// </summary>
    public PhysicalAttackLimbVariant? SelectedVariant
    {
        get => _selectedVariant;
        set
        {
            if (!IsEnabled) return;
            if (value == _selectedVariant) return;
            SetProperty(ref _selectedVariant, value);
            if (!IsSelected) IsSelected = true;
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!IsEnabled) return;
            if (value == _isSelected) return;
            SetProperty(ref _isSelected, value);
            _onSelectionChanged(this, value);
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (value == _isEnabled) return;
            SetProperty(ref _isEnabled, value);
            NotifyPropertyChanged(nameof(HitProbability));
            NotifyPropertyChanged(nameof(HitProbabilityText));
            NotifyPropertyChanged(nameof(AttackPossibilityDescription));
        }
    }

    /// <summary>
    /// The localized reason this attack is unavailable, or null when it is available.
    /// </summary>
    public string? RestrictionReason { get; }

    /// <summary>
    /// The to-hit number against the selected target. Only set for available rows.
    /// </summary>
    public int? TargetNumber { get; }

    /// <summary>
    /// The modifier breakdown behind <see cref="TargetNumber"/>. Only set for available rows.
    /// </summary>
    public PhysicalAttackBreakdown? Breakdown { get; }

    /// <summary>
    /// A physical attack's to-hit roll is 2D6 (the 1D6 roll is only for the hit location), so a
    /// modified to-hit number of 13 or more can never succeed. Such an attack is still a legal
    /// declaration, so it stays selectable.
    /// </summary>
    public bool IsImpossible => TargetNumber is { } number && number >= DiceUtils.Impossible2D6Roll;

    public string TargetNumberText => TargetNumber is { } number
        ? $"{_localizationService.GetString("Attack_TargetNumber")}: {number}"
        : string.Empty;

    /// <summary>
    /// The hit probability as a value between 0 and 100. Zero for unavailable or impossible rows.
    /// </summary>
    public double HitProbability => !IsEnabled || TargetNumber is not { } number || number > 12
        ? 0
        : DiceUtils.Calculate2d6Probability(number);

    /// <summary>
    /// The formatted hit probability for display: "-" when unavailable or impossible.
    /// </summary>
    public string HitProbabilityText => HitProbability <= 0 ? "-" : $"{HitProbability:F0}%";

    /// <summary>
    /// The content of the row's info flyout: the restriction reason for an unavailable row,
    /// otherwise the target number, the impossible text when applicable and every modifier line.
    /// </summary>
    public string AttackPossibilityDescription
    {
        get
        {
            if (!IsEnabled || TargetNumber is not { } number || Breakdown is null)
                return RestrictionReason
                       ?? _localizationService.GetString("PhysicalAttackDeclaration_Unavailable");

            var lines = new List<string> { TargetNumberText };
            if (IsImpossible)
                lines.Add(_localizationService.GetString("Attack_ImpossibleToHit"));
            lines.AddRange(Breakdown.AllModifiers
                .Select(modifier => modifier.Render(_localizationService)));
            return string.Join(Environment.NewLine, lines);
        }
    }
}

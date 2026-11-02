using Sanet.Localization;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Utils;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// One attack type in the physical attack declaration dialog. The row has no checkbox of its own:
/// the player selects it through the checkbox of one of its limb options, which then clears any
/// other selected attack. The to-hit calculator has no limb input, so the row carries one target
/// number and modifier breakdown, while each limb option carries its own damage and, when it
/// cannot be used, the reason why.
/// </summary>
public class PhysicalAttackOptionViewModel : BindableBase
{
    private readonly Action<PhysicalAttackOptionViewModel, bool> _onSelectionChanged;
    private readonly ILocalizationService _localizationService;
    private bool _isEnabled;
    private bool _isSelected;
    private PhysicalAttackLimbOptionViewModel? _selectedLimbOption;

    public PhysicalAttackOptionViewModel(
        PhysicalAttackType attackType,
        IReadOnlyList<PhysicalAttackLimbVariant> limbVariants,
        bool isEnabled,
        string? restrictionReason,
        int? targetNumber,
        PhysicalAttackBreakdown? breakdown,
        Action<PhysicalAttackOptionViewModel, bool> onSelectionChanged,
        ILocalizationService localizationService)
    {
        AttackType = attackType;
        _isEnabled = isEnabled;
        RestrictionReason = restrictionReason;
        TargetNumber = targetNumber;
        Breakdown = breakdown;
        _onSelectionChanged = onSelectionChanged;
        _localizationService = localizationService;

        LimbOptions = limbVariants
            .Select(variant => new PhysicalAttackLimbOptionViewModel(variant, this, _localizationService))
            .ToList();
    }

    public PhysicalAttackType AttackType { get; }

    public string AttackTypeName => _localizationService.GetString($"PhysicalAttackType_{AttackType}");

    /// <summary>
    /// The limb combinations this attack can be declared with. Empty when the attack has no
    /// reaching limbs against the selected target.
    /// </summary>
    public IReadOnlyList<PhysicalAttackLimbOptionViewModel> LimbOptions { get; }

    public bool HasLimbOptions => LimbOptions.Count > 0;

    /// <summary>
    /// Whether one of the limb options is checked. Derived from the limb selection rather than
    /// exposed as a checkbox of its own.
    /// </summary>
    public bool IsSelected => _isSelected;

    /// <summary>
    /// The limb combination that will be sent in the declaration, or null when no limb is checked.
    /// </summary>
    public PhysicalAttackLimbVariant? SelectedVariant => _selectedLimbOption?.Variant;

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
            foreach (var option in LimbOptions)
                option.NotifyEnabledChanged();
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

    /// <summary>
    /// Applies a limb checkbox change. The limb options of one row are mutually exclusive.
    /// Checking one selects the row; unchecking the active one clears the row so the dialog falls
    /// back to "Skip Attack" until another limb is picked.
    /// </summary>
    internal void SelectLimb(PhysicalAttackLimbOptionViewModel limb, bool isSelected)
    {
        if (!IsEnabled || !limb.Variant.IsAvailable)
        {
            // Restore the checkbox to the actual state when a disabled control tries to change it.
            limb.SetIsSelected(ReferenceEquals(limb, _selectedLimbOption));
            return;
        }

        if (!isSelected)
        {
            if (!ReferenceEquals(limb, _selectedLimbOption)) return;

            _selectedLimbOption = null;
            limb.SetIsSelected(false);
            NotifyPropertyChanged(nameof(SelectedVariant));
            SetSelected(false);
            return;
        }

        if (!ReferenceEquals(limb, _selectedLimbOption))
        {
            _selectedLimbOption?.SetIsSelected(false);
            _selectedLimbOption = limb;
            NotifyPropertyChanged(nameof(SelectedVariant));
        }

        limb.SetIsSelected(true);
        SetSelected(true);
    }

    /// <summary>
    /// Clears the row and all of its limb checkboxes without notifying the state. Used when
    /// another row has just been selected.
    /// </summary>
    internal void ClearSelection()
    {
        _selectedLimbOption = null;
        foreach (var option in LimbOptions)
            option.SetIsSelected(false);
        NotifyPropertyChanged(nameof(SelectedVariant));
        SetSelected(false, notify: false);
    }

    private void SetSelected(bool value, bool notify = true)
    {
        if (value == _isSelected) return;
        _isSelected = value;
        NotifyPropertyChanged(nameof(IsSelected));
        if (notify) _onSelectionChanged(this, value);
    }
}

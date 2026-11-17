using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Builds and projects the weapon-selection rows of the weapons-attack dialog, including range/cover
/// restrictions, to-hit breakdowns, aimed-shot variants and the selected-target panel. Kept out of
/// <see cref="WeaponsAttackState"/> so the state only orchestrates the selection steps.
/// </summary>
public sealed class WeaponSelectionPresenter
{
    private readonly BattleMapViewModel _viewModel;
    private readonly IClientGame _game;
    private readonly Dictionary<Weapon, WeaponSelectionViewModel> _weaponViewModels = new();

    public WeaponSelectionPresenter(BattleMapViewModel viewModel, IClientGame game)
    {
        _viewModel = viewModel;
        _game = game;
    }

    public IEnumerable<WeaponSelectionViewModel> Items => _weaponViewModels.Values;

    public PartLocation? GetAimedShotTarget(Weapon weapon) =>
        _weaponViewModels.TryGetValue(weapon, out var weaponVm) ? weaponVm.AimedShotTarget : null;

    public void Clear()
    {
        _weaponViewModels.Clear();
        _viewModel.WeaponSelectionItems.Clear();
    }

    public void Rebuild(
        IUnit attacker,
        Action<Weapon, bool> onSelectionChanged,
        Action<WeaponSelectionViewModel> onAimedShotRequest)
    {
        Clear();

        foreach (var weapon in attacker.Parts.Values.SelectMany(p => p.GetComponents<Weapon>()))
        {
            var viewModel = new WeaponSelectionViewModel(
                weapon: weapon,
                isInRange: false,
                isSelected: false,
                isEnabled: false,
                target: null,
                onSelectionChanged: onSelectionChanged,
                onAimedShotRequest: onAimedShotRequest,
                localizationService: _viewModel.LocalizationService,
                toHitCalculator: _game.ToHitCalculator,
                remainingAmmoShots: attacker.GetRemainingAmmoShots(weapon)
            );
            _weaponViewModels[weapon] = viewModel;
        }

        UpdateViewModelWeaponItems();
    }

    public void Update(IUnit? attacker, IUnit? target, WeaponRangeHighlighter ranges)
    {
        if (attacker?.Position == null || target?.Position == null) return;

        // Computed once per refresh and shared by every row.
        var attackerHasPartialCover = HasPartialCover(attacker, target);
        var isPrimaryTarget = IsPrimaryTarget(attacker, target);

        foreach (var (weapon, vm) in _weaponViewModels)
        {
            ProjectWeaponViewModel(weapon, vm, attacker, target, ranges, attackerHasPartialCover, isPrimaryTarget);
        }

        UpdateViewModelWeaponItems();
    }

    /// <summary>
    /// Whether the target gives the attacker partial cover, using the reversed line of sight.
    /// </summary>
    private bool HasPartialCover(IUnit attacker, IUnit target)
    {
        var reversedLosResult = _game.BattleMap?.GetLineOfSight(
            target.Position!,
            attacker.Position!,
            target.Height,
            attacker.Height);

        return reversedLosResult != null && _game.RulesProvider.HasPartialCover(attacker, reversedLosResult);
    }

    private static bool IsPrimaryTarget(IUnit attacker, IUnit target) =>
        attacker.WeaponAttackState.PrimaryTarget == null
        || attacker.WeaponAttackState.PrimaryTarget == target;

    private void ProjectWeaponViewModel(
        Weapon weapon,
        WeaponSelectionViewModel vm,
        IUnit attacker,
        IUnit target,
        WeaponRangeHighlighter ranges,
        bool attackerHasPartialCover,
        bool isPrimaryTarget)
    {
        // Only process available weapons
        if (!weapon.IsAvailable)
        {
            vm.ModifiersBreakdown = null;
            return;
        }

        var assignedTarget = ApplyRangeAndSelection(weapon, vm, attacker, target, ranges);
        ApplyPartialCoverRestriction(weapon, vm, attackerHasPartialCover);
        vm.Target = assignedTarget;
        ApplyModifierBreakdown(weapon, vm, attacker, target, isPrimaryTarget);
    }

    /// <summary>
    /// Applies the range/selection flags to the row and returns the unit this weapon is assigned to.
    /// </summary>
    private IUnit? ApplyRangeAndSelection(
        Weapon weapon,
        WeaponSelectionViewModel vm,
        IUnit attacker,
        IUnit target,
        WeaponRangeHighlighter ranges)
    {
        var isInRange = ranges.IsWeaponInRange(weapon, target, attacker, _game);
        var assignedTarget = attacker.WeaponAttackState.WeaponTargets.GetValueOrDefault(weapon);
        var isSelected = attacker.WeaponAttackState.IsWeaponAssigned(weapon, assignedTarget) == true;
        vm.IsInRange = isInRange;
        vm.IsSelected = isSelected;
        var isWeaponAvailable = weapon.IsAvailableForAttack();

        // Clear restriction reason before applying logic
        vm.RestrictionReason = null;

        vm.IsEnabled = (attacker.WeaponAttackState.IsWeaponAssigned(weapon) != true || assignedTarget == target)
                       && isInRange && isWeaponAvailable;
        return assignedTarget;
    }

    /// <summary>
    /// Leg weapons cannot fire out of partial cover.
    /// </summary>
    private void ApplyPartialCoverRestriction(
        Weapon weapon,
        WeaponSelectionViewModel vm,
        bool attackerHasPartialCover)
    {
        if (!attackerHasPartialCover) return;

        var location = weapon.FirstMountPart?.Location;
        if (location is not { } coveredLocation || !_game.RulesProvider.CanPartBeCovered(coveredLocation)) return;

        vm.IsEnabled = false;
        vm.RestrictionReason = _viewModel.LocalizationService.GetString("WeaponRestriction_PartialCoverLegs");
    }

    /// <summary>
    /// Fills the to-hit breakdowns, including the two aimed-shot variants when aimed shots are on.
    /// Rows without range or without a map/attacker keep empty breakdowns.
    /// </summary>
    private void ApplyModifierBreakdown(
        Weapon weapon,
        WeaponSelectionViewModel vm,
        IUnit attacker,
        IUnit target,
        bool isPrimaryTarget)
    {
        vm.ModifiersBreakdown = null;
        vm.AimedHeadModifiersBreakdown = null;
        vm.AimedOtherModifiersBreakdown = null;

        // Set modifier breakdown only when in range
        if (!vm.IsInRange) return;
        if (_game.BattleMap == null) return;

        // Calculate base breakdown without aimed shot, passing the primary target information
        var baseBreakdown = _game.ToHitCalculator.GetModifierBreakdown(
            attacker,
            target,
            weapon,
            _game.BattleMap,
            isPrimaryTarget,
            vm.AimedShotTarget);

        vm.ModifiersBreakdown = baseBreakdown;

        if (!vm.IsAimedShotAvailable) return;

        // Add aimed shot modifiers to the existing breakdown
        vm.AimedHeadModifiersBreakdown = _game.ToHitCalculator.AddAimedShotModifier(baseBreakdown, PartLocation.Head);
        vm.AimedOtherModifiersBreakdown = _game.ToHitCalculator.AddAimedShotModifier(baseBreakdown, PartLocation.CenterTorso);
    }

    /// <summary>
    /// Shows the aimed-shot location selector for the requested row.
    /// </summary>
    public void ShowAimedShotSelector(WeaponSelectionViewModel weaponVm)
    {
        if (weaponVm.Target == null
            || weaponVm.AimedHeadModifiersBreakdown == null
            || weaponVm.AimedOtherModifiersBreakdown == null)
            return;

        var selectorVm = new AimedShotLocationSelectorViewModel(
            weaponVm.Target,
            weaponVm.AimedHeadModifiersBreakdown,
            weaponVm.AimedOtherModifiersBreakdown,
            location =>
            {
                weaponVm.ApplyAimedShotResult(location);
                _viewModel.HideAimedShotLocationSelector();
            },
            _viewModel.LocalizationService);

        _viewModel.ShowAimedShotLocationSelector(selectorVm);
    }

    public void UpdateSelectedTarget(IUnit? attacker, IUnit? target, Action<IUnit> onSetPrimaryTarget)
    {
        if (target == null || attacker == null)
        {
            _viewModel.SelectedTarget = null;
            return;
        }

        // Only show target info if there are weapons assigned to this target
        var hasWeaponsForTarget = attacker.WeaponAttackState.WeaponTargets.Values.Contains(target);

        var isPrimary = attacker.WeaponAttackState.PrimaryTarget == null
                        || attacker.WeaponAttackState.PrimaryTarget == target;

        // Update or create the view model for the selected target
        if (_viewModel.SelectedTarget?.Target == target)
        {
            _viewModel.SelectedTarget.IsPrimary = isPrimary;
            _viewModel.SelectedTarget.HasWeaponsForTarget = hasWeaponsForTarget;
        }
        else
        {
            _viewModel.SelectedTarget = new TargetSelectionViewModel(target, isPrimary, hasWeaponsForTarget, onSetPrimaryTarget);
        }
    }

    /// <summary>
    /// Copies the rows into the view model's bound collection.
    /// </summary>
    private void UpdateViewModelWeaponItems()
    {
        _viewModel.WeaponSelectionItems.Clear();
        foreach (var kvp in _weaponViewModels)
        {
            _viewModel.WeaponSelectionItems.Add(kvp.Value);
        }
    }
}

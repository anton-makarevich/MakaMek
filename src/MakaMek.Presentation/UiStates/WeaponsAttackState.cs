using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Highlights;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.UiStates;

public class WeaponsAttackState : IUiState, ICombatSelectionRoles
{
    private readonly BattleMapViewModel _viewModel;
    private readonly List<HexDirection> _availableDirections = [];
    private IReadOnlyDictionary<Weapon, HashSet<HexCoordinates>> _weaponRanges = new Dictionary<Weapon, HashSet<HexCoordinates>>();
    private readonly Dictionary<Weapon, WeaponSelectionViewModel> _weaponViewModels = new();
    private readonly Lock _stateLock = new();
    private readonly WeaponsAttackSelectionPolicy _selectionPolicy;
    private readonly WeaponRangeAnalyzer _rangeAnalyzer;

    public IClientGame Game { get; }

    public WeaponsAttackStep CurrentStep { get; private set; } = WeaponsAttackStep.SelectingUnit;

    public string ActionLabel => CurrentStep switch
    {
        WeaponsAttackStep.SelectingUnit => _viewModel.LocalizationService.GetString("Action_SelectUnitToFire"),
        WeaponsAttackStep.ActionSelection => _viewModel.LocalizationService.GetString("Action_SelectAction"),
        WeaponsAttackStep.WeaponsConfiguration => _viewModel.LocalizationService.GetString("Action_ConfigureWeapons"),
        WeaponsAttackStep.TargetSelection => _viewModel.LocalizationService.GetString("Action_SelectTarget"),
        _ => string.Empty
    };

    public bool IsActionRequired => this.CanHumanPlayerAct();

    public bool CanExecutePlayerAction =>
        (CurrentStep is WeaponsAttackStep.ActionSelection or WeaponsAttackStep.TargetSelection)
        && !_viewModel.AreActionsMenuOffMap;

    public string PlayerActionLabel
    {
        get
        {
            if (!IsActionRequired) return string.Empty;
            return CurrentStep switch
            {
                WeaponsAttackStep.ActionSelection => _viewModel.LocalizationService.GetString("Action_SkipAttack"),
                WeaponsAttackStep.TargetSelection => Attacker?.WeaponAttackState.SelectedWeapons.Any() == true
                    ? _viewModel.LocalizationService.GetString("Action_DeclareAttack")
                    : _viewModel.LocalizationService.GetString("Action_SkipAttack"),
                _ => string.Empty
            };
        }
    }

    public void ExecutePlayerAction()
    {
        if (!this.CanHumanPlayerAct()) return;
        if (CurrentStep == WeaponsAttackStep.ActionSelection || CurrentStep == WeaponsAttackStep.TargetSelection)
        {
            ConfirmWeaponSelections();
        }
    }

    public WeaponsAttackState(
        BattleMapViewModel viewModel,
        WeaponsAttackSelectionPolicy? selectionPolicy = null,
        WeaponRangeAnalyzer? rangeAnalyzer = null)
    {
        Game = viewModel.Game! ?? throw new InvalidOperationException("Game is not client game");
        _viewModel = viewModel;
        _selectionPolicy = selectionPolicy ?? new WeaponsAttackSelectionPolicy();
        _rangeAnalyzer = rangeAnalyzer ?? new WeaponRangeAnalyzer();
        if (Game.PhaseStepState?.ActivePlayer == null)
        {
            throw new InvalidOperationException("Active player is null"); 
        }
    }

    public IUnit? SelectedUnit
    {
        get => CurrentStep == WeaponsAttackStep.TargetSelection ? SelectedTarget : Attacker;
        private set
        {
            lock (_stateLock)
            {
                var outcome = _selectionPolicy.Decide(
                    CurrentStep,
                    value,
                    this.CanHumanPlayerAct(),
                    Game.PhaseStepState?.ActivePlayer.Id,
                    _weaponRanges.Values);
                switch (outcome)
                {
                    case WeaponsAttackSelectionOutcome.Ignore:
                        return;
                    case WeaponsAttackSelectionOutcome.SelectAttacker:
                        ApplyAttackerSelection(value!);
                        break;
                    case WeaponsAttackSelectionOutcome.SelectTarget:
                        ApplyTargetSelection(value!);
                        break;
                }

                _viewModel.NotifyStateChanged();
            }
        }
    }

    private void ApplyAttackerSelection(IUnit unit)
    {
        Attacker = unit;
        CreateWeaponViewModels();
        CurrentStep = WeaponsAttackStep.ActionSelection;

        if (unit.CanFireWeapons)
        {
            // Highlight weapon ranges for the newly selected unit
            HighlightWeaponRanges();
        }
    }

    private void ApplyTargetSelection(IUnit unit)
    {
        SelectedTarget = unit;
        UpdateWeaponViewModels();
        _viewModel.IsWeaponSelectionVisible = true;
    }

    public void HandleUnitSelectionFromList(IUnit? unit)
    {
        SelectedUnit = unit;
    }

    public void HandleHexSelection(Hex hex)
    {
        // Handle cancellation when a direction selector is shown (torso rotation step)
        if (CurrentStep == WeaponsAttackStep.WeaponsConfiguration)
        {
            CancelTorsoRotation();
            return;
        }

        if (CurrentStep == WeaponsAttackStep.TargetSelection)
        {
            // Cancel target selection if clicked outside weapon range (non-highlighted hex)
            if (!IsHexInWeaponRange(hex.Coordinates))
            {
                CancelTargetSelection();
                return;
            }
        }

        HandleUnitSelectionFromHex(hex);
    }

    private void HandleUnitSelectionFromHex(Hex hex)
    {
        if (!this.CanHumanPlayerAct()) return;

        // Two units can share a hex after a blocked domino displacement. Pick the candidate by the
        // role the current step expects instead of taking whichever comes first.
        var unit = FindUnitOnHex(hex);
        if (unit == null) return;

        lock (_stateLock)
        {
            if (CurrentStep is WeaponsAttackStep.SelectingUnit or WeaponsAttackStep.ActionSelection)
            {
                SelectAttackerFromHex(unit);
                return;
            }

            if (CurrentStep == WeaponsAttackStep.TargetSelection)
            {
                if (TrySelectTargetFromHex(unit, hex)) _viewModel.NotifyStateChanged();
                return;
            }

            _viewModel.NotifyStateChanged();
        }
    }

    private IUnit? FindUnitOnHex(Hex hex)
    {
        var activePlayerId = Game.PhaseStepState?.ActivePlayer.Id;
        var selectingAttacker = CurrentStep is WeaponsAttackStep.SelectingUnit or WeaponsAttackStep.ActionSelection;

        return _viewModel.Units.FirstOrDefault(unit =>
            unit.Position?.Coordinates == hex.Coordinates
            && (selectingAttacker
                ? unit.Owner?.Id == activePlayerId
                : unit.Owner?.Id != activePlayerId));
    }

    /// <summary>
    /// Replaces the attacker with the unit under the cursor, dropping the previous attacker's
    /// selections and highlights first.
    /// </summary>
    private void SelectAttackerFromHex(IUnit unit)
    {
        if (unit.Owner != Game.PhaseStepState?.ActivePlayer || unit.HasDeclaredWeaponAttack)
            return;

        if (Attacker is not null)
        {
            ClearWeaponRangeHighlights();
            ResetUnitSelection();
        }

        SelectedUnit = unit;
    }

    /// <summary>
    /// Selects the enemy under the cursor when it is inside a weapon's reach.
    /// </summary>
    private bool TrySelectTargetFromHex(IUnit unit, Hex hex)
    {
        if (unit.Owner == Game.PhaseStepState?.ActivePlayer) return false;
        if (!IsHexInWeaponRange(hex.Coordinates)) return false;

        SelectedUnit = unit;
        return true;
    }

    private bool IsHexInWeaponRange(HexCoordinates coordinates)
    {
        return _weaponRanges.Values.Any(range => range.Contains(coordinates));
    }

    public void HandleFacingSelection(HexDirection direction)
    {
        if (!this.CanHumanPlayerAct()) return;
        if (CurrentStep != WeaponsAttackStep.WeaponsConfiguration 
            || Attacker == null
            || !_availableDirections.Contains(direction)) return;
        
        _viewModel.HideDirectionSelector();
        lock (_stateLock)
        {
            // Send command to server
            var command = new WeaponConfigurationCommand
            {
                GameOriginId = Game.Id,
                PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
                UnitId = Attacker.Id,
                Configuration = new WeaponConfiguration
                {
                    Type = WeaponConfigurationType.TorsoRotation,
                    Value = (int)direction
                }
            };

            Game.ConfigureUnitWeapons(command);

            // Return to action selection after rotation
            CurrentStep = WeaponsAttackStep.ActionSelection;
            _viewModel.NotifyStateChanged();
        }
    }

    public void HandleTorsoRotation(Guid unitId)
    {
        if (Attacker?.Id != unitId) return;
        ClearWeaponRangeHighlights();
        HighlightWeaponRanges();
    }

    private void ResetUnitSelection()
    {
        lock (_stateLock)
        {
            SelectedTarget = null;
            Attacker?.WeaponAttackState.ClearAllWeaponTargets();
            Attacker = null;
            _weaponRanges = new Dictionary<Weapon, HashSet<HexCoordinates>>();
            _weaponViewModels.Clear();
            CurrentStep = WeaponsAttackStep.SelectingUnit;
            _viewModel.NotifyStateChanged();
        }
    }

    private void CancelTorsoRotation()
    {
        if (CurrentStep != WeaponsAttackStep.WeaponsConfiguration) return;

        // Hide the direction selector
        _viewModel.HideDirectionSelector();

        // Return to an action selection step
        CurrentStep = WeaponsAttackStep.ActionSelection;
        _viewModel.NotifyStateChanged();
    }

    private void CancelTargetSelection()
    {
        lock (_stateLock)
        {
            if (CurrentStep != WeaponsAttackStep.TargetSelection) return;

            // Clear target and weapon selections
            Attacker?.WeaponAttackState.ClearAllWeaponTargets();
            SelectedTarget = null;
            _viewModel.IsWeaponSelectionVisible = false;

            // Return to an action selection step
            CurrentStep = WeaponsAttackStep.ActionSelection;
            _viewModel.NotifyStateChanged();
        }
    }

    public IEnumerable<StateAction> GetAvailableActions()
    {
        if (Attacker == null)
            return new List<StateAction>();

        var actions = new List<StateAction>();

        if (CurrentStep == WeaponsAttackStep.ActionSelection)
        {
            // Add skip attack action
            actions.Add(new StateAction(
                _viewModel.LocalizationService.GetString("Action_SkipAttack"),
                true,
                ConfirmWeaponSelections));

            if (Attacker.IsImmobile) return actions;
            
            // A unit with a pending Charge or DFA cannot fire, but it may still twist its torso:
            // charge alignment follows the feet, so a twist does not affect the attack
            // (Total Warfare, p. 148).
            // Add torso rotation action if available
            // TODO: once arms flip is implemented we should add an action per type instead of filtering
            var torsoRotationOption = Attacker
                .GetWeaponsConfigurationOptions()
                .FirstOrDefault(o => o.Type == WeaponConfigurationType.TorsoRotation);
            if (torsoRotationOption.AvailableDirections is { Count: > 0 })
            {
                actions.Add(new StateAction(
                    _viewModel.LocalizationService.GetString("Action_TurnTorso"),
                    true,
                    () => 
                    {
                        if (Attacker?.Position == null) return;
                        UpdateAvailableDirections();
                        _viewModel.ShowDirectionSelector(Attacker.Position.Coordinates, _availableDirections);
                        CurrentStep = WeaponsAttackStep.WeaponsConfiguration;
                        _viewModel.NotifyStateChanged();
                    }));
            }

            if (Attacker.CanFireWeapons)
            {
                // Add target selection action
                actions.Add(new StateAction(
                    _viewModel.LocalizationService.GetString("Action_SelectTarget"),
                    true,
                    () =>
                    {
                        CurrentStep = WeaponsAttackStep.TargetSelection;
                        _viewModel.NotifyStateChanged();
                    }));
            }

            
        }
        else if (CurrentStep == WeaponsAttackStep.TargetSelection)
        {
            // Add confirm weapon selections action
            actions.Add(new StateAction(
                Attacker?.WeaponAttackState.SelectedWeapons.Any() == true ? _viewModel.LocalizationService.GetString("Action_DeclareAttack") : _viewModel.LocalizationService.GetString("Action_SkipAttack"),
                true,
                ConfirmWeaponSelections));
        }

        return actions;
    }

    private void UpdateAvailableDirections()
    {
        _availableDirections.Clear();

        if (Attacker == null) return;

        var torsoRotationOption = Attacker
            .GetWeaponsConfigurationOptions()
            .FirstOrDefault(o => o.Type == WeaponConfigurationType.TorsoRotation);

        if (torsoRotationOption.AvailableDirections is not { Count: > 0 }) return;

        _availableDirections.AddRange(torsoRotationOption.AvailableDirections.OrderBy(d => (int)d));
    }

    private void HighlightWeaponRanges()
    {
        if (Attacker is not { Position: { } attackerPosition } attacker) return;

        var candidates = CollectWeaponCandidates(attacker, attackerPosition);
        _weaponRanges = _rangeAnalyzer.Analyze(
            attacker,
            candidates,
            _viewModel.Units,
            Game.BattleMap,
            out var losHighlights);

        // Apply all highlights in a single batch call so boundary outlines
        // are computed once over the full region per highlight type.
        var reachableHighlights = BuildReachableHighlights();
        var perHexHighlights = new Dictionary<HexCoordinates, IHexHighlightType>(
            losHighlights.Count + reachableHighlights.Count);
        foreach (var (coordinates, highlight) in losHighlights) perHexHighlights[coordinates] = highlight;
        foreach (var (coordinates, highlight) in reachableHighlights) perHexHighlights[coordinates] = highlight;
        _viewModel.HighlightRegions(perHexHighlights);
    }

    /// <summary>
    /// One reachable highlight per hex, labelled with the names of the weapons that can fire into it.
    /// </summary>
    private Dictionary<HexCoordinates, AttackReachableHighlight> BuildReachableHighlights()
    {
        var hexToWeaponNames = new Dictionary<HexCoordinates, HashSet<string>>();
        foreach (var (weapon, hexes) in _weaponRanges)
        {
            foreach (var hex in hexes)
            {
                if (!hexToWeaponNames.TryGetValue(hex, out var names))
                {
                    names = [];
                    hexToWeaponNames[hex] = names;
                }

                names.Add(weapon.Name);
            }
        }

        return hexToWeaponNames.ToDictionary(
            pair => pair.Key,
            pair => new AttackReachableHighlight(pair.Value.OrderBy(name => name).ToList()));
    }

    /// <summary>
    /// Collects the firing-arc hexes of every weapon that can fire, together with the mount level
    /// line of sight must be checked from. No line-of-sight checks happen here.
    /// </summary>
    private List<WeaponRangeAnalyzer.WeaponCandidate> CollectWeaponCandidates(
        IUnit attacker,
        HexPosition unitPosition)
    {
        var candidates = new List<WeaponRangeAnalyzer.WeaponCandidate>();

        foreach (var part in attacker.Parts.Values)
        {
            foreach (var weapon in part.GetComponents<Weapon>())
            {
                var candidate = CreateWeaponCandidate(weapon, unitPosition.Coordinates);
                if (candidate is not null) candidates.Add(candidate.Value);
            }
        }

        return candidates;
    }

    private WeaponRangeAnalyzer.WeaponCandidate? CreateWeaponCandidate(Weapon weapon, HexCoordinates origin)
    {
        // Skip weapons that cannot fire at all
        if (!weapon.IsAvailableForAttack()
            || weapon.Range == null
            || weapon.FirstMountPart == null)
        {
            return null;
        }

        var facing = weapon.FirstMountPart.Facing;
        if (facing == null) return null;

        var weaponHexes = new HashSet<HexCoordinates>();
        // For arms, we need to check both forward and side arcs
        foreach (var arc in weapon.GetFiringArcs())
        {
            var hexes = origin.GetHexesInFiringArc(facing.Value, arc, weapon.Range.LongRange);
            weaponHexes.UnionWith(hexes);
        }

        // Filter out hexes not on the map to skip unnecessary LOS calculations
        if (Game.BattleMap != null)
        {
            weaponHexes.RemoveWhere(hex => !Game.BattleMap.IsOnMap(hex));
        }

        return new WeaponRangeAnalyzer.WeaponCandidate(weapon, weaponHexes, weapon.FirstMountPart.Level);
    }

    private void ClearWeaponRangeHighlights()
    {
        // Clear highlights
        _viewModel.ClearHighlights();
        
        _weaponRanges = new Dictionary<Weapon, HashSet<HexCoordinates>>();
    }

    /// <summary>
    /// Gets all weapons that can fire at a given hex coordinate
    /// </summary>
    /// <param name="target">The target hex coordinates</param>
    /// <returns>List of weapons that can fire at the target</returns>
    public IReadOnlyList<Weapon> GetWeaponsInRange(HexCoordinates target)
    {
        return _weaponRanges
            .Where(kvp => kvp.Value.Contains(target))
            .Select(kvp => kvp.Key)
            .ToList();
    }
    
    public IUnit? Attacker
    {
        get;
        private set
        {
            field = value;
            _viewModel.NotifySelectedUnitChanged();
        }
    }

    public IUnit? SelectedTarget
    {
        get;
        private set
        {
            field = value;
            UpdateSelectedTargetViewModel();
            _viewModel.NotifySelectedUnitChanged();
        }
    }

    public IUnit? PrimaryTarget => Attacker?.WeaponAttackState.PrimaryTarget;

    private void CreateWeaponViewModels()
    {
        _weaponViewModels.Clear();
        if (Attacker == null) return;

        foreach (var weapon in Attacker.Parts.Values.SelectMany(p => p.GetComponents<Weapon>()))
        {
            var viewModel = new WeaponSelectionViewModel(
                weapon: weapon,
                isInRange: false,
                isSelected: false,
                isEnabled: false,
                target: null,
                onSelectionChanged: HandleWeaponSelection,
                onAimedShotRequest: HandleAimedShotRequest,
                localizationService: _viewModel.LocalizationService,
                toHitCalculator: Game.ToHitCalculator,
                remainingAmmoShots: Attacker.GetRemainingAmmoShots(weapon)
            );
            _weaponViewModels[weapon] = viewModel;
        }

        // Update the view model's collection
        UpdateViewModelWeaponItems();   
    }

    private void UpdateWeaponViewModels()
    {
        if (Attacker?.Position == null || SelectedTarget?.Position == null) return;

        var attacker = Attacker;
        var target = SelectedTarget;

        // Computed once per refresh and shared by every row.
        var attackerHasPartialCover = HasPartialCover(attacker, target);
        var isPrimaryTarget = IsPrimaryTarget(target);

        foreach (var (weapon, vm) in _weaponViewModels)
        {
            ProjectWeaponViewModel(weapon, vm, attacker, target, attackerHasPartialCover, isPrimaryTarget);
        }

        // Update the view model's collection
        UpdateViewModelWeaponItems();
    }

    /// <summary>
    /// Whether the target gives the attacker partial cover, using the reversed line of sight.
    /// </summary>
    private bool HasPartialCover(IUnit attacker, IUnit target)
    {
        var reversedLosResult = Game.BattleMap?.GetLineOfSight(
            target.Position!,
            attacker.Position!,
            target.Height,
            attacker.Height);

        return reversedLosResult != null && Game.RulesProvider.HasPartialCover(attacker, reversedLosResult);
    }

    private bool IsPrimaryTarget(IUnit target) => PrimaryTarget == null || PrimaryTarget == target;

    private void ProjectWeaponViewModel(
        Weapon weapon,
        WeaponSelectionViewModel vm,
        IUnit attacker,
        IUnit target,
        bool attackerHasPartialCover,
        bool isPrimaryTarget)
    {
        // Only process available weapons
        if (!weapon.IsAvailable)
        {
            vm.ModifiersBreakdown = null;
            return;
        }

        var assignedTarget = ApplyRangeAndSelection(weapon, vm, attacker, target);
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
        IUnit target)
    {
        var isInRange = IsWeaponInRange(weapon, target);
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
        if (location is not { } coveredLocation || !Game.RulesProvider.CanPartBeCovered(coveredLocation)) return;

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
        if (Game.BattleMap == null) return;

        // Calculate base breakdown without aimed shot, passing the primary target information
        var baseBreakdown = Game.ToHitCalculator.GetModifierBreakdown(
            attacker,
            target,
            weapon,
            Game.BattleMap,
            isPrimaryTarget,
            vm.AimedShotTarget);

        vm.ModifiersBreakdown = baseBreakdown;

        if (!vm.IsAimedShotAvailable) return;

        // Add aimed shot modifiers to the existing breakdown
        vm.AimedHeadModifiersBreakdown = Game.ToHitCalculator.AddAimedShotModifier(baseBreakdown, PartLocation.Head);
        vm.AimedOtherModifiersBreakdown = Game.ToHitCalculator.AddAimedShotModifier(baseBreakdown, PartLocation.CenterTorso);
    }

    // Helper method to update the view model's weapon items collection
    private void UpdateViewModelWeaponItems()
    {
        // Clear the view model's collection and add all items from our local collection
        _viewModel.WeaponSelectionItems.Clear();
        foreach (var kvp in _weaponViewModels)
        {
            _viewModel.WeaponSelectionItems.Add(kvp.Value);
        }
    }

    public IEnumerable<WeaponSelectionViewModel> WeaponSelectionItems => _weaponViewModels.Values;

    private bool IsWeaponInRange(Weapon weapon, HexCoordinates targetCoords)
    {
        return _weaponRanges.TryGetValue(weapon, out var range) && 
               range.Contains(targetCoords);
    }

    /// <summary>
    /// Whether the weapon can fire at the selected unit. The hex highlight is an "any occupant"
    /// result, so a co-located grounded unit must not inherit the airborne unit's line of sight:
    /// once a target is selected, use that unit's own LOS result.
    /// </summary>
    private bool IsWeaponInRange(Weapon weapon, IUnit target)
    {
        if (target.Position == null || !IsWeaponInRange(weapon, target.Position.Coordinates))
            return false;

        if (Game.BattleMap == null || Attacker?.Position == null) return true;

        LineOfSightResult lineOfSight = Game.BattleMap.GetLineOfSight(
            Attacker.Position,
            target.Position,
            weapon.FirstMountPart?.Level ?? 0,
            target.Height);
        return lineOfSight.HasLineOfSight;
    }

    private void HandleWeaponSelection(Weapon weapon, bool selected)
    {
        if (SelectedTarget == null || Attacker == null)
            return;

        if (!selected)
        {
            Attacker.WeaponAttackState.RemoveWeaponTarget(weapon, Attacker);
        }
        else
        {
            Attacker.WeaponAttackState.SetWeaponTarget(weapon, SelectedTarget, Attacker);
        }

        UpdateWeaponViewModels();
        UpdateSelectedTargetViewModel();
        _viewModel.NotifyStateChanged();
    }

    private void HandleAimedShotRequest(WeaponSelectionViewModel weaponVm)
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

    private void HandleSetPrimaryTarget(IUnit target)
    {
        if (Attacker == null) return;

        Attacker.WeaponAttackState.SetPrimaryTarget(target);
        UpdateSelectedTargetViewModel();
        UpdateWeaponViewModels();
        _viewModel.NotifyStateChanged();
    }

    private void UpdateSelectedTargetViewModel()
    {
        if (SelectedTarget == null || Attacker == null)
        {
            _viewModel.SelectedTarget = null;
            return;
        }

        // Only show target info if there are weapons assigned to this target
        var hasWeaponsForTarget = Attacker.WeaponAttackState.WeaponTargets.Values.Contains(SelectedTarget);

        var isPrimary = SelectedTarget == PrimaryTarget;

        // Update or create the view model for the selected target
        if (_viewModel.SelectedTarget?.Target == SelectedTarget)
        {
            _viewModel.SelectedTarget.IsPrimary = isPrimary;
            _viewModel.SelectedTarget.HasWeaponsForTarget = hasWeaponsForTarget;
        }
        else
        {
            _viewModel.SelectedTarget = new TargetSelectionViewModel(SelectedTarget, isPrimary, hasWeaponsForTarget, HandleSetPrimaryTarget);
        }
    }

    // DeterminePrimaryTarget method removed - now handled in UnitWeaponAttackState

    public void ConfirmWeaponSelections()
    {
        if (Attacker == null)
            return;
        
        // Create a weapon target data list
        var weaponTargetsData = new List<WeaponTargetData>();
        
        // Only process weapon targets if there are any (otherwise this is a Skip Attack)
        if (Attacker.WeaponAttackState.SelectedWeapons.Any())
        {
            foreach (var (weapon, target) in Attacker.WeaponAttackState.WeaponTargets)
            {
                var isPrimaryTarget = target == PrimaryTarget;

                // Get aimed shot target from weapon view model using O(1) dictionary lookup
                var aimedShotTarget = _weaponViewModels.TryGetValue(weapon, out var weaponVm)
                    ? weaponVm.AimedShotTarget
                    : null;

                weaponTargetsData.Add(new WeaponTargetData
                {
                    Weapon = weapon.ToData(),
                    TargetId = target.Id,
                    IsPrimaryTarget = isPrimaryTarget,
                    AimedShotTarget = aimedShotTarget
                });
            }
        }
        
        // Create and send the command
        var command = new WeaponAttackDeclarationCommand
        {
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = Attacker.Id,
            WeaponTargets = weaponTargetsData
        };
        
        Game.DeclareWeaponAttack(command);
        
        // Reset state after sending command
        ClearWeaponRangeHighlights();
        Attacker?.WeaponAttackState.ClearAllWeaponTargets();
        SelectedTarget = null;
        Attacker = null;
        _viewModel.IsWeaponSelectionVisible = false;

        // Clear the weapon view models
        _weaponViewModels.Clear();
        _viewModel.WeaponSelectionItems.Clear();

        CurrentStep = WeaponsAttackStep.SelectingUnit;
        _viewModel.NotifyStateChanged();
    }

}
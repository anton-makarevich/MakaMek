using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.UiStates;

public class WeaponsAttackState : IUiState
{
    private readonly BattleMapViewModel _viewModel;
    private readonly List<HexDirection> _availableDirections = [];
    private readonly Dictionary<Weapon, HashSet<HexCoordinates>> _weaponRanges = new();
    private readonly Dictionary<Weapon, WeaponSelectionViewModel> _weaponViewModels = new();
    private readonly Lock _stateLock = new();

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

    public bool CanExecutePlayerAction => CurrentStep is WeaponsAttackStep.ActionSelection or WeaponsAttackStep.TargetSelection;

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

    public WeaponsAttackState(BattleMapViewModel viewModel)
    {
        Game = viewModel.Game! ?? throw new InvalidOperationException("Game is not client game");
        _viewModel = viewModel;
        if (Game.PhaseStepState?.ActivePlayer == null)
        {
            throw new InvalidOperationException("Active player is null"); 
        }
    }

    public void HandleUnitSelection(IUnit? unit)
    {
        lock (_stateLock)
        {
            if (!this.CanHumanPlayerAct()) return;
            if (unit == null) return;
            if (unit.IsDestroyed) return;

            if (CurrentStep is WeaponsAttackStep.SelectingUnit or WeaponsAttackStep.ActionSelection)
            {
                if (unit.HasDeclaredWeaponAttack) return;

                Attacker = unit;
                CreateWeaponViewModels();
                CurrentStep = WeaponsAttackStep.ActionSelection;

                if (unit.CanFireWeapons)
                {
                    // Highlight weapon ranges for the newly selected unit
                    HighlightWeaponRanges();
                }
            }

            if (CurrentStep == WeaponsAttackStep.TargetSelection)
            {
                SelectedTarget = unit;
                UpdateWeaponViewModels();
                _viewModel.IsWeaponSelectionVisible = true;
            }

            _viewModel.NotifyStateChanged();
        }
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
        var unit = _viewModel.Units.FirstOrDefault(u => u.Position?.Coordinates == hex.Coordinates);
        if (unit == null) return;
        lock (_stateLock)
        {
            if (CurrentStep is WeaponsAttackStep.SelectingUnit or WeaponsAttackStep.ActionSelection)
            {
                if (unit.Owner != Game.PhaseStepState?.ActivePlayer || unit.HasDeclaredWeaponAttack)
                    return;

                if (Attacker is not null)
                {
                    ClearWeaponRangeHighlights();
                    ResetUnitSelection();
                }

                _viewModel.SelectedUnit = unit;
                return;
            }

            if (CurrentStep == WeaponsAttackStep.TargetSelection)
            {
                if (unit.Owner == Game.PhaseStepState?.ActivePlayer) return;
                if (!IsHexInWeaponRange(hex.Coordinates)) return;

                _viewModel.SelectedUnit = null;
                _viewModel.SelectedUnit = unit;
            }

            _viewModel.NotifyStateChanged();
        }
    }

    private bool IsHexInWeaponRange(HexCoordinates coordinates)
    {
        return _weaponRanges.Values.Any(range => range.Contains(coordinates));
    }

    public void HandleFacingSelection(HexDirection direction)
    {
        if (!this.CanHumanPlayerAct()) return;
        if (CurrentStep != WeaponsAttackStep.WeaponsConfiguration 
            || Attacker is not Mech mech 
            || !_availableDirections.Contains(direction)) return;
        
        _viewModel.HideDirectionSelector();
        lock (_stateLock)
        {
            // Send command to server
            var command = new WeaponConfigurationCommand
            {
                GameOriginId = Game.Id,
                PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
                UnitId = mech.Id,
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
            _weaponRanges.Clear();
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
            
            // Add torso rotation action if available
            if (Attacker is Mech { CanRotateTorso: true } mech)
            {
                actions.Add(new StateAction(
                    _viewModel.LocalizationService.GetString("Action_TurnTorso"),
                    true,
                    () => 
                    {
                        UpdateAvailableDirections();
                        _viewModel.ShowDirectionSelector(mech.Position!.Coordinates, _availableDirections);
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
        if (Attacker is not Mech mech || mech.Position == null) return;
        
        var currentFacing = (int)mech.Position.Facing;
        _availableDirections.Clear();

        // Add available directions based on PossibleTorsoRotation
        for (var i = 0; i < 6; i++)
        {
            var clockwiseSteps = (i - currentFacing + 6) % 6;
            var counterClockwiseSteps = (currentFacing - i + 6) % 6;
            var steps = Math.Min(clockwiseSteps, counterClockwiseSteps);

            if (steps <= mech.PossibleTorsoRotation && steps > 0)
            {
                _availableDirections.Add((HexDirection)i);
            }
        }
    }

    private void HighlightWeaponRanges()
    {
        if (Attacker?.Position == null) return;

        var reachableHexes = new HashSet<HexCoordinates>();
        var unitPosition = Attacker.Position;
        _weaponRanges.Clear();

        foreach (var part in Attacker.Parts.Values)
        {
            var weapons = part.GetComponents<Weapon>();
            foreach (var weapon in weapons)
            {
                var maxRange = weapon.LongRange;
                var facing = weapon.FirstMountPart?.Facing;
                if (facing == null)
                {
                    continue;
                }

                var weaponHexes = new HashSet<HexCoordinates>();
                // For arms, we need to check both forward and side arcs
                var arcs = weapon.GetFiringArcs();
                
                foreach (var arc in arcs)
                {
                    var hexes = unitPosition.Coordinates.GetHexesInFiringArc(facing.Value, arc, maxRange);
                    weaponHexes.UnionWith(hexes);
                }

                // Filter out hexes without the line of sight
                if (Game.BattleMap != null)
                {
                    weaponHexes.RemoveWhere(h => !Game.BattleMap.HasLineOfSight(unitPosition.Coordinates, h));
                }

                _weaponRanges[weapon] = weaponHexes;
                reachableHexes.UnionWith(weaponHexes);
            }
        }

        // Highlight the hexes
        _viewModel.HighlightHexes(reachableHexes.ToList(), true);
    }

    private void ClearWeaponRangeHighlights()
    {
        if (Attacker?.Position == null) return;

        // Get all hexes in maximum weapon range and unhighlight them
        var weapons = Attacker.Parts.Values
            .SelectMany(p => p.GetComponents<Weapon>())
            .ToList();
            
        IEnumerable<HexCoordinates> allPossibleHexes;
        // If there are no weapons, just clear all
        if (weapons.Count == 0)
        {
            allPossibleHexes = Game.BattleMap?.GetHexes()
                .Where(h=>h.IsHighlighted)
                .Select(h=>h.Coordinates)??[];
        }
        else
        {

            var maxRange = weapons.Max(w => w.LongRange);

            allPossibleHexes = Attacker.Position.Coordinates
                .GetCoordinatesInRange(maxRange);
        }

        _weaponRanges.Clear();
        _viewModel.HighlightHexes(allPossibleHexes.ToList(), false);
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
    
    public IUnit? Attacker { get; private set; }

    public IUnit? SelectedTarget
    {
        get;
        private set
        {
            field = value;
            _viewModel.SelectedUnit = value;
            UpdateSelectedTargetViewModel();
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
                _viewModel.ShowAimedShotLocationSelector,
                _viewModel.HideAimedShotLocationSelector,
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
        if (Attacker == null || SelectedTarget?.Position == null) return;

        var targetCoords = SelectedTarget.Position.Coordinates;
        foreach (var (weapon, vm) in _weaponViewModels)
        {
            // Only process available weapons
            if (!weapon.IsAvailable)
            {
                vm.ModifiersBreakdown = null;
                continue;
            }
            var isInRange = IsWeaponInRange(weapon, targetCoords);
            var target = Attacker?.WeaponAttackState.WeaponTargets.GetValueOrDefault(weapon);
            var isSelected = Attacker?.WeaponAttackState.IsWeaponAssigned(weapon, target) == true;
            vm.IsInRange = isInRange;
            vm.IsSelected = isSelected;
            var isWeaponAvailable = Attacker != null && weapon.IsAvailableForAttack();
            vm.IsEnabled = (Attacker?.WeaponAttackState.IsWeaponAssigned(weapon) != true || target == SelectedTarget) 
                                                && isInRange && isWeaponAvailable;
            vm.Target = target;

            vm.ModifiersBreakdown = null;
            vm.AimedHeadModifiersBreakdown = null;
            vm.AimedOtherModifiersBreakdown = null;

            // Set modifiers breakdown when in range
            if (!isInRange) continue;
            // Check if this target is the primary target
            var isPrimaryTarget = SelectedTarget == PrimaryTarget || PrimaryTarget==null;

            // Get modifiers breakdown, passing the primary target information
            if (Game.BattleMap == null || Attacker == null) continue;
            // Calculate base breakdown without aimed shot
            var baseBreakdown = Game.ToHitCalculator.GetModifierBreakdown(
                Attacker, SelectedTarget, weapon, Game.BattleMap, isPrimaryTarget, vm.AimedShotTarget);

            // Set the appropriate breakdown based on the current aimed shot target
            vm.ModifiersBreakdown = baseBreakdown;

            if (!vm.IsAimedShotAvailable) continue;
            // Add aimed shot modifiers to the existing breakdown
            vm.AimedHeadModifiersBreakdown = Game.ToHitCalculator.AddAimedShotModifier(baseBreakdown, PartLocation.Head);
            vm.AimedOtherModifiersBreakdown = Game.ToHitCalculator.AddAimedShotModifier(baseBreakdown, PartLocation.CenterTorso);
        }

        // Update the view model's collection
        UpdateViewModelWeaponItems();
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
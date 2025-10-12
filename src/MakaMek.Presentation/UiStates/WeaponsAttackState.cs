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
    // Removed _weaponTargets - now using Unit.WeaponAttackState
    private readonly Dictionary<Weapon, WeaponSelectionViewModel> _weaponViewModels = new();
    private readonly ClientGame _game;
    private readonly Lock _stateLock = new();

    public WeaponsAttackStep CurrentStep { get; private set; } = WeaponsAttackStep.SelectingUnit;

    public string ActionLabel => CurrentStep switch
    {
        WeaponsAttackStep.SelectingUnit => _viewModel.LocalizationService.GetString("Action_SelectUnitToFire"),
        WeaponsAttackStep.ActionSelection => _viewModel.LocalizationService.GetString("Action_SelectAction"),
        WeaponsAttackStep.WeaponsConfiguration => _viewModel.LocalizationService.GetString("Action_ConfigureWeapons"),
        WeaponsAttackStep.TargetSelection => _viewModel.LocalizationService.GetString("Action_SelectTarget"),
        _ => string.Empty
    };

    public bool IsActionRequired => _viewModel.Game is {CanActivePlayerAct:true};

    public bool CanExecutePlayerAction => CurrentStep == WeaponsAttackStep.ActionSelection || CurrentStep == WeaponsAttackStep.TargetSelection;

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
        if (_game is { CanActivePlayerAct: false }) return;
        if (CurrentStep == WeaponsAttackStep.ActionSelection || CurrentStep == WeaponsAttackStep.TargetSelection)
        {
            ConfirmWeaponSelections();
        }
    }

    public WeaponsAttackState(BattleMapViewModel viewModel)
    {
        _game = viewModel.Game! ?? throw new InvalidOperationException("Game is not client game");
        _viewModel = viewModel;
        if (_game.ActivePlayer == null)
        {
            throw new InvalidOperationException("Active player is null"); 
        }
    }

    public void HandleUnitSelection(Unit? unit)
    {
        lock (_stateLock)
        {
            if (_game is { CanActivePlayerAct: false }) return;
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
        // Handle cancellation when direction selector is shown (torso rotation step)
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
        var unit = _viewModel.Units.FirstOrDefault(u => u.Position?.Coordinates == hex.Coordinates);
        if (unit == null) return;
        lock (_stateLock)
        {
            if (CurrentStep is WeaponsAttackStep.SelectingUnit or WeaponsAttackStep.ActionSelection)
            {
                if (unit.Owner != _game.ActivePlayer || unit.HasDeclaredWeaponAttack)
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
                if (unit.Owner == _game.ActivePlayer) return;
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
        if (_game is { CanActivePlayerAct: false }) return;
        if (CurrentStep != WeaponsAttackStep.WeaponsConfiguration 
            || Attacker is not Mech mech 
            || !_availableDirections.Contains(direction)) return;
        
        _viewModel.HideDirectionSelector();
        lock (_stateLock)
        {
            // Send command to server
            var command = new WeaponConfigurationCommand
            {
                GameOriginId = _game.Id,
                PlayerId = _game.ActivePlayer!.Id,
                UnitId = mech.Id,
                Configuration = new WeaponConfiguration
                {
                    Type = WeaponConfigurationType.TorsoRotation,
                    Value = (int)direction
                }
            };

            _game.ConfigureUnitWeapons(command);

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
            _viewModel.SelectedUnit = null;
            CurrentStep = WeaponsAttackStep.SelectingUnit;
            _viewModel.NotifyStateChanged();
        }
    }

    private void CancelTorsoRotation()
    {
        if (CurrentStep != WeaponsAttackStep.WeaponsConfiguration) return;

        // Hide the direction selector
        _viewModel.HideDirectionSelector();

        // Return to action selection step
        CurrentStep = WeaponsAttackStep.ActionSelection;
        _viewModel.NotifyStateChanged();
    }

    private void CancelTargetSelection()
    {
        lock (_stateLock)
        {
            if (CurrentStep != WeaponsAttackStep.TargetSelection) return;

            // Clear target and weapon selections
            SelectedTarget = null;
            _viewModel.SelectedUnit = null;
            _viewModel.IsWeaponSelectionVisible = false;

            // Return to action selection step
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
                var facing = part.Location switch
                {
                    PartLocation.LeftLeg or PartLocation.RightLeg => unitPosition.Facing,
                    _ => Attacker is Mech mech ? mech.TorsoDirection : unitPosition.Facing
                };
                if (facing == null)
                {
                    continue;
                }

                var weaponHexes = new HashSet<HexCoordinates>();
                // For arms, we need to check both forward and side arcs
                if (part.Location is PartLocation.LeftArm or PartLocation.RightArm)
                {
                    var forwardHexes = unitPosition.Coordinates.GetHexesInFiringArc(facing.Value, FiringArc.Front, maxRange);
                    var sideArc = part.Location == PartLocation.LeftArm ? FiringArc.Left : FiringArc.Right;
                    var sideHexes = unitPosition.Coordinates.GetHexesInFiringArc(facing.Value, sideArc, maxRange);
                    
                    weaponHexes.UnionWith(forwardHexes);
                    weaponHexes.UnionWith(sideHexes);
                }
                else
                {
                    // For torso, legs, and head weapons - only forward arc
                    var hexes = unitPosition.Coordinates.GetHexesInFiringArc(facing.Value, FiringArc.Front, maxRange);
                    weaponHexes.UnionWith(hexes);
                }

                // Filter out hexes without line of sight
                if (_game.BattleMap != null)
                {
                    weaponHexes.RemoveWhere(h => !_game.BattleMap.HasLineOfSight(unitPosition.Coordinates, h));
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
            allPossibleHexes = _game.BattleMap?.GetHexes()
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
    
    public Unit? Attacker { get; private set; }

    public Unit? SelectedTarget { get; private set; }

    public Unit? PrimaryTarget => Attacker?.WeaponAttackState.PrimaryTarget;

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
                toHitCalculator: _game.ToHitCalculator,
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
        foreach (var kvp in _weaponViewModels)
        {
            var weapon = kvp.Key;
            var vm = kvp.Value;

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
            if (_game.BattleMap == null || Attacker == null) continue;
            // Calculate base breakdown without aimed shot
            var baseBreakdown = _game.ToHitCalculator.GetModifierBreakdown(
                Attacker, SelectedTarget, weapon, _game.BattleMap, isPrimaryTarget, vm.AimedShotTarget);

            // Set the appropriate breakdown based on current aimed shot target
            vm.ModifiersBreakdown = baseBreakdown;

            if (!vm.IsAimedShotAvailable) continue;
            // Use optimized method to add aimed shot modifiers to existing breakdown
            vm.AimedHeadModifiersBreakdown = _game.ToHitCalculator.AddAimedShotModifier(baseBreakdown, PartLocation.Head);
            vm.AimedOtherModifiersBreakdown = _game.ToHitCalculator.AddAimedShotModifier(baseBreakdown, PartLocation.CenterTorso);
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
        _viewModel.NotifyStateChanged();
    }

    // DeterminePrimaryTarget method removed - now handled in UnitWeaponAttackState

    public void ConfirmWeaponSelections()
    {
        if (Attacker == null)
            return;
        
        // Create weapon target data list
        var weaponTargetsData = new List<WeaponTargetData>();
        
        // Only process weapon targets if there are any (otherwise this is a Skip Attack)
        if (Attacker.WeaponAttackState.SelectedWeapons.Any())
        {
            foreach (var weaponTarget in Attacker.WeaponAttackState.WeaponTargets)
            {
                var weapon = weaponTarget.Key;
                var target = weaponTarget.Value;
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
            GameOriginId = _game.Id,
            PlayerId = _game.ActivePlayer!.Id,
            AttackerId = Attacker.Id,
            WeaponTargets = weaponTargetsData
        };
        
        _game.DeclareWeaponAttack(command);
        
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
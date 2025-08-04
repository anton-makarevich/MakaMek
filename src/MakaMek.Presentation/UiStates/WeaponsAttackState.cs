using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Units;
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
    private readonly List<HexDirection> _availableDirections = new();
    private readonly Dictionary<Weapon, HashSet<HexCoordinates>> _weaponRanges = new();
    private readonly Dictionary<Weapon, Unit> _weaponTargets = new();
    private readonly List<WeaponSelectionViewModel> _weaponViewModels = new();
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
                WeaponsAttackStep.TargetSelection => _weaponTargets.Count > 0
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

                // Highlight weapon ranges for the newly selected unit
                HighlightWeaponRanges();
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
            Attacker = null;
            SelectedTarget = null;
            PrimaryTarget = null;
            _weaponTargets.Clear();
            _weaponRanges.Clear();
            _weaponViewModels.Clear();
            _viewModel.SelectedUnit = null;
            CurrentStep = WeaponsAttackStep.SelectingUnit;
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

            // Add skip attack action
            actions.Add(new StateAction(
                _viewModel.LocalizationService.GetString("Action_SkipAttack"),
                true,
                ConfirmWeaponSelections));
        }
        else if (CurrentStep == WeaponsAttackStep.TargetSelection)
        {
            // Add confirm weapon selections action
            actions.Add(new StateAction(
                _weaponTargets.Count > 0 ? _viewModel.LocalizationService.GetString("Action_DeclareAttack") : _viewModel.LocalizationService.GetString("Action_SkipAttack"),
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

        foreach (var part in Attacker.Parts)
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
                    var forwardHexes = unitPosition.Coordinates.GetHexesInFiringArc(facing.Value, FiringArc.Forward, maxRange);
                    var sideArc = part.Location == PartLocation.LeftArm ? FiringArc.Left : FiringArc.Right;
                    var sideHexes = unitPosition.Coordinates.GetHexesInFiringArc(facing.Value, sideArc, maxRange);
                    
                    weaponHexes.UnionWith(forwardHexes);
                    weaponHexes.UnionWith(sideHexes);
                }
                else
                {
                    // For torso, legs, and head weapons - only forward arc
                    var hexes = unitPosition.Coordinates.GetHexesInFiringArc(facing.Value, FiringArc.Forward, maxRange);
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
        var weapons = Attacker.Parts
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

    public Unit? PrimaryTarget { get; private set; }

    private void CreateWeaponViewModels()
    {
        _weaponViewModels.Clear();
        if (Attacker == null) return;

        _weaponViewModels.AddRange(Attacker.Parts
            .SelectMany(p => p.GetComponents<Weapon>())
            .Select(w => new WeaponSelectionViewModel(
                weapon: w,
                isInRange: false,
                isSelected: false,
                isEnabled: false,
                target: null,
                onSelectionChanged: HandleWeaponSelection,
                localizationService: _viewModel.LocalizationService,
                remainingAmmoShots: Attacker.GetRemainingAmmoShots(w)
            )));
            
        // Update the view model's collection
        UpdateViewModelWeaponItems();
    }

    private void UpdateWeaponViewModels()
    {
        if (Attacker == null || SelectedTarget?.Position == null) return;

        var targetCoords = SelectedTarget.Position.Coordinates;
        foreach (var vm in _weaponViewModels)
        {
            // Only process available weapons
            if (!vm.Weapon.IsAvailable)
            {
                vm.ModifiersBreakdown = null;
                continue;
            }
            var isInRange = IsWeaponInRange(vm.Weapon, targetCoords);
            var target = _weaponTargets.GetValueOrDefault(vm.Weapon);
            var isSelected = _weaponTargets.ContainsKey(vm.Weapon) && _weaponTargets[vm.Weapon] == target;
            vm.IsInRange = isInRange;
            vm.IsSelected = isSelected;
            vm.IsEnabled = (!_weaponTargets.ContainsKey(vm.Weapon) || _weaponTargets[vm.Weapon] == SelectedTarget) && isInRange;
            vm.Target = target;
            
            // Set modifiers breakdown when in range
            if (isInRange)
            {
                // Check if this target is the primary target
                var isPrimaryTarget = SelectedTarget == PrimaryTarget || PrimaryTarget==null;
                
                // Get modifiers breakdown, passing the primary target information
                vm.ModifiersBreakdown = (_game.BattleMap!=null)
                    ? _game.ToHitCalculator.GetModifierBreakdown(
                        Attacker, SelectedTarget, vm.Weapon, _game.BattleMap, isPrimaryTarget)
                    : null;
            }
            else
            {
                // Weapon is not in range
                vm.ModifiersBreakdown = null;
            }
        }
        
        // Update the view model's collection
        UpdateViewModelWeaponItems();
    }

    // Helper method to update the view model's weapon items collection
    private void UpdateViewModelWeaponItems()
    {
        // Clear the view model's collection and add all items from our local collection
        _viewModel.WeaponSelectionItems.Clear();
        foreach (var item in _weaponViewModels)
        {
            _viewModel.WeaponSelectionItems.Add(item);
        }
    }

    public IEnumerable<WeaponSelectionViewModel> WeaponSelectionItems => _weaponViewModels;

    private bool IsWeaponInRange(Weapon weapon, HexCoordinates targetCoords)
    {
        return _weaponRanges.TryGetValue(weapon, out var range) && 
               range.Contains(targetCoords);
    }

    private void HandleWeaponSelection(Weapon weapon, bool selected)
    {
        if (SelectedTarget == null)
            return;
        if (!selected) 
        {
            _weaponTargets.Remove(weapon);
        }
        else
        {
            _weaponTargets[weapon] = SelectedTarget;
        }
        
        // Determine the primary target whenever weapon selections change
        PrimaryTarget = DeterminePrimaryTarget();
        
        UpdateWeaponViewModels();
        _viewModel.NotifyStateChanged();
    }

    private Unit? DeterminePrimaryTarget()
    {
        if (_weaponTargets.Count == 0)
        {
            return null;
        }
        
        // Get all unique targets
        var targets = _weaponTargets.Values.Distinct().ToList();
        
        // If only one target, it's the primary
        if (targets.Count == 1)
        {
            return targets[0];
        }
        
        // Check for targets in the forward arc
        if (Attacker?.Position == null) return targets[0];
        
        var attackerPosition = Attacker.Position;
        var facing = Attacker is Mech mech ? mech.TorsoDirection : attackerPosition.Facing;
        
        if (facing == null) return targets[0];
        
        // Find targets in the forward arc
        var targetsInForwardArc = targets
            .Where(t => t.Position != null && 
                       attackerPosition.Coordinates.IsInFiringArc(
                           t.Position.Coordinates, 
                           facing.Value, 
                           FiringArc.Forward))
            .ToList();

        // If there are targets in forward arc, pick the first one
        return targetsInForwardArc.Count != 0 ? targetsInForwardArc[0] :
            // Otherwise, pick the first target
            targets[0];
    }

    public void ConfirmWeaponSelections()
    {
        if (Attacker == null)
            return;
        
        // Create weapon target data list
        var weaponTargetsData = new List<WeaponTargetData>();
        
        // Only process weapon targets if there are any (otherwise this is a Skip Attack)
        if (_weaponTargets.Count > 0)
        {
            foreach (var weaponTarget in _weaponTargets)
            {
                var weapon = weaponTarget.Key;
                var target = weaponTarget.Value;
                var isPrimaryTarget = target == PrimaryTarget;
                
                weaponTargetsData.Add(new WeaponTargetData
                {
                    Weapon = new WeaponData
                    {
                        Name = weapon.Name,
                        Location = weapon.MountedOn?.Location ?? throw new Exception("Weapon is not mounted"),
                        Slots = weapon.MountedAtSlots
                    },
                    TargetId = target.Id,
                    IsPrimaryTarget = isPrimaryTarget
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
        _weaponTargets.Clear();
        PrimaryTarget = null;
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
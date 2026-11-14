using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands.Client.Builders;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Models.Highlights;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.UiStates;

public class MovementState : IUiState, IPhysicalAttackDeclarationState
{
    private static readonly IReadOnlyList<PhysicalAttackOptionViewModel> NoAttackOptions = [];

    private readonly BattleMapViewModel _viewModel;
    private readonly MoveUnitCommandBuilder _builder;
    private IUnit? _selectedUnit;
    private ReachableArea? _reachabilityData;
    private readonly IReadOnlySet<HexCoordinates> _prohibitedHexes;
    private readonly IReadOnlySet<HexCoordinates> _friendlyUnitsCoordinates;
    private int _movementPoints;
    private Dictionary<HexDirection, MovementPath> _possibleDirections = [];
    private readonly Lock _stateLock = new();
    private bool _isPostStandupMovement;
    private MovementPath? _selectedPath;
    private Guid? _deferredMovementUnitId;

    /// <summary>
    /// Legal movement-attack targets for the current path state, each with its cheapest validated
    /// path. Recomputed whenever reachability is recomputed; replaced as a whole, never mutated.
    /// </summary>
    private IReadOnlyDictionary<IUnit, MovementAttackCandidate> _movementAttackCandidates =
        new Dictionary<IUnit, MovementAttackCandidate>();

    private IUnit? _pendingAttackTarget;
    private MovementPath? _pendingAttackPath;
    private PhysicalAttackDeclarationData? _pendingAttackDeclaration;
    private IReadOnlyList<PhysicalAttackOptionViewModel> _pendingAttackOptions = NoAttackOptions;

    private IMovementStep _step;

    public IClientGame? Game => _viewModel.Game;

    public bool CanSelectUnit(IUnit? unit)
    {
        if (_deferredMovementUnitId is not { } lockedId) return true;
        if (unit == null) return true;
        return unit.Id == lockedId;
    }

    public MovementState(BattleMapViewModel viewModel)
    {
        _viewModel = viewModel;
        if (_viewModel.Game == null)
        {
            throw new InvalidOperationException("Game is null");
        }

        if (_viewModel.Game.PhaseStepState?.ActivePlayer == null)
        {
            throw new InvalidOperationException("Active player is null");
        }
        _builder = new MoveUnitCommandBuilder(_viewModel.Game.Id, _viewModel.Game.PhaseStepState.Value.ActivePlayer.Id);

        // Get hexes with enemy units - these will be excluded from pathfinding
        _prohibitedHexes = _viewModel.Units
            .Where(u=>u.Owner?.Id != _viewModel.Game.PhaseStepState?.ActivePlayer.Id && u.Position!=null)
            .Select(u => u.Position!.Coordinates)
            .ToHashSet();
        
        _friendlyUnitsCoordinates = _viewModel.Units
            .Where(u=>u.Owner?.Id == _viewModel.Game.PhaseStepState?.ActivePlayer.Id && u.Position!=null)
            .Select(u => u.Position!.Coordinates)
            .ToHashSet();

        _step = new SelectingUnitStep(this);
    }
    
    private void TransitionTo(IMovementStep step)
    {
        _step = step;
        _viewModel.NotifyStateChanged();
    }
    
    private void ClearHighlighting()
    {
        if (_reachabilityData != null)
            _viewModel.RemoveHighlight<MovementReachableHighlight>(_reachabilityData.Value.AllReachableCoordinates);
        _reachabilityData = null;
        ClearMovementAttackHighlights();
    }

    private void ClearMovementAttackHighlights()
    {
        if (_movementAttackCandidates.Count > 0)
        {
            var coordinates = _movementAttackCandidates.Keys
                .Where(unit => unit.Position != null)
                .Select(unit => unit.Position!.Coordinates)
                .ToHashSet();
            _viewModel.RemoveHighlight<AttackReachableHighlight>(coordinates);
        }

        _movementAttackCandidates = new Dictionary<IUnit, MovementAttackCandidate>();
    }

    private void HighlightReachableHexes()
    {
        if (_selectedPath?.MovementType == null) return;
        var movementType = _selectedPath.MovementType;

        ClearHighlighting();

        var remainingMp = GetRemainingMovementPoints();
        _movementPoints = remainingMp;

        var battleMap = _viewModel.Game?.BattleMap;
        if (_selectedUnit != null && battleMap != null)
        {
            var startPosition = _selectedPath.Destination;
            _reachabilityData = battleMap.GetReachableHexesForPosition(
                startPosition,
                remainingMp,
                _selectedUnit.CanMoveBackward(movementType),
                movementType,
                _prohibitedHexes,
                _friendlyUnitsCoordinates,
                _selectedUnit.Height,
                _selectedUnit.MaxLevelChangeForward,
                _selectedUnit.MaxLevelChangeBackward);

            _viewModel.HighlightCoordinates(_reachabilityData.Value.AllReachableCoordinates, new MovementReachableHighlight(movementType));

            UpdateMovementAttackCandidates(movementType, remainingMp);
        }

        _viewModel.NotifyStateChanged();
    }

    /// <summary>
    /// Finds every enemy Mech the selected unit can legally charge or DFA from the current path
    /// state, keeping the cheapest validated path per target, and highlights their hexes with the
    /// same attack highlight the weapon phase uses.
    /// </summary>
    private void UpdateMovementAttackCandidates(MovementType movementType, int remainingMp)
    {
        _movementAttackCandidates = FindMovementAttackCandidates(movementType, remainingMp);
        if (_movementAttackCandidates.Count == 0) return;

        var attackType = movementType.GetMovementAttackType();
        if (attackType is null) return;

        var coordinates = _movementAttackCandidates.Keys
            .Where(unit => unit.Position != null)
            .Select(unit => unit.Position!.Coordinates)
            .ToHashSet();
        if (coordinates.Count == 0) return;

        var label = GetString($"PhysicalAttackType_{attackType}");
        _viewModel.HighlightCoordinates(coordinates, new AttackReachableHighlight([label]));
    }

    private IReadOnlyDictionary<IUnit, MovementAttackCandidate> FindMovementAttackCandidates(
        MovementType movementType,
        int remainingMp)
    {
        var candidates = new Dictionary<IUnit, MovementAttackCandidate>();
        if (!TryCreateCandidateSearch(movementType, remainingMp, out var search)) return candidates;

        var allUnits = GetAllUnits();
        foreach (var target in FilterPotentialTargets(allUnits, search.ActivePlayerId))
        {
            var candidate = BuildMovementAttackCandidate(target, search, allUnits);
            if (candidate != null) candidates[target] = candidate;
        }

        return candidates;
    }

    /// <summary>
    /// Captures the fixed inputs of a candidate search, or returns <c>false</c> when the state is not
    /// ready to search (no selected unit or path, no map, no active player, or a non-attacking move).
    /// </summary>
    private bool TryCreateCandidateSearch(
        MovementType movementType,
        int remainingMp,
        out MovementAttackSearch search)
    {
        search = default;
        if (_selectedUnit is not { } unit
            || _selectedPath is not { } path
            || Game?.BattleMap is not { } map
            || Game.PhaseStepState?.ActivePlayer is not { } activePlayer
            || movementType.GetMovementAttackType() is not { } attackType)
        {
            return false;
        }

        search = new MovementAttackSearch(
            unit,
            path,
            map,
            activePlayer.Id,
            movementType,
            attackType,
            remainingMp,
            unit.CanMoveBackward(movementType));
        return true;
    }

    /// <summary>
    /// Live enemy Mechs that have finished acting this turn and are not already mid-attack.
    /// </summary>
    private static List<IUnit> FilterPotentialTargets(IReadOnlyList<IUnit> allUnits, Guid? activePlayerId) =>
        allUnits.Where(unit =>
                unit is Mech { IsDestroyed: false, Position: not null }
                && unit.Owner?.Id != activePlayerId
                && unit is { HasPendingMovementAttack: false }
                && (unit.HasMoved || unit.IsShutdown || unit.Pilot is { IsConscious: false }))
            .ToList();

    /// <summary>
    /// The cheapest continuation path from the current path state that reaches <paramref name="target"/>
    /// and passes the shared declaration validator, or <c>null</c> when no path qualifies.
    /// </summary>
    private MovementAttackCandidate? BuildMovementAttackCandidate(
        IUnit target,
        in MovementAttackSearch search,
        IReadOnlyList<IUnit> allUnits)
    {
        var paths = FindPathsToTarget(target, search);
        if (paths.Count == 0) return null;

        MovementAttackCandidate? cheapest = null;
        foreach (var path in paths.Values)
        {
            var validated = ValidateCandidatePath(target, path, search, allUnits);
            if (validated == null) continue;

            if (cheapest == null || path.TotalCost < cheapest.Path.TotalCost)
                cheapest = new MovementAttackCandidate(target, path, validated);
        }

        return cheapest;
    }

    /// <summary>
    /// All facings the unit can take to reach <paramref name="target"/> from the current path state.
    /// </summary>
    private IReadOnlyDictionary<HexDirection, MovementPath> FindPathsToTarget(
        IUnit target,
        in MovementAttackSearch search)
    {
        var targetCoordinates = target.Position!.Coordinates;
        var prohibited = _prohibitedHexes
            .Where(coordinates => coordinates != targetCoordinates)
            .ToHashSet();

        var reachability = search.Map.GetReachableHexesForPosition(
            search.StartPosition,
            search.RemainingMp,
            search.CanMoveBackward,
            search.MovementType,
            prohibited,
            _friendlyUnitsCoordinates,
            search.Unit.Height,
            search.Unit.MaxLevelChangeForward,
            search.Unit.MaxLevelChangeBackward);

        return search.Map.GetPathsToHexWithAllFacings(
            search.StartPosition,
            targetCoordinates,
            search.MovementType,
            search.RemainingMp,
            reachability,
            search.Unit.Height,
            search.Unit.MaxLevelChangeForward,
            search.Unit.MaxLevelChangeBackward,
            prohibited,
            targetSurface: target.Position.Surface,
            approachWaypointOffset: Game!.RulesProvider.GetDfaAirborneBaseOffset());
    }

    private PhysicalAttackDeclarationData? ValidateCandidatePath(
        IUnit target,
        MovementPath path,
        in MovementAttackSearch search,
        IReadOnlyList<IUnit> allUnits)
    {
        var fullPath = search.CurrentPath.Append(path);
        var declaration = new PhysicalAttackDeclarationData
        {
            AttackType = search.AttackType,
            AttackerLimbs = [],
            TargetId = target.Id
        };

        var validation = Game!.MovementAttackDeclarationValidator.Validate(
            search.Unit,
            fullPath.ToData(),
            search.MovementType,
            declaration,
            allUnits,
            search.Map);

        return !validation.IsValid || validation.Declaration is not { } validated ? null : validated;
    }

    private List<IUnit> GetAllUnits() =>
        Game?.Players.SelectMany(player => player.Units).ToList() ?? [];

    private string GetString(string key) => _viewModel.LocalizationService.GetString(key);

    private int GetRemainingMovementPoints()
    {
        if (_selectedPath == null || _selectedUnit == null) return 0;
        return Math.Max(0, _selectedUnit.GetMovementPoints(_selectedPath.MovementType) - _selectedPath.TotalCost);
    }

    public IUnit? SelectedUnit
    {
        get => _selectedUnit;
        private set
        {
            lock (_stateLock)
            {
                if (!this.CanHumanPlayerAct()) return;
                // A pending Charge/DFA declaration owns the unit until it is confirmed or cancelled.
                if (_pendingAttackTarget != null) return;
                if (value == null)
                {
                    ResetUnitSelection();
                    return;
                }
                
                if (!CanSelectUnit(value)) return;
                
                if (value.Status == UnitStatus.Destroyed) return;
                if (value.HasMoved && value.Id != _deferredMovementUnitId) return;

                _selectedUnit = value;
                _builder.SetUnit(value);
                _isPostStandupMovement = false;
                TransitionTo(new SelectingMovementTypeStep(this));
                _viewModel.NotifySelectedUnitChanged();
            }
        }
    }

    public void HandleUnitSelectionFromList(IUnit? unit)
    {
        if (unit != null && _selectedUnit != null && _selectedUnit != unit)
        {
            ResetUnitSelection();
        }

        SelectedUnit = unit;
    }

    public void HandleMovementTypeSelection(MovementType movementType)
    {
        lock (_stateLock)
        {
            _step.HandleMovementTypeSelection(movementType);
        }
    }

    public void HandleHexSelection(Hex hex)
    {
        if (_pendingAttackTarget != null)
        {
            lock (_stateLock)
            {
                HandlePendingMovementAttackHexSelection(hex);
            }
            return;
        }

        if (HandleUnitSelectionFromHex(hex)) return;
        lock (_stateLock)
        {
            _step.HandleHexSelection(hex);
        }
    }

    public void HandleFacingSelection(HexDirection direction)
    {
        lock (_stateLock)
        {
            _step.HandleFacingSelection(direction);
        }
    }

    private void ConfirmMovement()
    {
        if (CurrentMovementStep != MovementStep.ConfirmMovement) return;
        var direction = _viewModel.AvailableDirections?.FirstOrDefault();
        if (direction == null || !_possibleDirections.TryGetValue(direction.Value, out var path))
        {
            // A cancelled movement-attack declaration restores the confirmation step after the
            // direction selector is gone; the path built so far is the move to confirm.
            if (_selectedPath is not { TotalCost: > 0 } pathSoFar) return;
            _builder.SetMovementPath(pathSoFar);
            CompleteMovement();
            return;
        }

        if (_viewModel.MovementPath == null || _viewModel.MovementPath.Last().To != path.Destination)
        {
            if (_selectedUnit?.Position == null) return;
            path = MovementPath.CreateSingleSegmentPath(_selectedUnit.Position);
            _builder.SetMovementPath(path);
        }

        CompleteMovement();
    }

    private bool HandleUnitSelectionFromHex(Hex hex)
    {
        var unit = _viewModel.Units.FirstOrDefault(u => u.Position?.Coordinates == hex.Coordinates);
        if (unit == null) return false;
        
        if (unit == _selectedUnit)
        {
            if (CurrentMovementStep != MovementStep.SelectingUnit) return false;
            SelectedUnit = null;
            return true;
        }

        if (unit.Owner?.Id != _viewModel.Game?.PhaseStepState?.ActivePlayer.Id) return false;
        if (!CanSelectUnit(unit)) return false;
        
        SelectedUnit = null;
        SelectedUnit = unit;
        return true;
    }

    private void ResetUnitSelection()
    {
        if (_isPostStandupMovement) return;
        lock (_stateLock)
        {
            if (_selectedUnit == null) return;
            _selectedUnit = null;
            _builder.Reset();
            ClearPendingMovementAttack();
            _viewModel.HideMovementPath();
            _viewModel.HideDirectionSelector();
            _viewModel.HideSurfaceSelector();
            ClearHighlighting();
            TransitionTo(new SelectingUnitStep(this));
            _viewModel.NotifySelectedUnitChanged();
        }
    }

    private void HandleTargetHexSelection(Hex hex)
    {
        if (_selectedUnit?.Position == null
            || _selectedPath?.MovementType == null
            || Game == null
            || _reachabilityData == null) return;

        // An enemy hex is never reachable. If it can be legally charged or DFA'd it opens the
        // declaration; an ineligible enemy is ignored instead of cancelling the move.
        if (TryHandleEnemyHex(hex)) return;

        // Reset selection if clicked outside reachable hexes during target hex selection
        // BUT NOT if the unit is in post-standup movement state (must complete declared movement)
        if (!_reachabilityData.Value.IsHexReachable(hex.Coordinates))
        {
            ResetUnitSelection();
            return;
        }
        
        var availableSurfaces = _reachabilityData.Value
            .GetReachableSurfacesForCoordinate(hex.Coordinates)
            .DistinctBy(s => s.Surface)
            .ToList();

        // If only one surface, proceed directly
        if (availableSurfaces.Count == 1)
        {
            var surface = availableSurfaces[0].Surface;
            _movementPoints = GetRemainingMovementPoints();
            BuildPathsForSurfaceSelection(hex, surface);
            return;
        }

        // Multiple surfaces - pause for user selection
        TransitionTo(new SelectingSurfaceStep(this, hex.Coordinates, availableSurfaces));
    }

    private void BuildPathsForSurfaceSelection(Hex hex, HexSurface surface)
    {
        var startPosition = _selectedPath!.Destination;
        _movementPoints = GetRemainingMovementPoints();
        _possibleDirections = Game!.BattleMap?.GetPathsToHexWithAllFacings(
            startPosition,
            hex.Coordinates,
            _selectedPath!.MovementType,
            _movementPoints,
            _reachabilityData!.Value,
            _selectedUnit!.Height,
            _selectedUnit.MaxLevelChangeForward,
            _selectedUnit.MaxLevelChangeBackward,
            _prohibitedHexes,
            targetSurface: surface,
            approachWaypointOffset: Game!.RulesProvider.GetDfaAirborneBaseOffset()) ?? [];

        TransitionTo(new SelectingDirectionStep(this, hex.Coordinates));
    }

    private void HandleSurfaceSelection(HexSurface surface)
    {
        var targetHex = (_step as SelectingSurfaceStep)?.TargetHex;
        if (targetHex == null) return;
        
        _movementPoints = GetRemainingMovementPoints();
        var startPosition = _selectedPath!.Destination;
        _possibleDirections = Game!.BattleMap?.GetPathsToHexWithAllFacings(
            startPosition,
            targetHex,
            _selectedPath!.MovementType,
            _movementPoints,
            _reachabilityData!.Value,
            _selectedUnit!.Height,
            _selectedUnit.MaxLevelChangeForward,
            _selectedUnit.MaxLevelChangeBackward,
            _prohibitedHexes,
            targetSurface: surface,
            approachWaypointOffset: Game!.RulesProvider.GetDfaAirborneBaseOffset()) ?? [];

        TransitionTo(new SelectingDirectionStep(this, targetHex));
    }
    
    private void CompleteMovement()
    {
        lock (_stateLock)
        {
            _deferredMovementUnitId = null;
            ClearPendingMovementAttack();

            ClearHighlighting();
            _selectedUnit = null;
            _viewModel.HideMovementPath();
            _viewModel.HideDirectionSelector();
            _viewModel.HideSurfaceSelector();
            _viewModel.NotifySelectedUnitChanged();
            _isPostStandupMovement = false; // Reset post-standup state when movement is completed
            TransitionTo(new CompletedStep(this));

            var command = _builder.Build();
            if (command != null && _viewModel.Game is { } clientGame)
            {
                _builder.Reset();
                clientGame.MoveUnit(command.Value);
            }
        }
    }

    /// <summary>
    /// Handles an enemy hex click during normal target selection. Returns true when the hex holds an
    /// enemy unit, whether or not it is a legal movement-attack target: an ineligible enemy is
    /// ignored so the current movement selection stays intact.
    /// </summary>
    private bool TryHandleEnemyHex(Hex hex)
    {
        if (Game?.PhaseStepState?.ActivePlayer is not { } activePlayer) return false;

        var enemies = _viewModel.Units
            .Where(unit =>
                unit.Position?.Coordinates == hex.Coordinates
                && unit.Owner?.Id != activePlayer.Id)
            .ToList();
        if (enemies.Count == 0) return false;

        // Several enemies can share a hex; prefer the first one that is a legal target.
        foreach (var enemy in enemies)
        {
            if (!_movementAttackCandidates.TryGetValue(enemy, out var candidate)) continue;
            OpenMovementAttackDeclaration(candidate);
            break;
        }

        return true;
    }

    private void HandlePendingMovementAttackHexSelection(Hex hex)
    {
        if (_pendingAttackTarget == null) return;

        var otherUnit = _viewModel.Units.FirstOrDefault(unit =>
            unit.Position?.Coordinates == hex.Coordinates
            && unit.Id != _pendingAttackTarget.Id);
        if (otherUnit == null) return;

        if (_movementAttackCandidates.TryGetValue(otherUnit, out var candidate))
            OpenMovementAttackDeclaration(candidate);
    }

    /// <summary>
    /// Opens the shared declaration panel for a legal Charge or DFA without showing the direction
    /// selector: the chosen path already fixes the approach hex and facing.
    /// </summary>
    private void OpenMovementAttackDeclaration(MovementAttackCandidate candidate)
    {
        if (_selectedUnit == null
            || _selectedUnit.Pilot is null
            || _selectedPath == null
            || _selectedPath.Destination.Coordinates != candidate.Path.Start.Coordinates
            || Game?.BattleMap is not { } map) return;

        var path = _selectedPath.Append(candidate.Path);
        var declaration = candidate.Declaration;
        var stopPosition = path.GetStopPosition(declaration) ?? path.Destination;
        if (declaration.AttackType == PhysicalAttackType.DFA)
        {
            // Mirror BaseGame.OnMoveUnit: the attacker waits airborne above the approach hex.
            stopPosition = stopPosition with
            {
                AirborneLevel = map.GetDfaAirborneBaseLevel(
                    stopPosition,
                    path.Destination,
                    Game.RulesProvider.GetDfaAirborneBaseOffset())
            };
        }

        var scenario = PhysicalAttackScenario.ForPreview(
            _selectedUnit,
            candidate.Target,
            declaration.AttackType,
            stopPosition,
            path.MovementType,
            candidate.Target.MovementTaken?.HexesTraveled ?? 0);
        var breakdown = Game.PhysicalAttackCalculator.GetMovementAttackModifierBreakdown(scenario, map);
        var descriptionLines = BuildMovementAttackDescription(candidate.Target, declaration);
        var option = new PhysicalAttackOptionViewModel(
            declaration.AttackType,
            [],
            true,
            null,
            breakdown.Total,
            breakdown,
            (_, _) => { },
            _viewModel.LocalizationService,
            descriptionLines);

        _pendingAttackTarget = candidate.Target;
        _pendingAttackPath = path;
        _pendingAttackDeclaration = declaration;
        _pendingAttackOptions = [option];

        _viewModel.HideDirectionSelector();
        _viewModel.HideSurfaceSelector();
        _viewModel.ShowMovementPath(path);
        TransitionTo(new ConfirmMovementAttackStep(this));
    }

    private IReadOnlyList<string> BuildMovementAttackDescription(
        IUnit target,
        PhysicalAttackDeclarationData declaration)
    {
        var lines = new List<string>();
        if (_selectedUnit == null || Game == null) return lines;

        var hexesMoved = declaration.HexesMoved ?? 0;
        lines.Add(string.Format(GetString("PhysicalAttackDeclaration_MovementHexesMoved"), hexesMoved));

        var rules = Game.RulesProvider;
        if (declaration.AttackType == PhysicalAttackType.Charge)
        {
            lines.Add(string.Format(
                GetString("PhysicalAttackDeclaration_MovementDamage_Target"),
                rules.GetChargeDamage(_selectedUnit.Tonnage, hexesMoved)));
            lines.Add(string.Format(
                GetString("PhysicalAttackDeclaration_MovementDamage_Attacker"),
                rules.GetChargeCollisionDamage(target.Tonnage)));
            lines.Add(GetString("PhysicalAttackDeclaration_ChargePsr"));
            lines.Add(GetString("PhysicalAttackDeclaration_ChargeMiss"));
        }
        else
        {
            lines.Add(string.Format(
                GetString("PhysicalAttackDeclaration_MovementDamage_Target"),
                rules.GetDfaDamage(_selectedUnit.Tonnage)));
            lines.Add(string.Format(
                GetString("PhysicalAttackDeclaration_MovementDamage_Attacker"),
                rules.GetDfaAttackerDamage(_selectedUnit.Tonnage)));
            lines.Add(GetString("PhysicalAttackDeclaration_DfaPsr"));
            lines.Add(GetString("PhysicalAttackDeclaration_DfaMiss"));
        }

        lines.Add(GetString("PhysicalAttackDeclaration_MovementFacing"));
        lines.Add(GetString("PhysicalAttackDeclaration_NoWeaponOrPhysicalAttacks"));
        return lines;
    }

    private void ClearPendingMovementAttack()
    {
        _pendingAttackTarget = null;
        _pendingAttackPath = null;
        _pendingAttackDeclaration = null;
        _pendingAttackOptions = NoAttackOptions;
    }

    /// <summary>
    /// Revalidates the pending declaration before submitting it: the target may have moved or been
    /// destroyed between the preview and the confirmation. A stale declaration leaves the unit
    /// unmoved and returns to target selection.
    /// </summary>
    private void ConfirmMovementAttack()
    {
        if (!this.CanHumanPlayerAct()) return;

        var target = _pendingAttackTarget;
        var path = _pendingAttackPath;
        var declaration = _pendingAttackDeclaration;
        if (_selectedUnit == null
            || target == null
            || path == null
            || declaration == null
            || Game?.BattleMap is not { } map)
        {
            CancelPendingMovementAttack();
            return;
        }

        var validation = Game.MovementAttackDeclarationValidator.Validate(
            _selectedUnit,
            path.ToData(),
            path.MovementType,
            declaration,
            GetAllUnits(),
            map);
        if (!validation.IsValid)
        {
            CancelPendingMovementAttack();
            return;
        }

        _builder.SetMovementPath(path);
        _builder.SetMovementAttackTarget(target);
        CompleteMovement();
    }

    private void CancelPendingMovementAttack()
    {
        ClearPendingMovementAttack();
        _viewModel.ShowMovementPath(_selectedPath);

        if (_selectedPath is { TotalCost: > 0 })
        {
            // The partial move is still active. Restore the confirmation step together with the
            // single-direction selector it showed before the declaration opened. The declaration
            // can be opened from a direction selection for a different, unconfirmed hex, so the
            // dictionary must only hold the partial move: a stale entry for another hex would make
            // ConfirmMovement replace the partial move with a standing-still path.
            _possibleDirections = new Dictionary<HexDirection, MovementPath>
            {
                [_selectedPath.Destination.Facing] = _selectedPath
            };
            _viewModel.ShowDirectionSelector(
                _selectedPath.Destination.Coordinates,
                [_selectedPath.Destination.Facing]);
            TransitionTo(new ConfirmMovementStep(this));
        }
        else
        {
            TransitionTo(new SelectingTargetHexStep(this));
        }

        HighlightReachableHexes();
    }

    /// <inheritdoc />
    public bool IsDeclarationOpen => CurrentMovementStep == MovementStep.ConfirmMovementAttack;

    /// <inheritdoc />
    public IUnit? DeclarationAttacker => IsDeclarationOpen ? _selectedUnit : null;

    /// <inheritdoc />
    public IUnit? DeclarationTarget => _pendingAttackTarget;

    /// <inheritdoc />
    public IReadOnlyList<PhysicalAttackOptionViewModel> DeclarationOptions => _pendingAttackOptions;

    /// <inheritdoc />
    public void CloseDeclaration()
    {
        lock (_stateLock)
        {
            if (CurrentMovementStep != MovementStep.ConfirmMovementAttack) return;
            CancelPendingMovementAttack();
        }
    }

    public string ActionLabel => !IsActionRequired ? string.Empty : _step.ActionLabel;

    public bool IsActionRequired =>
        this.CanHumanPlayerAct() &&
        CurrentMovementStep != MovementStep.Completed;
    
    public bool CanExecutePlayerAction => CurrentMovementStep is MovementStep.ConfirmMovement
        or MovementStep.ConfirmMovementAttack;

    public string PlayerActionLabel => CurrentMovementStep switch
    {
        MovementStep.ConfirmMovement => _viewModel.LocalizationService.GetString("Action_MoveUnit"),
        MovementStep.ConfirmMovementAttack => GetMovementAttackActionLabel(),
        _ => string.Empty
    };

    private string GetMovementAttackActionLabel()
    {
        var attackType = _pendingAttackDeclaration?.AttackType;
        return attackType switch
        {
            PhysicalAttackType.Charge => _viewModel.LocalizationService.GetString("Action_DeclareCharge"),
            PhysicalAttackType.DFA => _viewModel.LocalizationService.GetString("Action_DeclareDfa"),
            _ => _viewModel.LocalizationService.GetString("Action_ConfirmMovementAttack")
        };
    }
    
    public MovementStep CurrentMovementStep => _step.Step;

    public void ExecutePlayerAction()
    {
        lock (_stateLock)
        {
            _step.ExecutePlayerAction();
        }
    }

    private string GetPsrProbabilityText(Mech mech, PilotingSkillRollType rollType)
    {
        var psrBreakdown = _viewModel.Game!.PilotingSkillCalculator.GetPsrBreakdown(
            mech, new PilotingSkillRollContext(rollType));
        var probability = Core.Utils.DiceUtils.Calculate2d6Probability(psrBreakdown.ModifiedPilotingSkill);
        return $" ({probability:0}%)";
    }

    private StateAction CreateStayProneAction()
    {
        return new StateAction(
            _viewModel.LocalizationService.GetString("Action_StayProne"),
            true,
            () => HandleMovementTypeSelection(MovementType.StandingStill));
    }

    private StateAction CreateProneFacingChangeAction(Mech mech)
    {
        var availableMp = mech.GetMovementPoints(MovementType.Walk);
        return new StateAction(
            string.Format(_viewModel.LocalizationService.GetString("Action_ChangeFacing"), availableMp),
            true,
            () => HandleProneFacingChange(mech));
    }

    private string GetStandupActionText(MovementType movementType, string probabilityText)
    {
        if (_selectedUnit is Mech { IsMinimumMovement: true })
            return _viewModel.LocalizationService.GetString("Action_AttemptStandup") + probabilityText;

        var typeKey = movementType == MovementType.Run ? "MovementType_Run" : "MovementType_Walk";
        return string.Format(
            _viewModel.LocalizationService.GetString("Action_MovementPoints"),
            _viewModel.LocalizationService.GetString(typeKey),
            _selectedUnit!.GetMovementPoints(movementType)) + probabilityText;
    }

    // TODO that should be a part of UnitPresentationExtensions
    public IEnumerable<StateAction> GetAvailableActions()
    {
        lock (_stateLock)
        {
            if (CurrentMovementStep == MovementStep.ConfirmMovementAttack)
            {
                return
                [
                    new StateAction(GetMovementAttackActionLabel(), true, ExecutePlayerAction)
                ];
            }

            if (CurrentMovementStep != MovementStep.SelectingMovementType || _selectedUnit == null)
                return [];

            if (_selectedUnit is Mech { IsProne: true } mech
                && _viewModel.Game is not null)
            {
                // Post-fall: MovementTaken being non-null means the unit was already moving when it fell.
                // Lock the standup to the originally declared movement type
                return mech.MovementTaken != null
                    ? GetLockedProneActions(mech) 
                    : GetStartOfPhaseProneActions(mech);
            }

            return GetNormalActions();
        }
    }

    private List<StateAction> GetLockedProneActions(Mech mech)
    {
        var lockedType = mech.MovementTaken!.MovementType;
        var lockedProneActions = new List<StateAction>
        {
            CreateStayProneAction()
        };

        if (!mech.CanStandup()) return lockedProneActions;

        var probabilityText = GetPsrProbabilityText(mech, PilotingSkillRollType.StandupAttempt);
        lockedProneActions.Add(new StateAction(
            GetStandupActionText(lockedType, probabilityText),
            true,
            () => AttemptStandup(lockedType)));

        if (mech.CanChangeFacingWhileProne())
            lockedProneActions.Add(CreateProneFacingChangeAction(mech));

        return lockedProneActions;
    }

    private List<StateAction> GetStartOfPhaseProneActions(Mech mech)
    {
        var proneActions = new List<StateAction>
        {
            CreateStayProneAction()
        };

        if (mech.IsImmobile) return proneActions;

        if (mech.CanStandup())
        {
            var probabilityText = GetPsrProbabilityText(mech, PilotingSkillRollType.StandupAttempt);

            if (mech.IsMinimumMovement)
            {
                proneActions.Add(new StateAction(
                    _viewModel.LocalizationService.GetString("Action_AttemptStandup") + probabilityText,
                    true,
                    () => AttemptStandup(MovementType.Walk)));
            }
            else
            {
                proneActions.Add(new StateAction(
                    GetStandupActionText(MovementType.Walk, probabilityText),
                    true,
                    () => AttemptStandup(MovementType.Walk)));

                if (mech.CanRun)
                {
                    proneActions.Add(new StateAction(
                        GetStandupActionText(MovementType.Run, probabilityText),
                        true,
                        () => AttemptStandup(MovementType.Run)));
                }
            }
        }

        if (mech.CanChangeFacingWhileProne())
            proneActions.Add(CreateProneFacingChangeAction(mech));

        return proneActions;
    }

    private List<StateAction> GetNormalActions()
    {
        var actions = new List<StateAction>
        {
            new(
                _viewModel.LocalizationService.GetString("Action_StandStill"),
                true,
                () => HandleMovementTypeSelection(MovementType.StandingStill))
        };

        if (_selectedUnit!.IsImmobile) return actions;

        actions.Add(new StateAction(
            string.Format(_viewModel.LocalizationService.GetString("Action_MovementPoints"),
                _viewModel.LocalizationService.GetString("MovementType_Walk"),
                _selectedUnit.GetMovementPoints(MovementType.Walk)),
            true,
            () => HandleMovementTypeSelection(MovementType.Walk)));

        if (_selectedUnit is Mech { CanRun: true })
        {
            actions.Add(new StateAction(
                string.Format(_viewModel.LocalizationService.GetString("Action_MovementPoints"),
                    _viewModel.LocalizationService.GetString("MovementType_Run"),
                    _selectedUnit.GetMovementPoints(MovementType.Run)),
                true,
                () => HandleMovementTypeSelection(MovementType.Run)));
        }

        if (_selectedUnit is not Mech { CanJump: true }) return actions;
        var jumpPoints = _selectedUnit.GetMovementPoints(MovementType.Jump);

        var jumpActionText = string.Format(_viewModel.LocalizationService.GetString("Action_MovementPoints"),
            _viewModel.LocalizationService.GetString("MovementType_Jump"),
            jumpPoints);

        if (_selectedUnit is Mech jumpMech && jumpMech.IsPsrForJumpRequired() && _viewModel.Game is not null)
        {
            jumpActionText += GetPsrProbabilityText(jumpMech, PilotingSkillRollType.JumpWithDamage);
        }

        actions.Add(new StateAction(
            jumpActionText,
            true,
            () => HandleMovementTypeSelection(MovementType.Jump)));

        return actions;
    }

    // A method to handle standup attempts
    private void AttemptStandup(MovementType movementType)
    {
        lock (_stateLock)
        {
            if (_viewModel.Game?.PhaseStepState?.ActivePlayer == null) return;
            if (_selectedUnit?.Position == null) return;

            // Set the selected movement type for later use
            _selectedPath = new MovementPath([
                new PathSegment(_selectedUnit.Position, _selectedUnit.Position, [])],
                movementType);
            // Ensure the builder has the unit and movement type set
            _builder.SetUnit(_selectedUnit);
            _builder.SetMovementPath(_selectedPath);

            TransitionTo(new SelectingStandingUpDirectionStep(this));
            _viewModel.ShowDirectionSelector(_selectedUnit.Position.Coordinates, Enum.GetValues<HexDirection>());
            _viewModel.NotifyStateChanged();
        }
    }

    // New method to handle standup with direction selection
    private void CompleteStandupAttempt(HexDirection direction)
    {
        lock (_stateLock)
        {
            if (_viewModel.Game?.PhaseStepState?.ActivePlayer == null) return;
            if (_selectedUnit?.Position == null) return;
            if (_selectedPath?.MovementType == null) return;

            // Create a standup command with the selected direction
            var standupCommand = new TryStandupCommand
            {
                GameOriginId = _viewModel.Game.Id,
                UnitId = _selectedUnit.Id,
                PlayerId = _viewModel.Game.PhaseStepState.Value.ActivePlayer.Id,
                NewFacing = direction,
                MovementTypeAfterStandup = _selectedPath.MovementType
            };
            
            // Hide the selectors; the movement path context is kept so that a fall from
            // this failed standup attempt can resume movement (see ResumeMovementAfterFall)
            _viewModel.HideDirectionSelector();
            _viewModel.HideSurfaceSelector();
            _viewModel.NotifyStateChanged();
            
            // Publish the command
            _viewModel.Game.TryStandupUnit(standupCommand);
        }
    }

    // Method to resume movement after successful standup
    public void ResumeMovementAfterStandup(Guid unitId)
    {
        lock (_stateLock)
        {
            // Fallback: if _selectedUnit is null (e.g., cleared by ClearSelection during phase change),
            // try to find the unit from the view model
            if (_selectedUnit == null || _selectedUnit.Id != unitId)
            {
                var stoodUpUnit = _viewModel.Units.FirstOrDefault(u => u.Id == unitId);
                if (stoodUpUnit == null)
                {
                    Game?.Logger.LogWarning("ResumeMovementAfterStandup: unit {UnitId} not found among alive units", unitId);
                    return;
                }
                _selectedUnit = stoodUpUnit;
                _builder.SetUnit(stoodUpUnit);
            }
            
            // Check if the unit is no longer prone (standup was successful)
            if (_selectedPath?.MovementType == null ||
                _selectedUnit is not Mech { IsProne: false, Position: not null } mech) return;

            if (mech.GetMovementPoints(_selectedPath.MovementType) < 1)
            {
                // No more movement possible, just confirm
                CompleteMovement();
                return;
            }
            
            _isPostStandupMovement = true; // Mark that this unit is in post-standup movement state
            _selectedPath = new MovementPath([
                new PathSegment(mech.Position, mech.Position, [])],
                _selectedPath.MovementType);
            _builder.SetMovementPath(_selectedPath);
            HighlightReachableHexes();
            TransitionTo(new SelectingTargetHexStep(this));
        }
    }

    public void ResumeMovementAfterFall(Guid unitId)
    {
        lock (_stateLock)
        {
            if (!TryAdoptFallenUnit(unitId)) return;

            if (_selectedUnit is not Mech mech)
            {
                Game?.Logger.LogWarning(
                    "Resume movement after fall ignored: unit {UnitId} is not a mech", unitId);
                return;
            }

            if (mech.Position == null || !mech.IsProne)
            {
                CompleteMovementAfterFailedFall(mech, unitId);
                return;
            }

            _selectedPath = RebuildPathAtCurrentPosition(mech, unitId);

            if (!mech.CanStandup())
            {
                _builder.SetUnit(_selectedUnit);
                _builder.SetMovementPath(_selectedPath);
                CompleteMovement();
                return;
            }

            _deferredMovementUnitId = unitId;

            TransitionTo(new SelectingMovementTypeStep(this));
            _viewModel.NotifySelectedUnitChanged();
        }
    }

    /// <summary>
    /// Makes the fallen unit the selected one. Selection may have been cleared by a state refresh, in
    /// which case the fallen unit is adopted when it belongs to the active player. Returns
    /// <c>false</c> when the command cannot be resumed, after logging why.
    /// </summary>
    private bool TryAdoptFallenUnit(Guid unitId)
    {
        var activePlayerId = Game?.PhaseStepState?.ActivePlayer.Id;

        if (_selectedUnit == null)
        {
            var fallenUnit = _viewModel.Units.FirstOrDefault(u => u.Id == unitId);
            if (fallenUnit == null)
            {
                Game?.Logger.LogWarning(
                    "ResumeMovementAfterFall: unit {UnitId} not found among alive units", unitId);
                return false;
            }

            if (fallenUnit.Owner?.Id != activePlayerId)
            {
                Game?.Logger.LogWarning(
                    "Resume movement after fall ignored: unit {UnitId} is not controlled by the active player", unitId);
                return false;
            }

            _selectedUnit = fallenUnit;
            _builder.SetUnit(fallenUnit);
        }
        else if (_selectedUnit.Id != unitId)
        {
            Game?.Logger.LogWarning(
                "Resume movement after fall ignored: command unit {CommandUnit} does not match movement state's selected unit {StateUnit}",
                unitId,
                _selectedUnit.Id);
            return false;
        }

        if (_selectedUnit.Owner?.Id == activePlayerId) return true;

        Game?.Logger.LogWarning(
            "Resume movement after fall ignored: unit {UnitId} is not controlled by the active player", unitId);
        return false;
    }

    /// <summary>
    /// The fall left the unit standing (or off the map): submit the current position and end movement.
    /// </summary>
    private void CompleteMovementAfterFailedFall(Mech mech, Guid unitId)
    {
        Game?.Logger.LogWarning(
            "Resume movement after fall: unit {UnitId} is {Reason}, ending movement",
            unitId,
            mech.Position == null ? "not positioned" : "not prone");

        if (mech.Position != null)
        {
            _builder.SetUnit(_selectedUnit);
            _builder.SetMovementPath(MovementPath.CreateSingleSegmentPath(mech.Position));
        }

        CompleteMovement();
    }

    /// <summary>
    /// Rebuilds the path as a single segment at the mech's current position. A missing path means the
    /// fall was processed by a state that lost it (e.g. recreated after a phase-step update), so the
    /// movement type comes from the unit's own movement instead.
    /// </summary>
    private MovementPath RebuildPathAtCurrentPosition(Mech mech, Guid unitId)
    {
        if (_selectedPath != null)
            return MovementPath.CreateSingleSegmentPath(mech.Position!, _selectedPath.MovementType);

        Game?.Logger.LogWarning(
            "Resume movement after fall: no movement path in state for unit {UnitId}, rebuilding at current position",
            unitId);

        return MovementPath.CreateSingleSegmentPath(
            mech.Position!,
            mech.MovementTaken?.MovementType ?? MovementType.Walk);
    }

    // New method to handle prone facing change
    private void HandleProneFacingChange(Mech mech)
    {
        if (_viewModel.Game?.PhaseStepState?.ActivePlayer == null) return;
        if (mech.Position == null || !mech.IsProne) return;

        _movementPoints = mech.GetMovementPoints(MovementType.Walk);

        // Calculate maximum rotation steps based on available movement points
        var maxRotateSteps = Math.Min(3, _movementPoints);

        // Reset possible directions
        _possibleDirections = [];

        var currentFacing = mech.Facing;
        if (currentFacing == null) return;
        
        _selectedPath = new MovementPath([
            new PathSegment(mech.Position, mech.Position, [])],
            MovementType.Walk);
        _builder.SetMovementPath(_selectedPath);

        void AddToPossibleDirections(HexDirection direction, int cost)
        {
            var pathSegments = new MovementPath([
                new PathSegment(mech.Position, mech.Position with { Facing = direction }, [new RotationMovementCost { FromFacing = mech.Position.Facing, ToFacing = direction, Value = cost }])
            ], MovementType.Walk);
            _possibleDirections[direction] = pathSegments;
        }

        // Generate possible directions by rotating from the current facing
        for (var steps = 1; steps <= maxRotateSteps; steps++)
        {
            var rotatedDirectionCw = currentFacing.Value.Rotate(steps);
            AddToPossibleDirections(rotatedDirectionCw, steps);
        
            if (steps == 3) break; // We don't want to duplicate an opposite direction
        
            var rotatedDirectionCcw = currentFacing.Value.Rotate(-steps);
            AddToPossibleDirections(rotatedDirectionCcw, steps);
        }

        TransitionTo(new SelectingDirectionStep(this, mech.Position.Coordinates));
    }

    private interface IMovementStep
    {
        MovementStep Step { get; }
        string ActionLabel { get; }
        void HandleMovementTypeSelection(MovementType movementType);
        void HandleHexSelection(Hex hex);
        void HandleFacingSelection(HexDirection direction);
        void ExecutePlayerAction();
    }

    private abstract class MovementStepBase : IMovementStep
    {
        protected readonly MovementState State;

        protected MovementStepBase(MovementState state)
        {
            State = state;
        }

        public abstract MovementStep Step { get; }
        public abstract string ActionLabel { get; }
        public virtual void HandleMovementTypeSelection(MovementType movementType) { }
        public virtual void HandleHexSelection(Hex hex) { }
        public virtual void HandleFacingSelection(HexDirection direction) { }
        public virtual void ExecutePlayerAction() { }
    }

    private sealed class SelectingUnitStep : MovementStepBase
    {
        public SelectingUnitStep(MovementState state) : base(state) { }
        public override MovementStep Step => MovementStep.SelectingUnit;
        public override string ActionLabel => State._viewModel.LocalizationService.GetString("Action_SelectUnitToMove");
    }

    private sealed class SelectingMovementTypeStep : MovementStepBase
    {
        public SelectingMovementTypeStep(MovementState state) : base(state) { }
        public override MovementStep Step => MovementStep.SelectingMovementType;
        public override string ActionLabel => State._viewModel.LocalizationService.GetString("Action_SelectMovementType");

        public override void HandleMovementTypeSelection(MovementType movementType)
        {
            if (!State.CanHumanPlayerAct()) return;
            if (State._selectedUnit?.Position == null) return;
            if (State.CurrentMovementStep != MovementStep.SelectingMovementType) return;
            
            if (movementType == MovementType.StandingStill)
            {
                // For standing still, we create an empty movement path
                var path = MovementPath.CreateSingleSegmentPath(State._selectedUnit.Position);
                State._builder.SetMovementPath(path);
                State.CompleteMovement();
                return;
            }

            State._selectedPath = new MovementPath([
                    new PathSegment(State._selectedUnit.Position, State._selectedUnit.Position, [])],
                movementType);
            State.HighlightReachableHexes();
            State.TransitionTo(new SelectingTargetHexStep(State));
        }
    }

    private sealed class SelectingTargetHexStep : MovementStepBase
    {
        public SelectingTargetHexStep(MovementState state) : base(state) { }
        public override MovementStep Step => MovementStep.SelectingTargetHex;
        public override string ActionLabel => State._viewModel.LocalizationService.GetString("Action_SelectTargetHexOrEnemy");

        public override void HandleHexSelection(Hex hex)
        {
            State.HandleTargetHexSelection(hex);
        }
    }

    private sealed class SelectingSurfaceStep : MovementStepBase
    {
        public HexCoordinates TargetHex { get; }

        public SelectingSurfaceStep(MovementState state, HexCoordinates targetHex, IReadOnlyList<HexReachabilityData> availableSurfaces) : base(state)
        {
            TargetHex = targetHex;

            var vm = new SurfaceSelectorViewModel(
                availableSurfaces,
                surface =>
                {
                    State._viewModel.HideSurfaceSelector();
                    State.HandleSurfaceSelection(surface);
                },
                State._viewModel.LocalizationService,
                () =>
                {
                    State._viewModel.HideSurfaceSelector();
                    State.TransitionTo(new SelectingTargetHexStep(State));
                });
            State._viewModel.ShowSurfaceSelector(targetHex, vm);
            State._viewModel.NotifyStateChanged();
        }
        public override MovementStep Step => MovementStep.SelectingSurface;
        public override string ActionLabel => State._viewModel.LocalizationService.GetString("Action_SelectSurface");
    }

    private sealed class SelectingDirectionStep : MovementStepBase
    {
        public SelectingDirectionStep(MovementState state, HexCoordinates targetHex) : base(state)
        {
            if (State._possibleDirections.Count == 0) return;
            State._viewModel.ShowDirectionSelector(targetHex, State._possibleDirections.Keys);
            State._viewModel.NotifyStateChanged();
        }
        public override MovementStep Step => MovementStep.SelectingDirection;
        public override string ActionLabel => State._viewModel.LocalizationService.GetString("Action_SelectFacingDirection");

        public override void HandleHexSelection(Hex hex)
        {
            State.HandleTargetHexSelection(hex);
        }

        public override void HandleFacingSelection(HexDirection direction)
        {
            if (State.CurrentMovementStep != MovementStep.SelectingDirection) return;
            if (!State._possibleDirections.TryGetValue(direction, out var path)) return;

            State._selectedPath = State._selectedPath?.Append(path) ?? path;
            State._builder.SetMovementPath(State._selectedPath);

            State._viewModel.ShowDirectionSelector(path.Destination.Coordinates, [direction]);
            State._viewModel.ShowMovementPath(State._selectedPath);

            var movementType = State._selectedPath.MovementType;
            var remainingMp = State.GetRemainingMovementPoints();
            State.TransitionTo(new ConfirmMovementStep(State));
            if (movementType is MovementType.Walk or MovementType.Run && remainingMp > 0)
            {
                State.HighlightReachableHexes();
            }
            else
            {
                // Reachability is not recomputed from the new destination (a jump or a path that
                // used every MP), so the candidates and their highlights are stale.
                State.ClearMovementAttackHighlights();
            }
            State._viewModel.NotifyStateChanged();
        }
    }

    private sealed class ConfirmMovementStep : MovementStepBase
    {
        public ConfirmMovementStep(MovementState state) : base(state) { }
        public override MovementStep Step => MovementStep.ConfirmMovement;
        public override string ActionLabel => GetActionLabel();

        private string GetActionLabel()
        {
            var remainingMp = State.GetRemainingMovementPoints();
            var movementType = State._selectedPath?.MovementType;
            
            // Check if we can continue movement (walk/run with remaining MPs)
            if (movementType is MovementType.Walk or MovementType.Run && remainingMp > 0)
            {
                return State._viewModel.LocalizationService.GetString("Action_ConfirmOrSelectNextHex");
            }
            
            // No more movement possible, just confirm
            return State._viewModel.LocalizationService.GetString("Action_ConfirmMovement");
        }

        public override void HandleHexSelection(Hex hex)
        {
            // An enemy hex can open a Charge/DFA declaration even in the confirmation step.
            if (State.TryHandleEnemyHex(hex)) return;

            // Reset selection if clicked outside reachable hexes during the confirmation step
            // This matches the behavior in HandleTargetHexSelection and SelectingDirectionStep
            if (State._reachabilityData != null && !State._reachabilityData.Value.IsHexReachable(hex.Coordinates))
            {
                State.ResetUnitSelection();
                return;
            }

            if (State._selectedPath?.MovementType is not (MovementType.Walk or MovementType.Run)) return;
            if (State.GetRemainingMovementPoints() <= 0) return;
            State.HandleTargetHexSelection(hex);
        }

        public override void HandleFacingSelection(HexDirection direction)
        {
            State.ConfirmMovement();
        }

        public override void ExecutePlayerAction()
        {
            State.ConfirmMovement();
        }
    }

    private sealed class ConfirmMovementAttackStep : MovementStepBase
    {
        public ConfirmMovementAttackStep(MovementState state) : base(state) { }
        public override MovementStep Step => MovementStep.ConfirmMovementAttack;
        public override string ActionLabel =>
            State._viewModel.LocalizationService.GetString("Action_ConfirmMovementAttack");

        public override void HandleHexSelection(Hex hex)
        {
            State.HandlePendingMovementAttackHexSelection(hex);
        }

        public override void ExecutePlayerAction()
        {
            State.ConfirmMovementAttack();
        }
    }

    private sealed class SelectingStandingUpDirectionStep : MovementStepBase
    {
        public SelectingStandingUpDirectionStep(MovementState state) : base(state) { }
        public override MovementStep Step => MovementStep.SelectingStandingUpDirection;
        public override string ActionLabel => State._viewModel.LocalizationService.GetString("Action_SelectFacingDirection");

        public override void HandleFacingSelection(HexDirection direction)
        {
            State.CompleteStandupAttempt(direction);
        }
    }

    private sealed class CompletedStep : MovementStepBase
    {
        public CompletedStep(MovementState state) : base(state) { }
        public override MovementStep Step => MovementStep.Completed;
        public override string ActionLabel => string.Empty;
    }

    /// <summary>
    /// One legal target with the cheapest continuation path from the current path state that
    /// reaches its hex and passes the shared declaration validator, plus the server-computed
    /// declaration for that path.
    /// </summary>
    private sealed record MovementAttackCandidate(
        IUnit Target,
        MovementPath Path,
        PhysicalAttackDeclarationData Declaration);

    /// <summary>
    /// The fixed inputs of one candidate search, captured so the per-target steps stay small.
    /// </summary>
    private readonly record struct MovementAttackSearch(
        IUnit Unit,
        MovementPath CurrentPath,
        IBattleMap Map,
        Guid? ActivePlayerId,
        MovementType MovementType,
        PhysicalAttackType AttackType,
        int RemainingMp,
        bool CanMoveBackward)
    {
        public HexPosition StartPosition => CurrentPath.Destination;
    }
}

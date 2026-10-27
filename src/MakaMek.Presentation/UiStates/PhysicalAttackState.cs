using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Highlights;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// The physical attack declaration state: pick the attacker, pick the attack type, pick the target,
/// then declare with the chosen limbs (or skip). It mirrors <see cref="WeaponsAttackState"/>, but
/// every eligibility decision comes from <see cref="IPhysicalAttackEligibility"/> so the UI cannot
/// disagree with the server about what is legal.
/// </summary>
/// <remarks>
/// The presentation layer writes this state on the UI thread, but <see cref="UnitControl"/> reads
/// <see cref="GetAvailableActions"/> from its 30 fps polling observable before hopping to the UI
/// thread. Every read and write of the mutable state therefore happens under
/// <see cref="_stateLock"/>: the public properties lock in their accessors and the private helpers
/// that need a coherent view of several fields lock for the duration of that view.
/// </remarks>
public class PhysicalAttackState : IUiState, ICombatSelectionRoles
{
    private static readonly IReadOnlyList<PhysicalAttackType> SupportedAttackTypes =
        PhysicalAttackDefinitions.Supported.Select(definition => definition.AttackType).ToList();

    private static readonly IReadOnlyDictionary<IUnit, IReadOnlyList<PartLocation>> NoEligibleTargets =
        new Dictionary<IUnit, IReadOnlyList<PartLocation>>();

    private readonly BattleMapViewModel _viewModel;
    private readonly Lock _stateLock = new();

    /// <summary>
    /// The limbs the attacker could use against each legal target for the selected attack type.
    /// Replaced as a whole under the lock, never mutated in place, so a reader always sees a
    /// consistent map.
    /// </summary>
    private IReadOnlyDictionary<IUnit, IReadOnlyList<PartLocation>> _eligibleLimbs = NoEligibleTargets;

    private PhysicalAttackStep _currentStep = PhysicalAttackStep.SelectingUnit;
    private PhysicalAttackType? _selectedAttackType;
    private IUnit? _attacker;
    private IUnit? _selectedTarget;
    private IReadOnlyList<PhysicalAttackType> _availableAttackTypes = [];

    public IClientGame Game { get; }

    public PhysicalAttackStep CurrentStep
    {
        get
        {
            lock (_stateLock) return _currentStep;
        }
        private set
        {
            lock (_stateLock) _currentStep = value;
        }
    }

    public PhysicalAttackType? SelectedAttackType
    {
        get
        {
            lock (_stateLock) return _selectedAttackType;
        }
        private set
        {
            lock (_stateLock) _selectedAttackType = value;
        }
    }

    public IUnit? Attacker
    {
        get
        {
            lock (_stateLock) return _attacker;
        }
        private set
        {
            lock (_stateLock) _attacker = value;
        }
    }

    public IUnit? SelectedTarget
    {
        get
        {
            lock (_stateLock) return _selectedTarget;
        }
        private set
        {
            lock (_stateLock) _selectedTarget = value;
        }
    }

    public PhysicalAttackPreviewViewModel? Preview
    {
        get
        {
            lock (_stateLock) return field;
        }
        private set
        {
            lock (_stateLock) field = value;
        }
    }

    /// <summary>
    /// The attack types the selected attacker can actually make against at least one enemy right now.
    /// </summary>
    public IReadOnlyList<PhysicalAttackType> AvailableAttackTypes
    {
        get
        {
            lock (_stateLock) return _availableAttackTypes;
        }
        private set
        {
            lock (_stateLock) _availableAttackTypes = value;
        }
    }

    public PhysicalAttackState(BattleMapViewModel viewModel)
    {
        Game = viewModel.Game! ?? throw new InvalidOperationException("Game is not client game");
        _viewModel = viewModel;
        if (Game.PhaseStepState?.ActivePlayer == null)
        {
            throw new InvalidOperationException("Active player is null");
        }
    }

    public string ActionLabel
    {
        get
        {
            lock (_stateLock)
            {
                return _currentStep switch
                {
                    PhysicalAttackStep.SelectingUnit => GetString("Action_SelectUnitToAttack"),
                    PhysicalAttackStep.ActionSelection when _availableAttackTypes.Count == 0 =>
                        GetString("PhysicalAttack_NoOptions"),
                    PhysicalAttackStep.ActionSelection => GetString("Action_SelectAction"),
                    PhysicalAttackStep.TargetSelection => GetString("Action_SelectTarget"),
                    _ => string.Empty
                };
            }
        }
    }

    public bool IsActionRequired => this.CanHumanPlayerAct();

    public bool CanExecutePlayerAction
    {
        get
        {
            lock (_stateLock)
            {
                return _currentStep is PhysicalAttackStep.ActionSelection or PhysicalAttackStep.TargetSelection
                       && !_viewModel.AreActionsMenuOffMap;
            }
        }
    }

    public string PlayerActionLabel
    {
        get
        {
            if (!IsActionRequired) return string.Empty;

            lock (_stateLock)
            {
                return _currentStep switch
                {
                    PhysicalAttackStep.ActionSelection => GetString("Action_SkipAttack"),
                    PhysicalAttackStep.TargetSelection => GetDefaultLimbs() != null
                        ? GetString("Action_DeclareAttack")
                        : GetString("Action_SkipAttack"),
                    _ => string.Empty
                };
            }
        }
    }

    public void ExecutePlayerAction()
    {
        if (!this.CanHumanPlayerAct()) return;

        lock (_stateLock)
        {
            switch (_currentStep)
            {
                case PhysicalAttackStep.ActionSelection:
                    Skip();
                    break;
                case PhysicalAttackStep.TargetSelection when GetDefaultLimbs() is { } limbs:
                    Declare(limbs);
                    break;
                case PhysicalAttackStep.TargetSelection:
                    Skip();
                    break;
            }
        }
    }

    public IUnit? SelectedUnit
    {
        get
        {
            lock (_stateLock)
                return _currentStep == PhysicalAttackStep.TargetSelection ? _selectedTarget : _attacker;
        }
        private set
        {
            if (value == null) return;

            lock (_stateLock)
            {
                if (!this.CanHumanPlayerAct()) return;
                if (value.IsDestroyed) return;

                if (_currentStep is PhysicalAttackStep.SelectingUnit or PhysicalAttackStep.ActionSelection)
                {
                    if (!CanActAsAttacker(value)) return;

                    if (_attacker != null && !ReferenceEquals(_attacker, value))
                        ResetAttackerDependentState();

                    Attacker = value;
                    UpdateAvailableAttackTypes();
                    HighlightValidTargets(SupportedAttackTypes);
                    CurrentStep = PhysicalAttackStep.ActionSelection;
                    _viewModel.NotifyStateChanged();
                    return;
                }

                if (_currentStep != PhysicalAttackStep.TargetSelection) return;

                if (!IsEligibleTarget(value))
                {
                    CancelTargetSelection();
                    return;
                }

                SelectedTarget = value;
                Preview = CreatePreview(value);
                _viewModel.IsPhysicalAttackPreviewVisible = Preview != null;
                _viewModel.NotifyStateChanged();
            }
        }
    }

    public void HandleUnitSelectionFromList(IUnit? unit)
    {
        SelectedUnit = unit;
    }

    public void HandleHexSelection(Hex hex)
    {
        if (!this.CanHumanPlayerAct()) return;

        if (CurrentStep == PhysicalAttackStep.TargetSelection)
        {
            var target = FindUnitOnHex(hex);
            if (target != null && IsEligibleTarget(target))
            {
                SelectedUnit = target;
                return;
            }

            // Any other hex abandons target selection but keeps the attacker and its attack type.
            CancelTargetSelection();
            return;
        }

        var unit = FindUnitOnHex(hex);
        if (unit == null) return;
        SelectedUnit = unit;
    }

    public void HandleFacingSelection(HexDirection direction)
    {
        // A physical attack never needs a facing decision.
    }

    /// <summary>
    /// Only the active player's Mechs that still have an attack option can be picked as attackers.
    /// </summary>
    public bool CanSelectUnit(IUnit? unit)
    {
        if (unit == null) return true;

        lock (_stateLock)
        {
            return _currentStep is PhysicalAttackStep.SelectingUnit or PhysicalAttackStep.ActionSelection
                   && CanActAsAttacker(unit);
        }
    }

    public IEnumerable<StateAction> GetAvailableActions()
    {
        lock (_stateLock)
        {
            if (_attacker == null) return [];

            var actions = new List<StateAction>();

            if (_currentStep == PhysicalAttackStep.ActionSelection)
            {
                foreach (var attackType in _availableAttackTypes)
                {
                    var type = attackType;
                    actions.Add(new StateAction(
                        GetString($"PhysicalAttackType_{type}"),
                        true,
                        () => SelectAttackType(type)));
                }
            }
            else if (_currentStep == PhysicalAttackStep.TargetSelection)
            {
                foreach (var (label, limbs) in GetDeclarationOptions())
                {
                    var declarationLimbs = limbs;
                    actions.Add(new StateAction(label, true, () => Declare(declarationLimbs)));
                }
            }
            else
            {
                return actions;
            }

            actions.Add(new StateAction(GetString("Action_SkipAttack"), true, Skip));
            return actions;
        }
    }

    private void SelectAttackType(PhysicalAttackType attackType)
    {
        if (!this.CanHumanPlayerAct()) return;

        lock (_stateLock)
        {
            if (_attacker == null) return;

            SelectedAttackType = attackType;
            var targets = BuildEligibleTargets(attackType);
            SetEligibleTargets(targets);
            if (targets.Count == 0)
            {
                SelectedAttackType = null;
                return;
            }

            // Drop the attacker-wide highlights added when the attacker was picked, so only the
            // targets that are valid for this attack type stay highlighted.
            _viewModel.ClearHighlights();
            HighlightValidTargets([attackType]);
            CurrentStep = PhysicalAttackStep.TargetSelection;
            _viewModel.NotifyStateChanged();
        }
    }

    private IReadOnlyDictionary<IUnit, IReadOnlyList<PartLocation>> BuildEligibleTargets(
        PhysicalAttackType attackType)
    {
        var targets = new Dictionary<IUnit, IReadOnlyList<PartLocation>>();
        if (Attacker is not { } attacker || Game.BattleMap is not { } map) return targets;

        foreach (var enemy in GetEnemies())
        {
            var limbs = Game.PhysicalAttackEligibility.GetEligibleLimbs(attacker, enemy, attackType, map);
            if (limbs.Count > 0)
                targets[enemy] = limbs;
        }

        return targets;
    }

    private void SetEligibleTargets(IReadOnlyDictionary<IUnit, IReadOnlyList<PartLocation>> targets)
    {
        lock (_stateLock) _eligibleLimbs = targets;
    }

    private void ClearEligibleTargets()
    {
        lock (_stateLock) _eligibleLimbs = NoEligibleTargets;
    }

    private bool IsEligibleTarget(IUnit unit)
    {
        lock (_stateLock) return _eligibleLimbs.ContainsKey(unit);
    }

    private void UpdateAvailableAttackTypes()
    {
        if (Attacker is not { } attacker || Game.BattleMap is not { } map)
        {
            AvailableAttackTypes = [];
            return;
        }

        var enemies = GetEnemies();
        AvailableAttackTypes = SupportedAttackTypes
            .Where(attackType => enemies.Any(enemy =>
                Game.PhysicalAttackEligibility.GetEligibleLimbs(attacker, enemy, attackType, map).Count > 0))
            .ToList();
    }

    private bool CanActAsAttacker(IUnit unit) =>
        !unit.IsDestroyed
        && unit.Owner?.Id == Game.PhaseStepState?.ActivePlayer.Id
        && !unit.HasDeclaredPhysicalAttack
        && HasAnyOption(unit);

    private bool HasAnyOption(IUnit unit)
    {
        if (Game.BattleMap is not { } map) return false;

        // Evaluated against the candidate, not the current attacker: this runs before the
        // candidate becomes the attacker.
        var enemies = _viewModel.Units.Where(other =>
            other.Owner?.Id != unit.Owner?.Id
            && !other.IsDestroyed
            && other.Position != null).ToList();

        return Game.PhysicalAttackEligibility.HasAnyOption(unit, enemies, map);
    }

    /// <summary>
    /// Materialised under the lock: the caller may then iterate it on another thread without
    /// holding the lock while it reads the view model's unit list.
    /// </summary>
    private List<IUnit> GetEnemies()
    {
        IUnit? attacker;
        lock (_stateLock) attacker = _attacker;
        if (attacker == null) return [];

        return _viewModel.Units.Where(unit =>
            unit.Owner?.Id != attacker.Owner?.Id
            && !unit.IsDestroyed
            && unit.Position != null).ToList();
    }

    private IUnit? FindUnitOnHex(Hex hex) =>
        _viewModel.Units.FirstOrDefault(unit => unit.Position?.Coordinates == hex.Coordinates);

    /// <summary>
    /// Highlights every hex that holds a unit the attacker can reach with any of the given attack types.
    /// The labels are the localized attack type names, matching the weapon reach highlight convention.
    /// </summary>
    private void HighlightValidTargets(IEnumerable<PhysicalAttackType> attackTypes)
    {
        if (Attacker is not { } attacker || Game.BattleMap is not { } map) return;

        var hexHighlights = new Dictionary<HexCoordinates, HashSet<string>>();
        var enemies = GetEnemies();

        foreach (var attackType in attackTypes)
        {
            foreach (var enemy in enemies)
            {
                if (Game.PhysicalAttackEligibility.GetEligibleLimbs(attacker, enemy, attackType, map).Count == 0)
                    continue;

                var coordinates = enemy.Position!.Coordinates;
                if (!hexHighlights.TryGetValue(coordinates, out var labels))
                {
                    labels = [];
                    hexHighlights[coordinates] = labels;
                }

                labels.Add(GetString($"PhysicalAttackType_{attackType}"));
            }
        }

        if (hexHighlights.Count == 0)
        {
            _viewModel.ClearHighlights();
            return;
        }

        var highlights = new Dictionary<HexCoordinates, IHexHighlightType>(hexHighlights.Count);
        foreach (var (coordinates, labels) in hexHighlights)
            highlights[coordinates] = new AttackReachableHighlight(labels.OrderBy(label => label).ToList());

        _viewModel.HighlightRegions(highlights);
    }

    private void CancelTargetSelection()
    {
        lock (_stateLock)
        {
            if (_currentStep != PhysicalAttackStep.TargetSelection) return;

            SelectedTarget = null;
            Preview = null;
            SelectedAttackType = null;
            ClearEligibleTargets();
            _viewModel.ClearHighlights();
            _viewModel.IsPhysicalAttackPreviewVisible = false;

            CurrentStep = PhysicalAttackStep.ActionSelection;
            _viewModel.NotifyStateChanged();
        }
    }

    private void ResetAttackerDependentState()
    {
        SelectedTarget = null;
        Preview = null;
        SelectedAttackType = null;
        ClearEligibleTargets();
        _viewModel.ClearHighlights();
        _viewModel.IsPhysicalAttackPreviewVisible = false;
    }

    private IReadOnlyList<PartLocation>? GetDefaultLimbs()
    {
        lock (_stateLock)
        {
            if (_selectedAttackType is not { } attackType || _selectedTarget == null) return null;
            if (!_eligibleLimbs.TryGetValue(_selectedTarget, out var limbs) || limbs.Count == 0) return null;

            // A kick uses exactly one leg; a punch can use one arm or both.
            return attackType == PhysicalAttackType.Kick ? [limbs[0]] : limbs.ToList();
        }
    }

    private IReadOnlyList<(string Label, IReadOnlyList<PartLocation> Limbs)> GetDeclarationOptions()
    {
        PhysicalAttackType attackType;
        IReadOnlyList<PartLocation> limbs;
        lock (_stateLock)
        {
            if (_selectedAttackType is not { } selectedType || _selectedTarget == null) return [];
            if (!_eligibleLimbs.TryGetValue(_selectedTarget, out var eligibleLimbs) || eligibleLimbs.Count == 0)
                return [];

            attackType = selectedType;
            limbs = eligibleLimbs;
        }

        var typeName = GetString($"PhysicalAttackType_{attackType}");
        var options = new List<(string Label, IReadOnlyList<PartLocation> Limbs)>(limbs.Count + 1);

        foreach (var limb in limbs)
        {
            var label = string.Format(
                GetString("Action_DeclarePhysicalAttack_WithLimb"),
                typeName,
                GetString($"MechPart_{limb}_Short"));
            options.Add((label, [limb]));
        }

        if (attackType == PhysicalAttackType.Punch && limbs.Count > 1)
        {
            var bothArmsLabel = string.Format(
                GetString("Action_DeclarePhysicalAttack_WithLimb"),
                typeName,
                GetString("PhysicalAttack_BothArms"));
            options.Add((bothArmsLabel, limbs.ToList()));
        }

        return options;
    }

    private PhysicalAttackPreviewViewModel? CreatePreview(IUnit target)
    {
        IUnit? attacker;
        PhysicalAttackType? attackType;
        lock (_stateLock)
        {
            attacker = _attacker;
            attackType = _selectedAttackType;
        }

        if (attacker == null || attackType is not { } type || Game.BattleMap is not { } map)
            return null;

        var targetNumber = Game.PhysicalAttackCalculator
            .GetToHitNumber(attacker, target, type, map);
        var breakdown = Game.PhysicalAttackCalculator
            .GetModifierBreakdown(attacker, target, type, map);

        return new PhysicalAttackPreviewViewModel(
            attacker,
            target,
            type,
            targetNumber,
            breakdown,
            _viewModel.LocalizationService);
    }

    private void Declare(IReadOnlyList<PartLocation> limbs)
    {
        if (!this.CanHumanPlayerAct()) return;

        lock (_stateLock)
        {
            if (_attacker == null || _selectedTarget == null || _selectedAttackType is not { } attackType)
                return;

            var declaration = new PhysicalAttackDeclarationData
            {
                AttackType = attackType,
                AttackerLimbs = limbs.ToList(),
                TargetId = _selectedTarget.Id
            };

            SubmitDeclaration(declaration);
        }
    }

    private void Skip()
    {
        if (!this.CanHumanPlayerAct()) return;

        lock (_stateLock)
        {
            if (_attacker == null) return;
            SubmitDeclaration(null);
        }
    }

    /// <summary>
    /// Sends the declaration (or the no-attack command when <paramref name="declaration"/> is null)
    /// and returns to attacker selection. The server advances the turn order on that command.
    /// </summary>
    private void SubmitDeclaration(PhysicalAttackDeclarationData? declaration)
    {
        IUnit? attacker;
        lock (_stateLock) attacker = _attacker;
        if (attacker == null) return;

        var command = new PhysicalAttackCommand
        {
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = attacker.Id,
            Declaration = declaration
        };

        Game.DeclarePhysicalAttack(command);

        Attacker = null;
        ResetAttackerDependentState();
        AvailableAttackTypes = [];
        CurrentStep = PhysicalAttackStep.SelectingUnit;
        _viewModel.NotifyStateChanged();
    }

    private string GetString(string key) => _viewModel.LocalizationService.GetString(key);
}

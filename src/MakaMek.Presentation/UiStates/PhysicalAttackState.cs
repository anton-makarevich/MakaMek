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
/// The physical attack declaration state: pick the attacker, pick the target, then declare
/// through one dialog that lists every supported attack type. It mirrors
/// <see cref="WeaponsAttackState"/>, but every eligibility decision comes from
/// <see cref="IPhysicalAttackEligibility"/> so the UI cannot disagree with the server about what
/// is legal. Availability and modifiers depend on the target, so the attack type is chosen only
/// after the target is known.
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

    private static readonly IReadOnlyList<PhysicalAttackOptionViewModel> NoOptions = [];

    private readonly BattleMapViewModel _viewModel;
    private readonly Lock _stateLock = new();

    /// <summary>
    /// The enemies the attacker can reach with at least one supported attack type. Replaced as a
    /// whole under the lock, never mutated in place, so a reader always sees a consistent set.
    /// </summary>
    private IReadOnlySet<IUnit> _reachableTargets = new HashSet<IUnit>();

    private IReadOnlyList<PhysicalAttackOptionViewModel> _options = NoOptions;
    private PhysicalAttackStep _currentStep = PhysicalAttackStep.SelectingUnit;
    private PhysicalAttackType? _selectedAttackType;
    private IUnit? _attacker;
    private IUnit? _selectedTarget;

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

    /// <summary>
    /// One row per supported attack type against the selected target. Empty outside the
    /// declaration step.
    /// </summary>
    public IReadOnlyList<PhysicalAttackOptionViewModel> Options
    {
        get
        {
            lock (_stateLock) return _options;
        }
        private set
        {
            lock (_stateLock) _options = value;
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
                    PhysicalAttackStep.TargetSelection => GetString("Action_SelectTarget"),
                    PhysicalAttackStep.Declaration => GetString("Action_SelectPhysicalAttack"),
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
                return _currentStep is PhysicalAttackStep.TargetSelection or PhysicalAttackStep.Declaration
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
                    PhysicalAttackStep.TargetSelection => GetString("Action_SkipAttack"),
                    PhysicalAttackStep.Declaration => _selectedAttackType != null
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
                case PhysicalAttackStep.TargetSelection:
                    Skip();
                    break;
                case PhysicalAttackStep.Declaration:
                    ConfirmDeclaration();
                    break;
            }
        }
    }

    public IUnit? SelectedUnit
    {
        get
        {
            lock (_stateLock)
                return _currentStep == PhysicalAttackStep.Declaration ? _selectedTarget : _attacker;
        }
        private set
        {
            if (value == null) return;

            lock (_stateLock)
            {
                if (!this.CanHumanPlayerAct()) return;
                if (value.IsDestroyed) return;

                switch (_currentStep)
                {
                    case PhysicalAttackStep.SelectingUnit:
                        if (!CanActAsAttacker(value)) return;
                        SetAttacker(value);
                        return;

                    case PhysicalAttackStep.TargetSelection:
                        if (IsFriendly(value))
                        {
                            if (!CanActAsAttacker(value)) return;
                            SetAttacker(value);
                            return;
                        }

                        if (!_reachableTargets.Contains(value)) return;
                        OpenDeclaration(value);
                        return;

                    case PhysicalAttackStep.Declaration:
                        if (!IsFriendly(value) && _reachableTargets.Contains(value))
                        {
                            if (!ReferenceEquals(value, _selectedTarget))
                                OpenDeclaration(value);
                            return;
                        }

                        CloseDeclaration();
                        return;
                }
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

        var unit = FindUnitOnHex(hex);

        switch (CurrentStep)
        {
            case PhysicalAttackStep.SelectingUnit:
                if (unit != null) SelectedUnit = unit;
                return;

            case PhysicalAttackStep.TargetSelection:
                // Friendly units switch the attacker; reachable enemies open the dialog; any other
                // hex is ignored so the attacker and its highlights stay.
                if (unit != null) SelectedUnit = unit;
                return;

            case PhysicalAttackStep.Declaration:
                // Another reachable enemy rebuilds the rows; anything else closes the dialog and
                // returns to target selection without losing the attacker.
                if (unit != null && !IsFriendly(unit) && _reachableTargets.Contains(unit))
                    SelectedUnit = unit;
                else if (unit == null || !ReferenceEquals(unit, _selectedTarget))
                    CloseDeclaration();
                return;
        }
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
            return _currentStep is PhysicalAttackStep.SelectingUnit or PhysicalAttackStep.TargetSelection
                   && CanActAsAttacker(unit);
        }
    }

    public IEnumerable<StateAction> GetAvailableActions()
    {
        lock (_stateLock)
        {
            if (_attacker == null) return [];

            return _currentStep switch
            {
                PhysicalAttackStep.TargetSelection =>
                    [new StateAction(GetString("Action_SkipAttack"), true, Skip)],
                PhysicalAttackStep.Declaration =>
                [
                    new StateAction(
                        _selectedAttackType != null
                            ? GetString("Action_DeclareAttack")
                            : GetString("Action_SkipAttack"),
                        true,
                        ConfirmDeclaration)
                ],
                _ => []
            };
        }
    }

    /// <summary>
    /// Closes the declaration dialog, drops the pending target and rows, and returns to target
    /// selection with the attacker and its reachable-target highlights intact.
    /// </summary>
    public void CloseDeclaration()
    {
        lock (_stateLock)
        {
            if (_currentStep != PhysicalAttackStep.Declaration) return;

            SelectedTarget = null;
            Options = NoOptions;
            SelectedAttackType = null;
            CurrentStep = PhysicalAttackStep.TargetSelection;
            RebuildReachableTargets();
            _viewModel.ClearHighlights();
            HighlightValidTargets();
            _viewModel.NotifyStateChanged();
        }
    }

    private void OpenDeclaration(IUnit target)
    {
        lock (_stateLock)
        {
            SelectedTarget = target;
            Options = BuildOptions(target);
            SelectedAttackType = null;
            CurrentStep = PhysicalAttackStep.Declaration;
            _viewModel.NotifyStateChanged();
        }
    }

    /// <summary>
    /// Builds one row per supported attack type against the selected target. A row is enabled when
    /// the attack has at least one reaching limb and no declaration restriction, including the
    /// displacement budget. Target numbers and breakdowns are computed only for enabled rows.
    /// </summary>
    private IReadOnlyList<PhysicalAttackOptionViewModel> BuildOptions(IUnit target)
    {
        IUnit? attacker;
        lock (_stateLock) attacker = _attacker;

        if (attacker == null || Game.BattleMap is not { } map) return NoOptions;

        var allUnits = GetAllUnits();
        var options = new List<PhysicalAttackOptionViewModel>(SupportedAttackTypes.Count);
        foreach (var definition in PhysicalAttackDefinitions.Supported)
        {
            var attackType = definition.AttackType;
            var limbs = Game.PhysicalAttackEligibility.GetEligibleLimbs(attacker, target, attackType, map);
            var restriction = Game.PhysicalAttackEligibility
                .GetDeclarationRestriction(attacker, target, attackType, map, allUnits);

            var isEnabled = limbs.Count > 0 && restriction is null;
            string? restrictionReason = null;
            if (!isEnabled)
            {
                restrictionReason = restriction is { } value
                    ? GetString($"PhysicalAttackRestriction_{value}")
                    : GetString("PhysicalAttackDeclaration_Unavailable");
            }

            int? targetNumber = null;
            PhysicalAttackBreakdown? breakdown = null;
            if (isEnabled)
            {
                targetNumber = Game.PhysicalAttackCalculator.GetToHitNumber(attacker, target, attackType, map);
                breakdown = Game.PhysicalAttackCalculator
                    .GetModifierBreakdown(attacker, target, attackType, map);
            }

            var variants = BuildLimbVariants(definition, limbs);
            var defaultVariant = variants.FirstOrDefault(variant =>
                variant.Limbs.SequenceEqual(GetDefaultLimbs(attackType, limbs) ?? []));

            options.Add(new PhysicalAttackOptionViewModel(
                attackType,
                variants,
                defaultVariant,
                isEnabled,
                restrictionReason,
                targetNumber,
                breakdown,
                OnOptionSelectionChanged,
                _viewModel.LocalizationService));
        }

        return options;
    }

    private IReadOnlyList<PhysicalAttackLimbVariant> BuildLimbVariants(
        PhysicalAttackDefinition definition, IReadOnlyList<PartLocation> limbs)
    {
        if (limbs.Count == 0) return [];

        var attackType = definition.AttackType;
        var typeName = GetString($"PhysicalAttackType_{attackType}");
        var variants = new List<PhysicalAttackLimbVariant>(limbs.Count + 1);
        string Label(string limb) => string.Format(
            GetString("Action_DeclarePhysicalAttack_WithLimb"), typeName, limb);

        // An attack that needs all of its limbs resolves as one declaration, so Push offers a
        // single "Both Arms" variant instead of one variant per arm.
        if (definition.MinLimbsPerDeclaration > 1)
        {
            if (limbs.Count < definition.MinLimbsPerDeclaration) return [];

            variants.Add(new PhysicalAttackLimbVariant(
                Label(GetString("PhysicalAttack_BothArms")),
                limbs.Take(definition.MinLimbsPerDeclaration).ToList()));
            return variants;
        }

        foreach (var limb in limbs)
            variants.Add(new PhysicalAttackLimbVariant(
                Label(GetString($"MechPart_{limb}_Short")), [limb]));

        if (attackType == PhysicalAttackType.Punch && limbs.Count > 1)
            variants.Add(new PhysicalAttackLimbVariant(
                Label(GetString("PhysicalAttack_BothArms")), limbs.ToList()));

        return variants;
    }

    /// <summary>
    /// A kick uses exactly one leg; a punch and a push use all of their eligible limbs.
    /// </summary>
    private static IReadOnlyList<PartLocation>? GetDefaultLimbs(
        PhysicalAttackType attackType, IReadOnlyList<PartLocation> limbs)
    {
        if (limbs.Count == 0) return null;
        return attackType == PhysicalAttackType.Kick ? [limbs[0]] : limbs.ToList();
    }

    /// <summary>
    /// Keeps exactly one row selected: selecting a row deselects the others first, and a
    /// deselection callback for a row that is not the current selection does nothing.
    /// </summary>
    private void OnOptionSelectionChanged(PhysicalAttackOptionViewModel source, bool isSelected)
    {
        lock (_stateLock)
        {
            if (isSelected)
            {
                foreach (var other in _options.Where(option =>
                             !ReferenceEquals(option, source) && option.IsSelected))
                    other.IsSelected = false;

                SelectedAttackType = source.AttackType;
            }
            else if (_selectedAttackType == source.AttackType)
            {
                SelectedAttackType = null;
            }
        }

        _viewModel.NotifyStateChanged();
    }

    private void SetAttacker(IUnit value)
    {
        if (_attacker != null && !ReferenceEquals(_attacker, value))
            ResetAttackerDependentState();

        Attacker = value;
        RebuildReachableTargets();
        _viewModel.ClearHighlights();
        HighlightValidTargets();
        CurrentStep = PhysicalAttackStep.TargetSelection;
        _viewModel.NotifyStateChanged();
    }

    private void RebuildReachableTargets()
    {
        var reachable = new HashSet<IUnit>();
        if (Attacker is { } attacker && Game.BattleMap is { } map)
        {
            foreach (var enemy in GetEnemies())
            {
                if (SupportedAttackTypes.Any(attackType =>
                        Game.PhysicalAttackEligibility
                            .GetEligibleLimbs(attacker, enemy, attackType, map).Count > 0))
                    reachable.Add(enemy);
            }
        }

        lock (_stateLock) _reachableTargets = reachable;
    }

    private bool CanActAsAttacker(IUnit unit) =>
        !unit.IsDestroyed
        && unit.Owner?.Id == Game.PhaseStepState?.ActivePlayer.Id
        && !unit.HasDeclaredPhysicalAttack
        && HasAnyOption(unit);

    private bool IsFriendly(IUnit unit) => unit.Owner?.Id == Game.PhaseStepState?.ActivePlayer.Id;

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

    private List<IUnit> GetAllUnits() => Game.Players.SelectMany(player => player.Units).ToList();

    private IUnit? FindUnitOnHex(Hex hex) =>
        _viewModel.Units.FirstOrDefault(unit => unit.Position?.Coordinates == hex.Coordinates);

    /// <summary>
    /// Highlights every hex that holds a unit the attacker can reach with any supported attack
    /// type. The labels are the localized attack type names, matching the weapon reach highlight
    /// convention.
    /// </summary>
    private void HighlightValidTargets()
    {
        if (Attacker is not { } attacker || Game.BattleMap is not { } map) return;

        var hexHighlights = new Dictionary<HexCoordinates, HashSet<string>>();

        foreach (var enemy in GetEnemies())
        {
            var labels = new HashSet<string>();
            foreach (var attackType in SupportedAttackTypes)
            {
                if (Game.PhysicalAttackEligibility.GetEligibleLimbs(attacker, enemy, attackType, map).Count == 0)
                    continue;

                labels.Add(GetString($"PhysicalAttackType_{attackType}"));
            }

            if (labels.Count == 0) continue;
            hexHighlights[enemy.Position!.Coordinates] = labels;
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

    private void ResetAttackerDependentState()
    {
        SelectedTarget = null;
        Options = NoOptions;
        SelectedAttackType = null;
        lock (_stateLock) _reachableTargets = new HashSet<IUnit>();
        _viewModel.ClearHighlights();
    }

    private void ConfirmDeclaration()
    {
        PhysicalAttackType? selectedType;
        lock (_stateLock) selectedType = _selectedAttackType;

        if (selectedType is null)
        {
            Skip();
            return;
        }

        DeclareSelected();
    }

    /// <summary>
    /// Revalidates the pending selection before submitting: a stale row, a lost permission or an
    /// eligibility change after the dialog opened must not publish a command.
    /// </summary>
    private void DeclareSelected()
    {
        if (!this.CanHumanPlayerAct()) return;

        IUnit? attacker;
        IUnit? target;
        PhysicalAttackType? attackType;
        PhysicalAttackOptionViewModel? option;
        lock (_stateLock)
        {
            attacker = _attacker;
            target = _selectedTarget;
            attackType = _selectedAttackType;
            option = _options.FirstOrDefault(candidate =>
                candidate.IsSelected && candidate.AttackType == _selectedAttackType);
        }

        if (attacker == null || target == null || attackType is not { } type
            || option?.SelectedVariant?.Limbs is not { Count: > 0 } limbs)
            return;

        if (Game.BattleMap is not { } map) return;

        var eligibleLimbs = Game.PhysicalAttackEligibility.GetEligibleLimbs(attacker, target, type, map);
        if (!limbs.All(eligibleLimbs.Contains)) return;

        if (Game.PhysicalAttackEligibility.GetDeclarationRestriction(
                attacker, target, type, map, GetAllUnits()) is not null)
            return;

        SubmitDeclaration(new PhysicalAttackDeclarationData
        {
            AttackType = type,
            AttackerLimbs = limbs.ToList(),
            TargetId = target.Id
        });
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
        CurrentStep = PhysicalAttackStep.SelectingUnit;
        _viewModel.NotifyStateChanged();
    }

    private string GetString(string key) => _viewModel.LocalizationService.GetString(key);
}

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
public class PhysicalAttackState : IUiState, ICombatSelectionRoles
{
    private static readonly IReadOnlyList<PhysicalAttackType> SupportedAttackTypes =
        PhysicalAttackDefinitions.Supported.Select(definition => definition.AttackType).ToList();

    private readonly BattleMapViewModel _viewModel;
    private readonly Lock _stateLock = new();

    /// <summary>
    /// The limbs the attacker could use against each legal target for the selected attack type.
    /// Targets not in this map cannot be attacked.
    /// </summary>
    private readonly Dictionary<IUnit, IReadOnlyList<PartLocation>> _eligibleLimbs = new();

    public IClientGame Game { get; }

    public PhysicalAttackStep CurrentStep { get; private set; } = PhysicalAttackStep.SelectingUnit;

    public PhysicalAttackType? SelectedAttackType { get; private set; }

    public IUnit? Attacker { get; private set; }

    public IUnit? SelectedTarget { get; private set; }

    public PhysicalAttackPreviewViewModel? Preview { get; private set; }

    /// <summary>
    /// The attack types the selected attacker can actually make against at least one enemy right now.
    /// </summary>
    public IReadOnlyList<PhysicalAttackType> AvailableAttackTypes { get; private set; } = [];

    public PhysicalAttackState(BattleMapViewModel viewModel)
    {
        Game = viewModel.Game! ?? throw new InvalidOperationException("Game is not client game");
        _viewModel = viewModel;
        if (Game.PhaseStepState?.ActivePlayer == null)
        {
            throw new InvalidOperationException("Active player is null");
        }
    }

    public string ActionLabel => CurrentStep switch
    {
        PhysicalAttackStep.SelectingUnit => GetString("Action_SelectUnitToAttack"),
        PhysicalAttackStep.ActionSelection when AvailableAttackTypes.Count == 0 =>
            GetString("PhysicalAttack_NoOptions"),
        PhysicalAttackStep.ActionSelection => GetString("Action_SelectAction"),
        PhysicalAttackStep.TargetSelection => GetString("Action_SelectTarget"),
        _ => string.Empty
    };

    public bool IsActionRequired => this.CanHumanPlayerAct();

    public bool CanExecutePlayerAction =>
        CurrentStep is PhysicalAttackStep.ActionSelection or PhysicalAttackStep.TargetSelection
        && !_viewModel.AreActionsMenuOffMap;

    public string PlayerActionLabel
    {
        get
        {
            if (!IsActionRequired) return string.Empty;
            return CurrentStep switch
            {
                PhysicalAttackStep.ActionSelection => GetString("Action_SkipAttack"),
                PhysicalAttackStep.TargetSelection => GetDefaultLimbs() != null
                    ? GetString("Action_DeclareAttack")
                    : GetString("Action_SkipAttack"),
                _ => string.Empty
            };
        }
    }

    public void ExecutePlayerAction()
    {
        if (!this.CanHumanPlayerAct()) return;

        switch (CurrentStep)
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

    public IUnit? SelectedUnit
    {
        get => CurrentStep == PhysicalAttackStep.TargetSelection ? SelectedTarget : Attacker;
        private set
        {
            if (value == null) return;

            lock (_stateLock)
            {
                if (!this.CanHumanPlayerAct()) return;
                if (value.IsDestroyed) return;

                if (CurrentStep is PhysicalAttackStep.SelectingUnit or PhysicalAttackStep.ActionSelection)
                {
                    if (!CanActAsAttacker(value)) return;

                    if (Attacker != null && !ReferenceEquals(Attacker, value))
                        ResetAttackerDependentState();

                    Attacker = value;
                    UpdateAvailableAttackTypes();
                    HighlightValidTargets(SupportedAttackTypes);
                    CurrentStep = PhysicalAttackStep.ActionSelection;
                    _viewModel.NotifyStateChanged();
                    return;
                }

                if (CurrentStep != PhysicalAttackStep.TargetSelection) return;

                if (!_eligibleLimbs.ContainsKey(value))
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
            if (target != null && _eligibleLimbs.ContainsKey(target))
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
    public bool CanSelectUnit(IUnit? unit) =>
        unit == null || (CurrentStep is PhysicalAttackStep.SelectingUnit or PhysicalAttackStep.ActionSelection
                         && CanActAsAttacker(unit));

    public IEnumerable<StateAction> GetAvailableActions()
    {
        if (Attacker == null) return [];

        var actions = new List<StateAction>();

        if (CurrentStep == PhysicalAttackStep.ActionSelection)
        {
            foreach (var attackType in AvailableAttackTypes)
            {
                var type = attackType;
                actions.Add(new StateAction(
                    GetString($"PhysicalAttackType_{type}"),
                    true,
                    () => SelectAttackType(type)));
            }
        }
        else if (CurrentStep == PhysicalAttackStep.TargetSelection)
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

    private void SelectAttackType(PhysicalAttackType attackType)
    {
        if (!this.CanHumanPlayerAct()) return;

        lock (_stateLock)
        {
            if (Attacker == null) return;

            SelectedAttackType = attackType;
            ComputeEligibleTargets(attackType);
            if (_eligibleLimbs.Count == 0)
            {
                SelectedAttackType = null;
                return;
            }

            HighlightValidTargets([attackType]);
            CurrentStep = PhysicalAttackStep.TargetSelection;
            _viewModel.NotifyStateChanged();
        }
    }

    private void ComputeEligibleTargets(PhysicalAttackType attackType)
    {
        _eligibleLimbs.Clear();
        if (Attacker == null || Game.BattleMap is not { } map) return;

        foreach (var enemy in GetEnemies())
        {
            var limbs = Game.PhysicalAttackEligibility.GetEligibleLimbs(Attacker, enemy, attackType, map);
            if (limbs.Count > 0)
                _eligibleLimbs[enemy] = limbs;
        }
    }

    private void UpdateAvailableAttackTypes()
    {
        if (Attacker == null || Game.BattleMap is not { } map)
        {
            AvailableAttackTypes = [];
            return;
        }

        var enemies = GetEnemies().ToList();
        AvailableAttackTypes = SupportedAttackTypes
            .Where(attackType => enemies.Any(enemy =>
                Game.PhysicalAttackEligibility.GetEligibleLimbs(Attacker, enemy, attackType, map).Count > 0))
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

    private IEnumerable<IUnit> GetEnemies()
    {
        if (Attacker == null) return [];

        return _viewModel.Units.Where(unit =>
            unit.Owner?.Id != Attacker.Owner?.Id
            && !unit.IsDestroyed
            && unit.Position != null);
    }

    private IUnit? FindUnitOnHex(Hex hex) =>
        _viewModel.Units.FirstOrDefault(unit => unit.Position?.Coordinates == hex.Coordinates);

    /// <summary>
    /// Highlights every hex that holds a unit the attacker can reach with any of the given attack types.
    /// The labels are the localized attack type names, matching the weapon reach highlight convention.
    /// </summary>
    private void HighlightValidTargets(IEnumerable<PhysicalAttackType> attackTypes)
    {
        if (Attacker == null || Game.BattleMap is not { } map) return;

        var hexHighlights = new Dictionary<HexCoordinates, HashSet<string>>();
        var enemies = GetEnemies().ToList();

        foreach (var attackType in attackTypes)
        {
            foreach (var enemy in enemies)
            {
                if (Game.PhysicalAttackEligibility.GetEligibleLimbs(Attacker, enemy, attackType, map).Count == 0)
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
            if (CurrentStep != PhysicalAttackStep.TargetSelection) return;

            SelectedTarget = null;
            Preview = null;
            SelectedAttackType = null;
            _eligibleLimbs.Clear();
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
        _eligibleLimbs.Clear();
        _viewModel.ClearHighlights();
        _viewModel.IsPhysicalAttackPreviewVisible = false;
    }

    private IReadOnlyList<PartLocation>? GetDefaultLimbs()
    {
        if (SelectedAttackType is not { } attackType || SelectedTarget == null) return null;
        if (!_eligibleLimbs.TryGetValue(SelectedTarget, out var limbs) || limbs.Count == 0) return null;

        // A kick uses exactly one leg; a punch can use one arm or both.
        return attackType == PhysicalAttackType.Kick ? [limbs[0]] : limbs.ToList();
    }

    private IReadOnlyList<(string Label, IReadOnlyList<PartLocation> Limbs)> GetDeclarationOptions()
    {
        if (SelectedAttackType is not { } attackType || SelectedTarget == null) return [];
        if (!_eligibleLimbs.TryGetValue(SelectedTarget, out var limbs) || limbs.Count == 0) return [];

        var typeName = GetString($"PhysicalAttackType_{attackType}");
        var options = new List<(string Label, IReadOnlyList<PartLocation> Limbs)>();

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
        if (Attacker == null || SelectedAttackType is not { } attackType || Game.BattleMap is not { } map)
            return null;

        var targetNumber = Game.PhysicalAttackCalculator
            .GetToHitNumber(Attacker, target, attackType, map);
        var breakdown = Game.PhysicalAttackCalculator
            .GetModifierBreakdown(Attacker, target, attackType, map);

        return new PhysicalAttackPreviewViewModel(
            Attacker,
            target,
            attackType,
            targetNumber,
            breakdown,
            _viewModel.LocalizationService);
    }

    private void Declare(IReadOnlyList<PartLocation> limbs)
    {
        if (!this.CanHumanPlayerAct()) return;

        lock (_stateLock)
        {
            if (Attacker == null || SelectedTarget == null || SelectedAttackType is not { } attackType)
                return;

            var declaration = new PhysicalAttackDeclarationData
            {
                AttackType = attackType,
                AttackerLimbs = limbs.ToList(),
                TargetId = SelectedTarget.Id
            };

            SubmitDeclaration(declaration);
        }
    }

    private void Skip()
    {
        if (!this.CanHumanPlayerAct()) return;

        lock (_stateLock)
        {
            if (Attacker == null) return;
            SubmitDeclaration(null);
        }
    }

    /// <summary>
    /// Sends the declaration (or the no-attack command when <paramref name="declaration"/> is null)
    /// and returns to attacker selection. The server advances the turn order on that command.
    /// </summary>
    private void SubmitDeclaration(PhysicalAttackDeclarationData? declaration)
    {
        if (Attacker == null) return;

        var command = new PhysicalAttackCommand
        {
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = Attacker.Id,
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

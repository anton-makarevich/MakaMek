using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Presents the physical-attack phase and its safe pass action while punch/kick resolution is added.
/// </summary>
public sealed class PhysicalAttackState : IUiState
{
    private readonly BattleMapViewModel _viewModel;
    private readonly HashSet<Guid> _submittedUnits = [];
    private IUnit? _selectedUnit;

    /// <summary>Initializes the state for the current game phase.</summary>
    public PhysicalAttackState(BattleMapViewModel viewModel)
    {
        _viewModel = viewModel;
        Game = viewModel.Game ?? throw new InvalidOperationException("Game is null");
        if (Game.PhaseStepState?.ActivePlayer == null)
            throw new InvalidOperationException("Active player is null");
    }

    /// <inheritdoc />
    public IClientGame Game { get; }

    /// <inheritdoc />
    public IUnit? SelectedUnit
    {
        get => _selectedUnit;
        private set
        {
            if (_selectedUnit == value) return;
            _selectedUnit = value;
            _viewModel.NotifySelectedUnitChanged();
            _viewModel.NotifyStateChanged();
        }
    }

    /// <inheritdoc />
    public string ActionLabel => SelectedUnit == null
        ? _viewModel.LocalizationService.GetString("Action_SelectUnitForPhysicalAttack")
        : _viewModel.LocalizationService.GetString("Action_SelectPhysicalAttack");

    /// <inheritdoc />
    public bool IsActionRequired => this.IsActiveHumanPlayer() && Game.CanActivePlayerAct;

    /// <inheritdoc />
    public bool CanExecutePlayerAction => SelectedUnit != null && this.CanHumanPlayerAct();

    /// <inheritdoc />
    public string PlayerActionLabel => _viewModel.LocalizationService.GetString("Action_PassPhysicalAttack");

    /// <inheritdoc />
    public bool CanSelectUnit(IUnit? unit) => unit is not null
        && unit.Owner?.Id == Game.PhaseStepState?.ActivePlayer.Id
        && !_submittedUnits.Contains(unit.Id)
        && !unit.IsDestroyed;

    /// <inheritdoc />
    public void HandleUnitSelectionFromList(IUnit? unit)
    {
        if (!this.CanHumanPlayerAct() || !CanSelectUnit(unit)) return;
        SelectedUnit = unit;
    }

    /// <inheritdoc />
    public void HandleHexSelection(Hex hex)
    {
        if (!this.CanHumanPlayerAct()) return;
        var unit = _viewModel.Units.FirstOrDefault(candidate => candidate.Position?.Coordinates == hex.Coordinates);
        HandleUnitSelectionFromList(unit);
    }

    /// <inheritdoc />
    public void HandleFacingSelection(HexDirection direction)
    {
        // Physical attacks do not use a facing selector in the initial slice.
    }

    /// <inheritdoc />
    public IEnumerable<StateAction> GetAvailableActions()
    {
        if (!CanExecutePlayerAction) return [];

        return
        [
            new StateAction(
                _viewModel.LocalizationService.GetString("Action_PassPhysicalAttack"),
                true,
                ExecutePlayerAction)
        ];
    }

    /// <inheritdoc />
    public void ExecutePlayerAction()
    {
        if (!CanExecutePlayerAction || SelectedUnit == null) return;

        var unit = SelectedUnit;
        _submittedUnits.Add(unit.Id);
        SelectedUnit = null;
        _ = Game.PassPhysicalAttack(new PassPhysicalAttackCommand
        {
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = unit.Id
        });
    }
}

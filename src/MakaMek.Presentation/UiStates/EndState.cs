using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Presentation.UiStates;

public class EndState : IUiState
{
    private readonly BattleMapViewModel _viewModel;
    private readonly ILocalizationService _localizationService;

    public IClientGame? Game => _viewModel.Game;

    public EndState(BattleMapViewModel viewModel)
    {
        _viewModel = viewModel;
        _localizationService = viewModel.LocalizationService;
    }

    public string ActionLabel => _localizationService.GetString("EndPhase_ActionLabel");

    public bool IsActionRequired => IsActivePlayer && CanActivePlayerAct;

    public bool CanExecutePlayerAction => IsActivePlayer && CanActivePlayerAct;

    public string PlayerActionLabel => _localizationService.GetString("EndPhase_PlayerActionLabel");

    private bool IsActivePlayer => this.IsActiveHumanPlayer();

    private bool CanActivePlayerAct => _viewModel.Game?.CanActivePlayerAct ?? false;

    public void HandleUnitSelection(IUnit? unit)
    {
        // In EndState, we allow selecting any unit on the map for viewing
        _viewModel.NotifyStateChanged();
    }

    public void HandleHexSelection(Hex hex)
    {
        // Find a unit at the selected hex
        var unit = _viewModel.Units.FirstOrDefault(u => u.Position?.Coordinates == hex.Coordinates);

        // If there's a unit at this hex, select it
        _viewModel.SelectedUnit = unit ??
                                  // If no unit at this hex, deselect the current unit
                                  null;
    }

    public void HandleFacingSelection(HexDirection direction)
    {
        // Not used in EndState
    }

    public IEnumerable<StateAction> GetAvailableActions()
    {
        var actions = new List<StateAction>();

        if (!IsActivePlayer || _viewModel.Game == null || _viewModel.SelectedUnit == null)
            return actions;

        // Only show actions for units belonging to the active player
        var selectedUnit = _viewModel.SelectedUnit;
        var activePlayer = _viewModel.Game.PhaseStepState?.ActivePlayer;

        if (selectedUnit.Owner?.Id == activePlayer?.Id && !selectedUnit.IsDestroyed)
        {
            // Show shutdown action for non-shutdown units
            if (!selectedUnit.IsShutdown)
            {
                actions.Add(new StateAction(
                    _localizationService.GetString("Action_Shutdown"),
                    true,
                    () => ExecuteShutdownAction(selectedUnit)));
            }

            // Show startup action for shutdown mechs
            if (selectedUnit.IsShutdown && selectedUnit is Mech mech)
            {
                var canStartup = CanStartupUnit(mech);
                if (canStartup.canStartup)
                {
                    var actionText = canStartup.probability < 100
                        ? $"{_localizationService.GetString("Action_Startup")} ({canStartup.probability:F0}%)"
                        : _localizationService.GetString("Action_Startup");

                    actions.Add(new StateAction(
                        actionText,
                        true,
                        () => ExecuteStartupAction(selectedUnit)));
                }
            }
        }

        return actions;
    }

    private void ExecuteShutdownAction(IUnit unit)
    {
        if (!IsActivePlayer || _viewModel.Game?.PhaseStepState?.ActivePlayer == null) return;

        var command = new ShutdownUnitCommand
        {
            GameOriginId = _viewModel.Game.Id,
            PlayerId = _viewModel.Game.PhaseStepState.Value.ActivePlayer.Id,
            UnitId = unit.Id,
            Timestamp = DateTime.UtcNow
        };

        if (_viewModel.Game is { } clientGame)
        {
            clientGame.ShutdownUnit(command);
        }
    }

    private (bool canStartup, double probability) CanStartupUnit(Mech mech)
    {
        if (_viewModel.Game == null) return (false, 0);

        // Must be shutdown to startup
        if (!mech.CurrentShutdownData.HasValue)
            return (false, 0);

        var shutdownData = mech.CurrentShutdownData.Value;

        // Can't start up in the same turn as shutdown
        if (shutdownData.Turn >= _viewModel.Game.Turn)
            return (false, 0);

        // Get the avoidance number for the current heat level
        var avoidNumber = _viewModel.Game.HeatEffectsCalculator.GetShutdownAvoidNumber(mech.CurrentHeat);

        // Check if startup is impossible (heat too high)
        if (avoidNumber >= DiceUtils.Impossible2D6Roll)
            return (false, 0);

        // Calculate probability
        var probability = DiceUtils.Calculate2d6Probability(avoidNumber);
        return (true, probability);
    }

    private void ExecuteStartupAction(IUnit unit)
    {
        if (!IsActivePlayer || _viewModel.Game?.PhaseStepState?.ActivePlayer == null) return;

        var command = new StartupUnitCommand
        {
            GameOriginId = _viewModel.Game.Id,
            PlayerId = _viewModel.Game.PhaseStepState.Value.ActivePlayer.Id,
            UnitId = unit.Id,
            Timestamp = DateTime.UtcNow
        };

        if (_viewModel.Game is { } clientGame)
        {
            clientGame.StartupUnit(command);
        }
    }

    /// <summary>
    /// Sends the TurnEndedCommand to end the current player's turn
    /// </summary>
    public void ExecutePlayerAction()
    {
        if (!IsActivePlayer || _viewModel.Game?.PhaseStepState?.ActivePlayer == null) return;

        var command = new TurnEndedCommand
        {
            GameOriginId = _viewModel.Game.Id,
            PlayerId = _viewModel.Game.PhaseStepState.Value.ActivePlayer.Id,
            Timestamp = DateTime.UtcNow
        };

        if (_viewModel.Game is { } clientGame)
        {
            clientGame.EndTurn(command);
        }
    }
}

using AsyncAwaitBestPractices;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Utils;
using Sanet.Localization;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Presentation.UiStates;

public class EndState : IUiState
{
    private readonly BattleMapViewModel _viewModel;
    private readonly ILocalizationService _localizationService;

    public IUnit? SelectedUnit
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            _viewModel.NotifySelectedUnitChanged();
            _viewModel.NotifyStateChanged();
        }
    }

    public IClientGame? Game => _viewModel.Game;

    public EndState(BattleMapViewModel viewModel)
    {
        _viewModel = viewModel;
        _localizationService = viewModel.LocalizationService;
    }

    public string ActionLabel => _localizationService.GetString("EndPhase_ActionLabel");

    public bool IsActionRequired => IsActivePlayer && CanActivePlayerAct;

    public bool CanExecutePlayerAction => (IsActivePlayer && CanActivePlayerAct) || _viewModel.IsGameOver;

    public string PlayerActionLabel => _viewModel.IsGameOver 
        ? _localizationService.GetString("EndPhase_EndGameLabel") 
        : _localizationService.GetString("EndPhase_PlayerActionLabel");

    private bool IsActivePlayer => this.IsActiveHumanPlayer();

    private bool CanActivePlayerAct => _viewModel.Game?.CanActivePlayerAct ?? false;

    public void HandleUnitSelectionFromList(IUnit? unit)
    {
        // In EndState, we allow selecting any unit on the map for viewing
        SelectedUnit = unit;
    }

    public void HandleHexSelection(Hex hex)
    {
        // Find a unit at the selected hex
        var unit = _viewModel.Units.FirstOrDefault(u => u.Position?.Coordinates == hex.Coordinates);

        // If there's a unit at this hex, select it
        SelectedUnit = unit;
    }

    public void HandleFacingSelection(HexDirection direction)
    {
        // Not used in EndState
    }

    public IEnumerable<StateAction> GetAvailableActions()
    {
        var selectedUnit = SelectedUnit;
        if (!CanShowActionsFor(selectedUnit)) return [];

        return BuildUnitActions(selectedUnit!);
    }

    /// <summary>
    /// Actions are only offered for a live unit of the active player while the game is running.
    /// </summary>
    private bool CanShowActionsFor(IUnit? unit)
    {
        if (_viewModel.IsGameOver || !IsActivePlayer || _viewModel.Game == null) return false;

        return unit != null && !unit.IsDestroyed && IsActivePlayersUnit(unit);
    }

    private bool IsActivePlayersUnit(IUnit unit) =>
        unit.Owner?.Id == _viewModel.Game?.PhaseStepState?.ActivePlayer?.Id;

    private List<StateAction> BuildUnitActions(IUnit unit)
    {
        var actions = new List<StateAction>();

        // A running unit can shut down; a shutdown Mech can try to start up instead.
        if (!unit.IsShutdown)
        {
            actions.Add(CreateShutdownAction(unit));
            return actions;
        }

        if (unit is not Mech mech) return actions;

        var startup = CanStartupUnit(mech);
        if (!startup.canStartup) return actions;

        actions.Add(CreateStartupAction(unit, startup.probability));
        return actions;
    }

    private StateAction CreateShutdownAction(IUnit unit) =>
        new(
            _localizationService.GetString("Action_Shutdown"),
            true,
            () => ExecuteShutdownAction(unit));

    private StateAction CreateStartupAction(IUnit unit, double probability)
    {
        var actionText = probability < 100
            ? $"{_localizationService.GetString("Action_Startup")} ({probability:F0}%)"
            : _localizationService.GetString("Action_Startup");

        return new StateAction(actionText, true, () => ExecuteStartupAction(unit));
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
    /// Sends the TurnEndedCommand to end the current player's turn or navigates to end game if over
    /// </summary>
    public void ExecutePlayerAction()
    {
        if (_viewModel.IsGameOver)
        {
            _viewModel.NavigateToEndGame().SafeFireAndForget(ex => Game?.Logger.LogError(ex, "Error navigating to end game"));
            return;
        }

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

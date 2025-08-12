using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Presentation.UiStates;

public class EndState : IUiState
{
    private readonly BattleMapViewModel _viewModel;
    private readonly ILocalizationService _localizationService;

    public EndState(BattleMapViewModel viewModel)
    {
        _viewModel = viewModel;
        _localizationService = viewModel.LocalizationService;
    }

    public string ActionLabel => _localizationService.GetString("EndPhase_ActionLabel");

    public bool IsActionRequired => IsActivePlayer && CanActivePlayerAct;

    public bool CanExecutePlayerAction => IsActivePlayer && CanActivePlayerAct;

    public string PlayerActionLabel => _localizationService.GetString("EndPhase_PlayerActionLabel");

    private bool IsActivePlayer => _viewModel.Game?.ActivePlayer != null && 
                                  _viewModel.Game is { } clientGame &&
                                  clientGame.LocalPlayers.Any(p => p == _viewModel.Game.ActivePlayer.Id);

    private bool CanActivePlayerAct => _viewModel.Game?.CanActivePlayerAct ?? false;

    public void HandleUnitSelection(Unit? unit)
    {
        // In EndState, we allow selecting any unit on the map for viewing
        _viewModel.NotifyStateChanged();
    }

    public void HandleHexSelection(Hex hex)
    {
        // Find unit at the selected hex
        var unit = _viewModel.Units.FirstOrDefault(u => u.Position?.Coordinates == hex.Coordinates);

        // If there's a unit at this hex, select it
        _viewModel.SelectedUnit = unit ??
                                  // If no unit at this hex, deselect current unit
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

        // Only show shutdown action for units belonging to the active player
        var selectedUnit = _viewModel.SelectedUnit;
        var activePlayer = _viewModel.Game.ActivePlayer;

        if (selectedUnit.Owner?.Id == activePlayer?.Id && !selectedUnit.IsDestroyed && !selectedUnit.IsShutdown)
        {
            actions.Add(new StateAction(
                _localizationService.GetString("Action_Shutdown"),
                true,
                () => ExecuteShutdownAction(selectedUnit)));
        }

        return actions;
    }

    private void ExecuteShutdownAction(Unit unit)
    {
        if (!IsActivePlayer || _viewModel.Game == null) return;

        var command = new ShutdownUnitCommand
        {
            GameOriginId = _viewModel.Game.Id,
            PlayerId = _viewModel.Game.ActivePlayer!.Id,
            UnitId = unit.Id,
            Timestamp = DateTime.UtcNow
        };

        if (_viewModel.Game is { } clientGame)
        {
            clientGame.ShutdownUnit(command);
        }
    }

    /// <summary>
    /// Sends the TurnEndedCommand to end the current player's turn
    /// </summary>
    public void ExecutePlayerAction()
    {
        if (!IsActivePlayer || _viewModel.Game == null) return;

        var command = new TurnEndedCommand
        {
            GameOriginId = _viewModel.Game.Id,
            PlayerId = _viewModel.Game.ActivePlayer!.Id,
            Timestamp = DateTime.UtcNow
        };

        if (_viewModel.Game is { } clientGame)
        {
            clientGame.EndTurn(command);
        }
    }
}

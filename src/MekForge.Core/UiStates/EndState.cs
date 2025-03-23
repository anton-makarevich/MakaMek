using Sanet.MekForge.Core.Models.Game;
using Sanet.MekForge.Core.Models.Game.Commands.Client;
using Sanet.MekForge.Core.Models.Map;
using Sanet.MekForge.Core.Models.Units;
using Sanet.MekForge.Core.Services.Localization;
using Sanet.MekForge.Core.ViewModels;

namespace Sanet.MekForge.Core.UiStates;

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

    public bool IsActionRequired => IsActivePlayer;

    public bool CanExecutePlayerAction => true;

    private bool IsActivePlayer => _viewModel.Game?.ActivePlayer != null && 
                                  _viewModel.Game is ClientGame clientGame &&
                                  clientGame.LocalPlayers.Any(p => p.Id == _viewModel.Game.ActivePlayer.Id);

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

        if (_viewModel.Game is ClientGame clientGame)
        {
            clientGame.EndTurn(command);
        }
    }
}

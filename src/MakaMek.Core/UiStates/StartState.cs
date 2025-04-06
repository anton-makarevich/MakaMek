using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.ViewModels;

namespace Sanet.MakaMek.Core.UiStates;

public class StartState : IUiState
{
    private readonly BattleMapViewModel _viewModel;
    private readonly ILocalizationService _localizationService;

    public StartState(BattleMapViewModel viewModel)
    {
        _viewModel = viewModel;
        _localizationService = viewModel.LocalizationService;
    }

    public string ActionLabel => _localizationService.GetString("StartPhase_ActionLabel");

    public bool IsActionRequired => IsLocalPlayerActive;
    
    public bool CanExecutePlayerAction => true;

    public string PlayerActionLabel => _localizationService.GetString("StartPhase_PlayerActionLabel");

    private bool IsLocalPlayerActive => _viewModel.Game is ClientGame { ActivePlayer: not null } clientGame && 
                                        clientGame.LocalPlayers.Any(p => p.Id == clientGame.ActivePlayer.Id);

    public void HandleUnitSelection(Unit? unit)
    {
        // Not applicable in StartState as there are no units yet
    }

    public void HandleHexSelection(Hex hex)
    {
        // Not applicable in StartState
    }

    public void HandleFacingSelection(HexDirection direction)
    {
        // Not applicable in StartState
    }

    /// <summary>
    /// Sets the active local player as ready to play
    /// </summary>
    public void ExecutePlayerAction()
    {
        if (_viewModel.Game is not ClientGame clientGame || clientGame.ActivePlayer == null) return;
        
        // Only set the active player as ready if they are a local player
        if (clientGame.LocalPlayers.All(p => p.Id != clientGame.ActivePlayer.Id)) return;
        var readyCommand = new UpdatePlayerStatusCommand
        {
            GameOriginId = clientGame.Id,
            PlayerId = clientGame.ActivePlayer.Id,
            PlayerStatus = PlayerStatus.Ready,
        };

        clientGame.SetPlayerReady(readyCommand);
    }
}

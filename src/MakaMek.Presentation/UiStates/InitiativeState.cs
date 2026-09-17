using AsyncAwaitBestPractices;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Guides a human player through a manual initiative roll while other players wait.
/// </summary>
public sealed class InitiativeState : IUiState
{
    private readonly BattleMapViewModel _viewModel;
    private readonly ILocalizationService _localizationService;

    /// <summary>
    /// Creates an initiative interaction state for the current battle.
    /// </summary>
    /// <param name="viewModel">Battle map presentation model owning the game.</param>
    public InitiativeState(BattleMapViewModel viewModel)
    {
        _viewModel = viewModel;
        _localizationService = viewModel.LocalizationService;
    }

    public IClientGame? Game => _viewModel.Game;

    public string ActionLabel => IsActionRequired
        ? _localizationService.GetString("Initiative_ActionLabel")
        : _localizationService.GetString("Initiative_WaitingLabel");

    public bool IsActionRequired => this.IsActiveHumanPlayer();

    public bool CanExecutePlayerAction => IsActionRequired;

    public string PlayerActionLabel => _localizationService.GetString("Initiative_RollButton");

    public IEnumerable<StateAction> GetAvailableActions() => IsActionRequired
        ? [new StateAction(PlayerActionLabel, true, ExecutePlayerAction)]
        : [];

    public void ExecutePlayerAction()
    {
        if (!this.CanHumanPlayerAct() || Game?.PhaseStepState?.ActivePlayer is not { } activePlayer)
            return;

        Game.RollInitiative(new RollDiceCommand
        {
            GameOriginId = Game.Id,
            PlayerId = activePlayer.Id
        }).SafeFireAndForget();
        _viewModel.NotifyStateChanged();
    }

    public void HandleUnitSelectionFromList(IUnit? unit) { }
    public void HandleHexSelection(Hex hex) { }
    public void HandleFacingSelection(HexDirection direction) { }
}

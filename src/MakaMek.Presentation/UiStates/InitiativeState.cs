using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Reports the initiative phase while the server rolls it. Initiative is rolled server-side, so
/// there is no player action in this state - it exists to label the phase and let the HUD show the
/// rolls as they arrive.
/// </summary>
public sealed class InitiativeState : IUiState
{
    private readonly BattleMapViewModel _viewModel;
    private readonly ILocalizationService _localizationService;

    /// <summary>
    /// Creates an initiative state for the current battle.
    /// </summary>
    /// <param name="viewModel">Battle map presentation model owning the game.</param>
    public InitiativeState(BattleMapViewModel viewModel)
    {
        _viewModel = viewModel;
        _localizationService = viewModel.LocalizationService;
    }

    public IClientGame? Game => _viewModel.Game;

    public string ActionLabel => _localizationService.GetString("Initiative_WaitingLabel");

    public bool IsActionRequired => false;

    public void HandleUnitSelectionFromList(IUnit? unit) { }
    public void HandleHexSelection(Hex hex) { }
    public void HandleFacingSelection(HexDirection direction) { }
}

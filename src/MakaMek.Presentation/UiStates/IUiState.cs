using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Presentation.UiStates;

public interface IUiState
{
    string ActionLabel { get; }
    bool IsActionRequired { get; }
    bool CanExecutePlayerAction => false;
    string PlayerActionLabel => string.Empty;
    IUnit? SelectedUnit => null;
    void HandleUnitSelectionFromList(IUnit? unit);
    void HandleHexSelection(Hex hex);
    void HandleFacingSelection(HexDirection direction);

    /// <summary>
    /// Whether the UI may select the given unit (e.g. list click or map). States that temporarily lock
    /// selection to one unit can return false for others. Clearing selection (null) is not passed here.
    /// </summary>
    bool CanSelectUnit(IUnit? unit) => true;
    IEnumerable<StateAction> GetAvailableActions() => new List<StateAction>();
    
    /// <summary>
    /// Executes the primary player action for the current state
    /// </summary>
    void ExecutePlayerAction() { }
    
    /// <summary>
    /// The game instance for this state. Null for states that don't require a game (e.g., IdleState).
    /// </summary>
    IClientGame? Game => null;
}
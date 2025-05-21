using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.UiStates;

public interface IUiState
{
    string ActionLabel { get; }
    bool IsActionRequired { get; }
    bool CanExecutePlayerAction => false;
    string PlayerActionLabel => string.Empty;
    void HandleUnitSelection(Unit? unit);
    void HandleHexSelection(Hex hex);
    void HandleFacingSelection(HexDirection direction);
    IEnumerable<StateAction> GetAvailableActions() => new List<StateAction>();
    
    /// <summary>
    /// Executes the primary player action for the current state
    /// </summary>
    void ExecutePlayerAction() { }
}
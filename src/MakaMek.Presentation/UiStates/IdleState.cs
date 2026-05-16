using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Presentation.UiStates;

public class IdleState : IUiState
{
    public void HandleUnitSelectionFromList(IUnit? unit) { }
    public void HandleHexSelection(Hex hex) { }
    public void HandleFacingSelection(HexDirection direction) { }

    public string ActionLabel => "Wait";
    public bool IsActionRequired => false;
}
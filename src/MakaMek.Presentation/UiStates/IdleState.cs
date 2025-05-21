using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.UiStates;

public class IdleState : IUiState
{
    public void HandleUnitSelection(Unit? unit) { }
    public void HandleHexSelection(Hex hex) { }
    public void HandleFacingSelection(HexDirection direction) { }

    public string ActionLabel => "Wait";
    public bool IsActionRequired => false;
}
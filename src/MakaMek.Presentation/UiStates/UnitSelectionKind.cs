namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// The role a unit plays in the current UI state, which decides how its selection ring looks.
/// </summary>
public enum UnitSelectionKind
{
    /// <summary>
    /// The unit is not selected in any way and shows no ring.
    /// </summary>
    None,

    /// <summary>
    /// The unit is the currently selected one.
    /// </summary>
    Selected,

    /// <summary>
    /// The unit is the attacker of the declared weapon attack.
    /// </summary>
    Attacker,

    /// <summary>
    /// The unit is the target of the declared weapon attack.
    /// </summary>
    Target
}

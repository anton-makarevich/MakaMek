using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// A UI state that puts units into combat roles, so the selection ring can tell them apart.
/// </summary>
public interface ICombatSelectionRoles
{
    /// <summary>
    /// The unit that declared the current weapon attack, if any.
    /// </summary>
    IUnit? Attacker { get; }

    /// <summary>
    /// The unit currently picked as a weapon attack target, if any.
    /// </summary>
    IUnit? SelectedTarget { get; }
}

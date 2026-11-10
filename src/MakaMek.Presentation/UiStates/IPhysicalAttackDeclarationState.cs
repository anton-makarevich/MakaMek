using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// A UI state that opens the shared physical attack declaration panel. Both the Physical Attack
/// Phase (<see cref="PhysicalAttackState"/>) and the Movement Phase (a pending Charge or DFA,
/// see <see cref="MovementState"/>) implement it, so the panel is driven by one contract instead of
/// being coupled to one phase.
/// </summary>
public interface IPhysicalAttackDeclarationState
{
    /// <summary>
    /// Whether the declaration panel is open for a target.
    /// </summary>
    bool IsDeclarationOpen { get; }

    /// <summary>
    /// The unit making the declaration, used for the panel header.
    /// </summary>
    IUnit? DeclarationAttacker { get; }

    /// <summary>
    /// The unit the declaration is being made against, or null when the panel is closed.
    /// </summary>
    IUnit? DeclarationTarget { get; }

    /// <summary>
    /// The attack type rows shown by the panel.
    /// </summary>
    IReadOnlyList<PhysicalAttackOptionViewModel> DeclarationOptions { get; }

    /// <summary>
    /// Cancels the pending declaration and returns the state to target selection.
    /// </summary>
    void CloseDeclaration();
}

using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Decides which role a unit plays for the current UI state, which is what the selection ring visualises.
/// </summary>
/// <remarks>
/// The rule lives here rather than in the UI so it can be unit tested: the Avalonia layer is excluded from coverage.
/// </remarks>
public static class UnitSelectionKindResolver
{
    /// <summary>
    /// Resolves the ring role of <paramref name="unit"/> for the given state.
    /// </summary>
    /// <param name="unit">The unit the ring belongs to.</param>
    /// <param name="selected">The currently selected unit, if any.</param>
    /// <param name="state">The current UI state, if any.</param>
    public static UnitSelectionKind Resolve(IUnit unit, IUnit? selected, IUiState? state)
    {
        var roles = state as ICombatSelectionRoles;
        return Resolve(unit, selected, roles?.Attacker, roles?.SelectedTarget);
    }

    /// <summary>
    /// Resolves the ring role of <paramref name="unit"/> from the explicit combat roles.
    /// </summary>
    /// <remarks>
    /// A destroyed unit never gets a ring, the attacker wins over the target and the target wins over a plain selection,
    /// so the most specific meaning is always the one that shows.
    /// </remarks>
    public static UnitSelectionKind Resolve(IUnit unit, IUnit? selected, IUnit? attacker, IUnit? target)
    {
        if (unit.IsOutOfCommission) return UnitSelectionKind.None;

        if (ReferenceEquals(attacker, unit)) return UnitSelectionKind.Attacker;
        if (ReferenceEquals(target, unit)) return UnitSelectionKind.Target;

        return ReferenceEquals(selected, unit) ? UnitSelectionKind.Selected : UnitSelectionKind.None;
    }
}

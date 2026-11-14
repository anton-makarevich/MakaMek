using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// What a selection request does to <see cref="WeaponsAttackState"/> in its current sub-step.
/// </summary>
public enum WeaponsAttackSelectionOutcome
{
    /// <summary>The request is rejected: nothing changes and nothing is notified.</summary>
    Ignore,

    /// <summary>The candidate becomes the attacking unit and the state advances to action selection.</summary>
    SelectAttacker,

    /// <summary>The candidate becomes the target of the current attacker.</summary>
    SelectTarget,

    /// <summary>The candidate is not part of this sub-step: nothing changes, but listeners are notified.</summary>
    NotifyOnly
}

/// <summary>
/// The admissibility rules for picking a unit in the weapons attack phase: an own, live unit that has
/// not declared yet can attack, an enemy inside a weapon's reach can be targeted.
/// </summary>
/// <remarks>
/// The rules live outside the UiState setter so the setter stays a plain decision plus application,
/// and so the decision can be unit tested without an Avalonia view model.
/// </remarks>
public sealed class WeaponsAttackSelectionPolicy
{
    /// <summary>
    /// Decides what selecting <paramref name="candidate"/> means in <paramref name="step"/>.
    /// </summary>
    /// <param name="step">The sub-step the selection is made in.</param>
    /// <param name="candidate">The unit the player picked, if any.</param>
    /// <param name="canAct">Whether the active player may act at all right now.</param>
    /// <param name="activePlayerId">Id of the active player, or <c>null</c> when unknown.</param>
    /// <param name="weaponRanges">The current per-weapon reachable hexes.</param>
    public WeaponsAttackSelectionOutcome Decide(
        WeaponsAttackStep step,
        IUnit? candidate,
        bool canAct,
        Guid? activePlayerId,
        IEnumerable<HashSet<HexCoordinates>> weaponRanges)
    {
        if (!canAct || candidate is not { } unit || unit.IsDestroyed)
            return WeaponsAttackSelectionOutcome.Ignore;

        if (step is WeaponsAttackStep.SelectingUnit or WeaponsAttackStep.ActionSelection)
        {
            return CanSelectAttacker(unit, activePlayerId)
                ? WeaponsAttackSelectionOutcome.SelectAttacker
                : WeaponsAttackSelectionOutcome.Ignore;
        }

        if (step == WeaponsAttackStep.TargetSelection)
        {
            return CanSelectTarget(unit, activePlayerId, weaponRanges)
                ? WeaponsAttackSelectionOutcome.SelectTarget
                : WeaponsAttackSelectionOutcome.Ignore;
        }

        // A sub-step that does not take unit selections (weapon configuration) leaves the selection
        // untouched, but the state still repaints.
        return WeaponsAttackSelectionOutcome.NotifyOnly;
    }

    private static bool CanSelectAttacker(IUnit candidate, Guid? activePlayerId) =>
        candidate.Owner?.Id == activePlayerId
        && !candidate.HasDeclaredWeaponAttack;

    private static bool CanSelectTarget(
        IUnit candidate,
        Guid? activePlayerId,
        IEnumerable<HashSet<HexCoordinates>> weaponRanges) =>
        candidate.Owner?.Id != activePlayerId
        && candidate.Position is { } position
        && weaponRanges.Any(range => range.Contains(position.Coordinates));
}

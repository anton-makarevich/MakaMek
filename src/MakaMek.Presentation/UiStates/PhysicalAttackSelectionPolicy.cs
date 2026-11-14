using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// What a selection request does to <see cref="PhysicalAttackState"/> in its current sub-step.
/// </summary>
public enum PhysicalAttackSelectionOutcome
{
    /// <summary>The request is rejected: nothing changes.</summary>
    Ignore,

    /// <summary>The candidate becomes the attacker and the state moves on to target selection.</summary>
    SelectAttacker,

    /// <summary>The candidate becomes the declared-on target and the dialog opens.</summary>
    OpenDeclaration,

    /// <summary>The request is not a reachable enemy, so the declaration dialog closes.</summary>
    CloseDeclaration
}

/// <summary>
/// The admissibility rules for picking a unit in the physical attack phase: friendly units become the
/// attacker, reachable enemies become the target, and anything else closes an open declaration.
/// </summary>
/// <remarks>
/// The rules live outside the UiState setter so the setter stays a plain decision plus application,
/// and so the decision can be unit tested without an Avalonia view model.
/// </remarks>
public sealed class PhysicalAttackSelectionPolicy
{
    /// <summary>
    /// Decides what selecting <paramref name="candidate"/> means in <paramref name="step"/>.
    /// </summary>
    /// <param name="step">The sub-step the selection is made in.</param>
    /// <param name="candidate">The unit the player picked, if any.</param>
    /// <param name="canAct">Whether the active player may act at all right now.</param>
    /// <param name="isFriendly">Whether the candidate belongs to the active player.</param>
    /// <param name="canActAsAttacker">Whether the candidate can still declare an attack this turn.</param>
    /// <param name="isReachable">Whether the candidate is one of the attacker's reachable targets.</param>
    /// <param name="isCurrentTarget">Whether the candidate is already the declared-on target.</param>
    public PhysicalAttackSelectionOutcome Decide(
        PhysicalAttackStep step,
        IUnit? candidate,
        bool canAct,
        bool isFriendly,
        bool canActAsAttacker,
        bool isReachable,
        bool isCurrentTarget)
    {
        if (!canAct || candidate is null || candidate.IsDestroyed)
            return PhysicalAttackSelectionOutcome.Ignore;

        return step switch
        {
            PhysicalAttackStep.SelectingUnit => ChooseAttacker(canActAsAttacker),
            PhysicalAttackStep.TargetSelection => isFriendly
                ? ChooseAttacker(canActAsAttacker)
                : ChooseTarget(isReachable),
            PhysicalAttackStep.Declaration => ChooseDeclarationChange(isFriendly, isReachable, isCurrentTarget),
            _ => PhysicalAttackSelectionOutcome.Ignore
        };
    }

    private static PhysicalAttackSelectionOutcome ChooseAttacker(bool canActAsAttacker) =>
        canActAsAttacker
            ? PhysicalAttackSelectionOutcome.SelectAttacker
            : PhysicalAttackSelectionOutcome.Ignore;

    private static PhysicalAttackSelectionOutcome ChooseTarget(bool isReachable) =>
        isReachable
            ? PhysicalAttackSelectionOutcome.OpenDeclaration
            : PhysicalAttackSelectionOutcome.Ignore;

    /// <summary>
    /// While the dialog is open another reachable enemy rebuilds it, the current target is a no-op,
    /// and any other unit closes the dialog without losing the attacker.
    /// </summary>
    private static PhysicalAttackSelectionOutcome ChooseDeclarationChange(
        bool isFriendly,
        bool isReachable,
        bool isCurrentTarget)
    {
        if (!isFriendly && isReachable)
        {
            return isCurrentTarget
                ? PhysicalAttackSelectionOutcome.Ignore
                : PhysicalAttackSelectionOutcome.OpenDeclaration;
        }

        return PhysicalAttackSelectionOutcome.CloseDeclaration;
    }
}

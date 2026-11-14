using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Builds the action-menu entries for the physical-attack steps. Kept out of
/// <see cref="PhysicalAttackState"/> so the state does not depend on the action row type.
/// </summary>
public static class PhysicalAttackActionBuilder
{
    public static IEnumerable<StateAction> Build(
        PhysicalAttackStep step,
        bool hasAttacker,
        bool hasSelectedAttackType,
        string skipLabel,
        string declareLabel,
        Action skip,
        Action confirmDeclaration)
    {
        if (!hasAttacker) return [];

        return step switch
        {
            PhysicalAttackStep.TargetSelection =>
                [new StateAction(skipLabel, true, skip)],
            PhysicalAttackStep.Declaration =>
            [
                new StateAction(
                    hasSelectedAttackType ? declareLabel : skipLabel,
                    true,
                    confirmDeclaration)
            ],
            _ => []
        };
    }
}

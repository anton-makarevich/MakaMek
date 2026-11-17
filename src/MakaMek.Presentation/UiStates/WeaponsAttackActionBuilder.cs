namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Builds the action-menu entries for the weapons-attack steps. Kept out of
/// <see cref="WeaponsAttackState"/> so the state does not depend on the action row type.
/// </summary>
public static class WeaponsAttackActionBuilder
{
    public static IEnumerable<StateAction> Build(
        WeaponsAttackStep step,
        bool hasAttacker,
        bool attackerIsImmobile,
        bool canFireWeapons,
        bool hasSelectedWeapons,
        string skipLabel,
        string turnTorsoLabel,
        string selectTargetLabel,
        string declareLabel,
        Action skip,
        Action? turnTorso,
        Action selectTarget)
    {
        if (!hasAttacker) return [];

        var actions = new List<StateAction>();

        if (step == WeaponsAttackStep.ActionSelection)
        {
            // Add skip attack action
            actions.Add(new StateAction(skipLabel, true, skip));

            if (attackerIsImmobile) return actions;

            // A unit with a pending Charge or DFA cannot fire, but it may still twist its torso:
            // charge alignment follows the feet, so a twist does not affect the attack
            // (Total Warfare, p. 148).
            if (turnTorso != null)
            {
                actions.Add(new StateAction(turnTorsoLabel, true, turnTorso));
            }

            if (canFireWeapons)
            {
                // Add target selection action
                actions.Add(new StateAction(selectTargetLabel, true, selectTarget));
            }
        }
        else if (step == WeaponsAttackStep.TargetSelection)
        {
            // Add confirm weapon selections action
            actions.Add(new StateAction(
                hasSelectedWeapons ? declareLabel : skipLabel,
                true,
                skip));
        }

        return actions;
    }
}

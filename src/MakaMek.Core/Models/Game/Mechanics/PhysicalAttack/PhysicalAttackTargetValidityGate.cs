using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Re-checks a declared attack against the eligibility rules at resolution time: the target is
/// alive and adjacent, the limb still reaches it (arcs, torso twist, elevation) and can still be
/// used.
/// </summary>
public class PhysicalAttackTargetValidityGate(IPhysicalAttackEligibility eligibility) : IPhysicalAttackResolutionGate
{
    public bool ShouldSkip(PhysicalAttackContext context, ILogger logger)
    {
        if (eligibility.CanResolve(context.Attacker, context.Target, context.AttackType, context.AttackerLimb,
                context.Map))
            return false;

        logger.LogInformation(
            "Skipping {AttackType} by {AttackerName} with {Limb} against {TargetName}: the target can no longer be attacked",
            context.AttackType, context.Attacker.Name, context.AttackerLimb, context.Target.Name);
        return true;
    }
}

using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Models.Logger;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Re-checks a declared attack against the eligibility rules at resolution time. The eligibility
/// policy owns both sides: the attacker still has to be a standing, conscious, not already
/// committed Mech, and the target has to be alive and adjacent with a limb that still reaches it
/// (arcs, torso twist, elevation, water).
/// </summary>
public class PhysicalAttackTargetValidityGate(IPhysicalAttackEligibility eligibility) : IPhysicalAttackResolutionGate
{
    public bool ShouldSkip(PhysicalAttackContext context, ILogger logger)
    {
        if (eligibility.CanResolve(context.Attacker, context.Target, context.AttackType, context.AttackerLimb,
                context.Map))
            return false;

        logger.LogSkippingOutdatedPhysicalAttack(
            context.AttackType, context.Attacker.Name, context.AttackerLimb, context.Target.Name);
        return true;
    }
}

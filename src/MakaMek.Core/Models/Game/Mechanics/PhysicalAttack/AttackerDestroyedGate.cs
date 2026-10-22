using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// A destroyed attacker makes no physical attack.
/// </summary>
public class AttackerDestroyedGate : IPhysicalAttackResolutionGate
{
    public bool ShouldSkip(PhysicalAttackContext context, ILogger logger)
    {
        if (!context.Attacker.IsDestroyed) return false;

        logger.LogInformation("Skipping {AttackType} by {AttackerName} with {Limb}: the attacker is destroyed",
            context.AttackType, context.Attacker.Name, context.AttackerLimb);
        return true;
    }
}

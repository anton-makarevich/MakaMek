using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// An attacker that fell after declaring, for example in the weapon attack resolution phase,
/// makes no physical attack.
/// </summary>
public class AttackerProneGate : IPhysicalAttackResolutionGate
{
    public bool ShouldSkip(PhysicalAttackContext context, ILogger logger)
    {
        if (context.Attacker is not Mech { IsProne: true }) return false;

        logger.LogInformation("Skipping {AttackType} by {AttackerName} with {Limb}: the attacker is prone",
            context.AttackType, context.Attacker.Name, context.AttackerLimb);
        return true;
    }
}

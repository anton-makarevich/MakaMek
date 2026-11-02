using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Shared damage calculation for a physical attack limb. Both the resolver (which applies the
/// damage to the target) and the calculator (which previews it in the declaration dialog) use
/// this, so the preview and the resolved damage cannot drift apart.
/// </summary>
internal static class PhysicalAttackDamageCalculator
{
    public static int Calculate(IRulesProvider rules, IUnit attacker, PhysicalAttackType attackType,
        PartLocation limb)
    {
        var limbComponents = attacker
            .GetComponentsAtLocation(limb)
            .Select(component => new LimbComponentState(component.ComponentType, component.IsDestroyed))
            .ToList();
        var reducingActuators = rules.GetPhysicalAttackReducingActuators(attackType, limbComponents);
        return rules.GetPhysicalAttackDamage(attacker.Tonnage, attackType, reducingActuators);
    }
}

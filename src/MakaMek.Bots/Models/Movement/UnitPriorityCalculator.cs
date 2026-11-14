using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Bots.Models.Movement;

/// <summary>
/// Role- and initiative-based movement priority rules.
/// </summary>
public sealed class UnitPriorityCalculator : IUnitPriorityCalculator
{
    public double CalculatePriority(IUnit unit, MovementPhaseState state)
    {
        double priority = 0;

        // 1. Role Score
        var role = unit.GetTacticalRole();

        // Handle Fallen/Prone units
        if (unit is Mech { IsProne: true })
        {
            return 0; // Always last
        }

        priority += role switch
        {
            UnitTacticalRole.LrmBoat => 90,
            UnitTacticalRole.Scout => 20,
            UnitTacticalRole.Jumper => 25,
            UnitTacticalRole.Brawler => 30,
            _ => 50 // Default for others
        };

        // Check for 0 movement options (if detectable easily, for now relying on roles)
        // If we want to strictly follow "Units with 0 movement options: 80", we'd need to check reachability.
        // Skipping expensive check for now as per "0 for now will be introduced later" for situation modifier,
        // effectively treating "0 options" as a situation.

        // 2. Initiative Modifier
        if (state.EnemyUnitsRemaining == 0)
        {
            // We are moving last/late (no enemies left to move)
            priority += 30;
        }
        else
        {
            // Enemies still have moves
            if (role == UnitTacticalRole.Brawler)
            {
                priority -= 30;
            }
        }

        return priority;
    }
}

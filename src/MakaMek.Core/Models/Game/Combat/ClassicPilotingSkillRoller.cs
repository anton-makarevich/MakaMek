using Sanet.MakaMek.Core.Models.Game.Combat.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Combat.Modifiers.PilotingSkill;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Combat;

/// <summary>
/// Classic BattleTech implementation of piloting skill roll calculator
/// </summary>
public class ClassicPilotingSkillRoller : IPilotingSkillRoller
{
    /// <summary>
    /// Gets a detailed breakdown of all modifiers affecting the piloting skill roll
    /// </summary>
    /// <param name="unit">The unit making the piloting skill roll</param>
    /// <returns>A breakdown of the piloting skill roll calculation</returns>
    public PsrBreakdown GetPsrBreakdown(Unit unit)
    {
        if (unit.Crew == null)
        {
            throw new ArgumentException("Unit has no crew", nameof(unit));
        }

        var modifiers = new List<RollModifier>();
        
        // Add damaged gyro modifier if applicable
        if (unit is Mech mech)
        {
            // Check for damaged gyro
            var gyroHits = GetGyroHits(mech);
            if (gyroHits > 0)
            {
                modifiers.Add(new DamagedGyroModifier
                {
                    Value = gyroHits, // Each hit adds a +1 modifier
                    HitsCount = gyroHits
                });
            }
        }

        return new PsrBreakdown
        {
            BasePilotingSkill = unit.Crew.Piloting,
            Modifiers = modifiers
        };
    }

    /// <summary>
    /// Gets the number of hits on the mech's gyro
    /// </summary>
    /// <param name="mech">The mech to check</param>
    /// <returns>The number of hits on the gyro</returns>
    private int GetGyroHits(Mech mech)
    {
        var gyro = mech.GetAllComponents<Gyro>().FirstOrDefault();
        return gyro?.Hits ?? 0;
    }
}

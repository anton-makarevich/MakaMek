using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

/// <summary>
/// Builds the attack modifiers a 'Mech carries from heat, being prone or skidding, damaged
/// sensors and arm actuator critical hits. Extracted from <see cref="Mech"/> so the mech only
/// exposes the values and this class owns the component knowledge.
/// </summary>
internal static class MechAttackModifierCalculator
{
    /// <summary>
    /// Gets all attack penalties currently affecting the mech.
    /// </summary>
    public static IReadOnlyList<RollModifier> Calculate(Mech mech, PartLocation location)
    {
        var penalties = new List<RollModifier>();

        // Heat attack penalty
        if (mech.AttackHeatPenalty != null)
        {
            penalties.Add(mech.AttackHeatPenalty);
        }

        // Prone firing penalty
        if (mech.IsProne)
        {
            penalties.Add(new ProneAttackerModifier
            {
                Value = ProneAttackerModifier.DefaultValue // +2 modifier for firing while prone
            });
        }

        // Skidding penalty
        if (mech.IsSkidding)
        {
            penalties.Add(new SkiddingAttackerModifier
            {
                Value = SkiddingAttackerModifier.DefaultValue // +1 modifier for skidding
            });
        }

        // Add sensor hit modifier for Mechs
        var sensors = mech.GetAllComponents<Sensors>().FirstOrDefault(s => s.Hits > 0);
        if (sensors != null)
        {
            var sensorsHitModifier = SensorHitModifier.Create(sensors.Hits);
            if (sensorsHitModifier != null) penalties.Add(sensorsHitModifier);
        }

        // Arm critical hit modifiers
        var armCriticalModifiers = GetArmCriticalHitModifiers(mech, location);
        penalties.AddRange(armCriticalModifiers);

        return penalties;
    }

    /// <summary>
    /// Gets all arm critical hit modifiers for the mech.
    /// </summary>
    private static IEnumerable<RollModifier> GetArmCriticalHitModifiers(Mech mech, PartLocation location)
    {
        // Check the requested arm
        var arm = mech.Parts.Values.OfType<Arm>()
            .FirstOrDefault(a => a.Location == location && a.IsDestroyed == false);
        if (arm == null) return [];

        var modifiers = new List<RollModifier>();

        // Check for shoulder actuator first (overrides other arm modifiers)
        var shoulder = arm.GetComponents<ShoulderActuator>().FirstOrDefault(sh => sh.IsDestroyed);
        if (shoulder != null)
        {
            modifiers.Add(new ShoulderActuatorHitModifier
            {
                ArmLocation = arm.Location,
                Value = 4 // +4 modifier for destroyed shoulder
            });
            // Skip other checks for this arm as per rules
            return modifiers;
        }

        // Check upper arm actuator
        var upperArm = arm.GetComponents<UpperArmActuator>().FirstOrDefault(a => a.IsDestroyed);
        if (upperArm != null)
        {
            modifiers.Add(new UpperArmActuatorHitModifier
            {
                ArmLocation = arm.Location,
                Value = 1 // +1 modifier for destroyed upper arm actuator
            });
        }

        // Check lower arm actuator
        var lowerArm = arm.GetComponents<LowerArmActuator>().FirstOrDefault(a => a.IsDestroyed);
        if (lowerArm != null)
        {
            modifiers.Add(new LowerArmActuatorHitModifier
            {
                ArmLocation = arm.Location,
                Value = 1 // +1 modifier for destroyed lower arm actuator
            });
        }

        return modifiers;
    }
}

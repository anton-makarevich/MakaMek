using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

/// <summary>
/// Builds the movement penalties a 'Mech carries from heat, destroyed legs and actuators, and the
/// movement reduction those penalties imply. Extracted from <see cref="Mech"/> so the mech only
/// exposes the values and this class owns the component knowledge.
/// </summary>
internal static class MechMovementModifierCalculator
{
    /// <summary>
    /// Gets all movement penalties currently affecting the mech.
    /// </summary>
    public static IReadOnlyList<RollModifier> Calculate(Mech mech, int baseMovement)
    {
        var penalties = new List<RollModifier>();

        // Heat movement penalty
        var heatPenalty = mech.MovementHeatPenalty;
        if (heatPenalty != null)
        {
            penalties.Add(heatPenalty);
        }

        // Leg destruction penalty
        var destroyedLegs = mech.Parts.Values.OfType<Leg>().Count(p => p.IsDestroyed);
        var legDestructionPenalty = LegDestroyedPenalty.Create(destroyedLegs, baseMovement);
        if (legDestructionPenalty != null)
        {
            penalties.Add(legDestructionPenalty);
            return penalties;
        }

        // Hip actuator penalty
        var destroyedHips = mech.GetAllComponents<HipActuator>().Count(a => a.IsDestroyed);
        var hipDestroyedPenalty = HipDestroyedPenalty.Create(destroyedHips, baseMovement);
        if (hipDestroyedPenalty != null)
        {
            penalties.Add(hipDestroyedPenalty);
        }

        // Foot actuator penalty
        var destroyedFoot = mech.GetAllComponents<FootActuator>().Count(a => a.IsDestroyed);
        if (destroyedFoot > 0)
        {
            penalties.Add(new FootActuatorMovementPenalty
            {
                DestroyedCount = destroyedFoot,
                Value = destroyedFoot
            });
        }

        // Lower leg actuator penalty
        var destroyedLowerLeg = mech.GetAllComponents<LowerLegActuator>().Count(a => a.IsDestroyed);
        if (destroyedLowerLeg > 0)
        {
            penalties.Add(new LowerLegActuatorMovementPenalty
            {
                DestroyedCount = destroyedLowerLeg,
                Value = destroyedLowerLeg
            });
        }

        // Upper leg actuator penalty
        var destroyedUpperLeg = mech.GetAllComponents<UpperLegActuator>().Count(a => a.IsDestroyed);
        if (destroyedUpperLeg > 0)
        {
            penalties.Add(new UpperLegActuatorMovementPenalty
            {
                DestroyedCount = destroyedUpperLeg,
                Value = destroyedUpperLeg
            });
        }

        return penalties;
    }

    /// <summary>
    /// Reduces the base movement of the mech by the destroyed legs, hips and actuator penalties.
    /// </summary>
    public static int GetDamageReducedMovement(Mech mech, int baseMovement)
    {
        var movementPenalties = mech.MovementModifiers;
        var destroyedLegsPenalty = movementPenalties.OfType<LegDestroyedPenalty>().FirstOrDefault();
        if (destroyedLegsPenalty != null) return 2 - destroyedLegsPenalty.DestroyedLegCount; // This will reduce movement to 1 for one destroyed leg

        var destroyedHipsPenalty = movementPenalties.OfType<HipDestroyedPenalty>().FirstOrDefault();
        // If both hips are destroyed, movement is 0
        if (destroyedHipsPenalty is { DestroyedHipCount: >= 2 })
            return 0; // This will reduce movement to 0

        var hipModifiedMovement = baseMovement - (destroyedHipsPenalty?.Value ?? 0);

        // Apply actuator penalties (excluding hip penalties which are handled above)
        var actuatorPenalties = mech.MovementModifiers
            .Where(p => p is FootActuatorMovementPenalty
                or LowerLegActuatorMovementPenalty
                or UpperLegActuatorMovementPenalty)
            .Sum(p => p.Value);

        return Math.Max(0, hipModifiedMovement - actuatorPenalties);
    }
}


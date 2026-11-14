using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.HeatPenalties;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Pilots;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

/// <summary>
/// Applies a 'Mech's heat rules: engine heat sinks, water dissipation, the engine damage penalty,
/// life-support pilot damage and the heat movement/attack penalties. Extracted from
/// <see cref="Mech"/> so the mech only exposes the values and this class owns the rules.
/// </summary>
internal static class MechHeatEffects
{
    /// <summary>
    /// Gets the heat penalty caused by engine damage.
    /// </summary>
    public static EngineHeatPenalty? GetEngineHeatPenalty(Mech mech) =>
        mech.GetAllComponents<Engine>().FirstOrDefault()?.HeatPenalty;

    /// <summary>
    /// Number of heat sinks carried by the mech's engine.
    /// </summary>
    public static int GetEngineHeatSinks(Mech mech) =>
        mech.GetAllComponents<Engine>().FirstOrDefault()?.NumberOfHeatSinks ?? 0;

    /// <summary>
    /// Calculates how many extra heat points the submerged heat sinks dissipate.
    /// </summary>
    public static int GetWaterHeatDissipationBonus(Mech mech, IRulesProvider rulesProvider)
    {
        var cap = rulesProvider.GetWaterHeatDissipationCap();
        var bonus = 0;
        foreach (var heatSink in mech.GetAvailableComponents<HeatSink>())
        {
            if (heatSink.IsSubmerged)
            {
                bonus += heatSink.HeatDissipation;
                if (bonus >= cap)
                    return cap;
            }
        }

        var engine = mech.GetComponentsAtLocation<Engine>(PartLocation.CenterTorso).FirstOrDefault();
        if (engine is { NumberOfHeatSinks: > 0, IsSubmerged: true })
        {
            bonus += engine.NumberOfHeatSinks;
        }

        return Math.Min(bonus, cap);
    }

    /// <summary>
    /// Applies the pilot damage caused by a destroyed life support system. Heat shutdown is
    /// handled by <c>HeatEffectsCalculator</c> in the heat phase.
    /// </summary>
    public static void ApplyHeatEffects(Mech mech)
    {
        var lifeSupport = mech.GetAllComponents<LifeSupport>().FirstOrDefault(ls => ls.IsDestroyed);
        if (lifeSupport != null && mech.CurrentHeat >= 15 && mech.Pilot is MechWarrior mw)
        {
            var hits = 1;
            if (mech.CurrentHeat >= 26)
            {
                hits++;
            }
            mw.Hit(hits);
        }
    }

    /// <summary>
    /// Calculates the movement penalty based on current heat.
    /// </summary>
    public static HeatMovementPenalty? GetMovementHeatPenalty(int currentHeat)
    {
        if (currentHeat < 5) return null;
        var heatPenaltyValue = currentHeat switch
        {
            >= 25 => 5,
            >= 20 => 4,
            >= 15 => 3,
            >= 10 => 2,
            >= 5 => 1,
            _ => 0
        };
        return new HeatMovementPenalty
        {
            HeatLevel = currentHeat,
            Value = heatPenaltyValue
        };
    }

    /// <summary>
    /// Calculates the attack penalty based on current heat.
    /// </summary>
    public static HeatRollModifier? GetAttackHeatPenalty(int currentHeat)
    {
        if (currentHeat < 8) return null;

        var heatPenaltyValue = currentHeat switch
        {
            >= 24 => 4,
            >= 17 => 3,
            >= 13 => 2,
            >= 8 => 1,
            _ => 0
        };

        return new HeatRollModifier
        {
            HeatLevel = currentHeat,
            Value = heatPenaltyValue
        };
    }
}

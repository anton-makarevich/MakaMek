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
/// life-support pilot damage and the heat movement/attack penalties. Exposed as behavior on
/// <see cref="Mech"/> so the mech only carries the values.
/// </summary>
internal static class MechHeatExtensions
{
    extension(Mech mech)
    {
        /// <summary>
        /// Gets the heat penalty caused by engine damage.
        /// </summary>
        public EngineHeatPenalty? GetEngineHeatPenalty() =>
            mech.GetAllComponents<Engine>().FirstOrDefault()?.HeatPenalty;

        /// <summary>
        /// Number of heat sinks carried by the mech's engine.
        /// </summary>
        public int GetEngineHeatSinkCount() =>
            mech.GetAllComponents<Engine>().FirstOrDefault()?.NumberOfHeatSinks ?? 0;

        /// <summary>
        /// Calculates how many extra heat points the submerged heat sinks dissipate.
        /// </summary>
        public int CalculateWaterHeatDissipationBonus(IRulesProvider rulesProvider)
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
        public void ApplyHeatDamage()
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
    }

    /// <summary>
    /// Calculates the movement penalty based on current heat.
    /// </summary>
    internal static HeatMovementPenalty? GetMovementHeatPenalty(int currentHeat)
    {
        if (currentHeat < 5) return null;

        // The guard above excludes every value below 5, so the wildcard is the first tier:
        // 5-9 heat subtracts 1 Walking MP (Total Warfare, p. 160).
        var heatPenaltyValue = currentHeat switch
        {
            >= 25 => 5,
            >= 20 => 4,
            >= 15 => 3,
            >= 10 => 2,
            _ => 1
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
    internal static HeatRollModifier? GetAttackHeatPenalty(int currentHeat)
    {
        if (currentHeat < 8) return null;

        // The guard above excludes every value below 8, so the wildcard is the first tier:
        // 8-12 heat adds 1 to weapon to-hit numbers (Total Warfare, p. 160).
        var heatPenaltyValue = currentHeat switch
        {
            >= 24 => 4,
            >= 17 => 3,
            >= 13 => 2,
            _ => 1
        };

        return new HeatRollModifier
        {
            HeatLevel = currentHeat,
            Value = heatPenaltyValue
        };
    }
}

using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

/// <summary>
/// Piloting-skill-roll modifiers contributed by a single leg and its actuators.
/// </summary>
public static class LegPilotingSkillModifierExtensions
{
    extension(Leg leg)
    {
        /// <summary>
        /// Adds the modifiers for the destroyed actuators of this leg (hip, upper leg, lower leg,
        /// foot). A destroyed leg as a whole is handled by the caller.
        /// </summary>
        public void AddActuatorPilotingSkillModifiers(List<RollModifier> modifiers, IRulesProvider rules)
        {
            var hipActuator = leg.GetComponent<HipActuator>();
            if (hipActuator?.IsDestroyed == true)
            {
                modifiers.Add(new HipActuatorHitModifier
                {
                    Value = rules.GetPilotingSkillRollModifier(PilotingSkillRollType.HipActuatorHit)
                });
            }

            var upperLegActuator = leg.GetComponent<UpperLegActuator>();
            if (upperLegActuator?.IsDestroyed == true)
            {
                modifiers.Add(new UpperLegActuatorHitModifier
                {
                    Value = rules.GetPilotingSkillRollModifier(PilotingSkillRollType.UpperLegActuatorHit)
                });
            }

            var lowerLegActuator = leg.GetComponent<LowerLegActuator>();
            if (lowerLegActuator?.IsDestroyed == true)
            {
                modifiers.Add(new LowerLegActuatorHitModifier
                {
                    Value = rules.GetPilotingSkillRollModifier(PilotingSkillRollType.LowerLegActuatorHit)
                });
            }

            var footActuator = leg.GetComponent<FootActuator>();
            if (footActuator?.IsDestroyed == true)
            {
                modifiers.Add(new FootActuatorHitModifier
                {
                    Value = rules.GetPilotingSkillRollModifier(PilotingSkillRollType.FootActuatorHit)
                });
            }
        }
    }
}

using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Maps the physical-attack PSR requirement to the roll context that carries its modifiers.
/// </summary>
internal static class PilotingSkillRollContextExtensions
{
    extension(PilotingSkillRollType rollType)
    {
        public PilotingSkillRollContext ToContext() => rollType switch
        {
            PilotingSkillRollType.KickHit => new KickHitRollContext(),
            PilotingSkillRollType.KickMiss => new KickMissRollContext(),
            PilotingSkillRollType.PushHit => new PushHitRollContext(),
            PilotingSkillRollType.ChargeImpact => new ChargeImpactRollContext(),
            PilotingSkillRollType.DfaTargetImpact => new DfaTargetImpactRollContext(),
            PilotingSkillRollType.DfaAttackerImpact => new DfaAttackerImpactRollContext(),
            _ => throw new NotSupportedException($"No piloting skill roll context is defined for {rollType}")
        };
    }
}

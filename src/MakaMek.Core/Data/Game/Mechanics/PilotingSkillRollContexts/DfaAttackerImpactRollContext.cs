namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

/// <summary>
/// Piloting skill roll context for the attacker after a successful Death From Above. The roll is
/// made at +4 (Total Warfare, p. 150). Landing in water adds the standard water-entry modifier to
/// the same roll.
/// </summary>
/// <param name="WaterDepth">
/// The depth of the water the attacker landed in, or 0 when the landing hex is dry.
/// </param>
public record DfaAttackerImpactRollContext(int WaterDepth = 0)
    : PilotingSkillRollContext(PilotingSkillRollType.DfaAttackerImpact);

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

/// <summary>
/// Piloting skill roll context for a 'Mech whose kick missed
/// </summary>
public record KickMissRollContext() : PilotingSkillRollContext(PilotingSkillRollType.KickMiss);

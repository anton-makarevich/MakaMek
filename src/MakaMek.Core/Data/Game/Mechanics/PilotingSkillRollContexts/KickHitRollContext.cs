namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

/// <summary>
/// Piloting skill roll context for a 'Mech that was hit by a kick
/// </summary>
public record KickHitRollContext() : PilotingSkillRollContext(PilotingSkillRollType.KickHit);

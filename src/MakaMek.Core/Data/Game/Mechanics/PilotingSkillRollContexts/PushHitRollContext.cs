namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

/// <summary>
/// Piloting skill roll context for a 'Mech that was successfully pushed.
/// </summary>
public record PushHitRollContext() : PilotingSkillRollContext(PilotingSkillRollType.PushHit);

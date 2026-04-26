namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

/// <summary>
/// Piloting skill roll context for determining pilot damage from a fall
/// </summary>
public record PilotDamageFromFallRollContext(int LevelsFallen) : PilotingSkillRollContext(PilotingSkillRollType.PilotDamageFromFall);

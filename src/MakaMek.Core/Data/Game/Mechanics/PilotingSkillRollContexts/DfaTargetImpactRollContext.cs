namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

/// <summary>
/// Piloting skill roll context for the target of a successful Death From Above. The roll is made
/// at +2 (Total Warfare, p. 150).
/// </summary>
public record DfaTargetImpactRollContext() : PilotingSkillRollContext(PilotingSkillRollType.DfaTargetImpact);

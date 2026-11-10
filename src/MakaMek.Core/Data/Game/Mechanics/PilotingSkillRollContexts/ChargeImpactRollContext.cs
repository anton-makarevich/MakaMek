namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

/// <summary>
/// Piloting skill roll context for a 'Mech involved in a successful charge. Both the attacker and
/// the target make this roll at +2 (Total Warfare, p. 148).
/// </summary>
public record ChargeImpactRollContext() : PilotingSkillRollContext(PilotingSkillRollType.ChargeImpact);

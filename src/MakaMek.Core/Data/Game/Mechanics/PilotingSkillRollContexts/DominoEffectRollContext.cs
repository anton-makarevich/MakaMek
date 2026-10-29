namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

/// <summary>
/// The mandatory roll of a 'Mech displaced by a domino effect (Total Warfare, p. 151-153).
/// </summary>
public record DominoEffectRollContext() : PilotingSkillRollContext(PilotingSkillRollType.DominoEffect);

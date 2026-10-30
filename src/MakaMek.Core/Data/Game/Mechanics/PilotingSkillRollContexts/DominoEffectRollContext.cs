namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

/// <summary>
/// The mandatory roll of a 'Mech displaced by a domino effect (Total Warfare, p. 151-153).
/// </summary>
/// <param name="WaterDepth">
/// Depth of the water the Mech was displaced into, when it landed on a ground surface in a water
/// hex (0 when dry). The standard water-depth modifier is part of this roll, not an extra one.
/// </param>
public record DominoEffectRollContext(int WaterDepth = 0)
    : PilotingSkillRollContext(PilotingSkillRollType.DominoEffect);

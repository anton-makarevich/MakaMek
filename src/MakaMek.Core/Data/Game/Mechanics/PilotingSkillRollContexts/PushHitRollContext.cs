namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

/// <summary>
/// Piloting skill roll context for a 'Mech that was successfully pushed.
/// </summary>
/// <param name="WaterDepth">
/// The depth of the water the push displaced the target into, or 0 when it did not enter water.
/// Entering water is not a separate roll: it adds the standard water-depth modifier to this roll.
/// </param>
public record PushHitRollContext(int WaterDepth = 0)
    : PilotingSkillRollContext(PilotingSkillRollType.PushHit);

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

/// <summary>
/// Piloting skill roll context for entering deep water
/// </summary>
public record EnteringDeepWaterRollContext(int WaterDepth) : PilotingSkillRollContext(PilotingSkillRollType.WaterEntry);

using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

/// <summary>
/// Automatic fall after a missed Death From Above. The attacker takes damage as from a two-level
/// fall and every hit location is resolved on the rear column, so the fall uses a fixed facing
/// roll instead of rolling the dice (Total Warfare, p. 150).
/// </summary>
/// <param name="LevelsFallen">The number of levels the attacker falls.</param>
/// <param name="FacingDiceRoll">The fixed facing roll that makes every hit location rear.</param>
/// <param name="FacingAfterFall">The facing after the fall, derived from the fixed roll.</param>
public record DfaFallRollContext(
    int LevelsFallen,
    DiceResult FacingDiceRoll,
    HexDirection FacingAfterFall)
    : PilotingSkillRollContext(PilotingSkillRollType.DfaFall);

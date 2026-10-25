using Sanet.MakaMek.Core.Data.Game.Mechanics;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

/// <summary>
/// One piloting skill roll a physical attack requires, and the participant who has to make it.
/// A list of these replaces the former kick-specific booleans so later attack types can add
/// obligations without changing the data contract again.
/// </summary>
/// <param name="Recipient">The unit that has to roll.</param>
/// <param name="RollType">The reason for the roll, which also selects the PSR context.</param>
public record PhysicalAttackPsrRequirement(
    PhysicalAttackParticipant Recipient,
    PilotingSkillRollType RollType);

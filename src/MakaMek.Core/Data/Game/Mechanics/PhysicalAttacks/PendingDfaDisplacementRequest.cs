namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

/// <summary>
/// The client-side mirror of an outstanding DFA displacement request. The presentation state uses
/// it to highlight the candidates and the bot uses it to answer for its own unit.
/// </summary>
/// <param name="RequestId">The id the selection must carry.</param>
/// <param name="AttackerId">The unit that made the Death From Above.</param>
/// <param name="TargetUnitId">The unit that has to be displaced.</param>
/// <param name="OwnerPlayerId">The player whose controller chooses the destination.</param>
/// <param name="Candidates">The legal adjacent destinations.</param>
public record PendingDfaDisplacementRequest(
    Guid RequestId,
    Guid AttackerId,
    Guid TargetUnitId,
    Guid OwnerPlayerId,
    IReadOnlyList<PhysicalAttackDisplacementCandidateData> Candidates);

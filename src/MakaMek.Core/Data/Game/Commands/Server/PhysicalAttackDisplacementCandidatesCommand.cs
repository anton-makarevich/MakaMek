using Sanet.Localization;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

/// <summary>
/// Asks the target's controller to choose where a unit displaced by a missed Death From Above
/// moves. The server publishes this only when more than one candidate exists; exactly one
/// candidate auto-resolves without an interaction.
/// </summary>
public record struct PhysicalAttackDisplacementCandidatesCommand : IGameCommand
{
    /// <summary>Identifies this one request. The selection must carry the same id.</summary>
    public required Guid RequestId { get; init; }

    /// <summary>The unit that made the Death From Above.</summary>
    public required Guid AttackerId { get; init; }

    /// <summary>The unit that has to be displaced.</summary>
    public required Guid TargetUnitId { get; init; }

    /// <summary>The player whose controller chooses the destination.</summary>
    public required Guid OwnerPlayerId { get; init; }

    /// <summary>The legal adjacent destinations, in stable direction order.</summary>
    public required IReadOnlyList<PhysicalAttackDisplacementCandidateData> Candidates { get; init; }

    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var owner = game.Players.FirstOrDefault(p => p.Id == command.OwnerPlayerId);
        var target = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == command.TargetUnitId);
        var attacker = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == command.AttackerId);

        if (owner == null || target == null || attacker == null)
            return string.Empty;

        return string.Format(
            localizationService.GetString("Command_DfaDisplacementCandidates"),
            owner.Name,
            attacker.Model,
            target.Model,
            command.Candidates.Count);
    }
}

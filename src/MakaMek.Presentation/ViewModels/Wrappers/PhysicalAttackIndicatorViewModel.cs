using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// A physical attack that has been declared and not yet fully resolved, as the map has to draw it:
/// one badge on the edge between the attacker and its target.
/// </summary>
/// <remarks>
/// The limbs still to resolve are part of the record so a two-arm punch keeps its badge until both
/// arms are resolved, and a repeated resolution command for an arm that is already gone leaves the
/// record unchanged.
/// </remarks>
public record PhysicalAttackIndicatorViewModel
{
    public required Guid AttackerId { get; init; }
    public required Guid TargetId { get; init; }
    public required PhysicalAttackType AttackType { get; init; }
    public required HexCoordinates From { get; init; }
    public required HexCoordinates To { get; init; }
    public required string AttackerTint { get; init; }
    public required IReadOnlyList<PartLocation> PendingLimbs { get; init; }
}

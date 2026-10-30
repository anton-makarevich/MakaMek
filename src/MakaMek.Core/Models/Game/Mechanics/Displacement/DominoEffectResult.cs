using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;

/// <summary>
/// The calculated result of a domino effect. Commands are ordered far-to-near so that each one
/// moves a unit into a hex that is already empty when it is applied.
/// </summary>
public sealed record DominoEffectResult(
    DominoEffectOutcome Outcome,
    IReadOnlyList<DisplaceUnitCommand> Commands,
    IReadOnlyList<IUnit> PsrCandidates,
    HexSurface EnteringSurface = HexSurface.Ground,
    DisplacementFailureReason? Failure = null,
    HexCoordinates? BlockedAt = null)
{
    /// <summary>
    /// Automatic falls caused by the chain: a displaced unit that dropped two or more levels into
    /// an empty hex falls after the displacement (Total Warfare p. 151-152).
    /// </summary>
    public IReadOnlyList<DominoFall> Falls { get; init; } = [];

    /// <summary>
    /// Whether <see cref="Commands"/> can be applied: the chain resolved fully, or it displaced some
    /// units and then stopped at a link that is impossible (no rewind of the accepted links).
    /// </summary>
    public bool HasDisplacements => Outcome is DominoEffectOutcome.Resolved or DominoEffectOutcome.PartiallyResolved;

    public static DominoEffectResult NotRequired() =>
        new(DominoEffectOutcome.NotRequired, [], []);
}

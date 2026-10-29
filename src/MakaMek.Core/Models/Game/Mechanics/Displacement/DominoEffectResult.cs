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
    public static DominoEffectResult NotRequired() =>
        new(DominoEffectOutcome.NotRequired, [], []);
}

using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

/// <summary>
/// Tags a 'Mech's movement path with the events the rules record on it (standup attempt, fall).
/// Extracted from <see cref="Mech"/> so the mech does not need to know the path and segment event
/// types just to annotate its own movement.
/// </summary>
internal static class MechMovementTags
{
    public static MovementPath CreateSingleSegmentPath(HexPosition position, MovementType movementType) =>
        MovementPath.CreateSingleSegmentPath(position, movementType);

    public static MovementPath TagStandupAttempt(MovementPath path, int standupCost) =>
        path.WithLastSegmentEvent(
            new SegmentEvent(SegmentEventType.StandupAttempt),
            new StandUpAttemptMovementCost { Value = standupCost });

    public static MovementPath? TagFall(MovementPath? path) =>
        path?.WithLastSegmentEvent(new SegmentEvent(SegmentEventType.Fall));
}

using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

/// <summary>
/// Builds the command and action list a failed skid produces: the truncated move path with the
/// skid segments appended, and the ordered fall/skid actions. Extracted from
/// <see cref="SkidInterruptResolver"/> so the resolver only decides when a skid happens.
/// </summary>
internal static class SkidActionBuilder
{
    /// <summary>
    /// Builds the original command truncated at the skid segment, with the skid segments appended
    /// and the given events tagged on the truncated last segment.
    /// </summary>
    public static MoveUnitCommand BuildModifiedMoveCommand(
        MovementInterruptContext context,
        IEnumerable<PathSegment> additionalSegments,
        params SegmentEventType[] lastSegmentEvents)
    {
        var truncatedPath = new MovementPath(
            context.MoveCommand.MovementPath.Take(context.SegmentIndex + 1).ToList(),
            context.MoveCommand.MovementType);

        foreach (var eventType in lastSegmentEvents)
            truncatedPath = truncatedPath.WithLastSegmentEvent(new SegmentEvent(eventType));

        var allSegments = truncatedPath.Segments
            .Select(s => s.ToData())
            .Concat(additionalSegments.Select(s => s.ToData()))
            .ToList();

        var modifiedPath = new MovementPath(allSegments, context.MoveCommand.MovementType);

        return context.MoveCommand with
        {
            MovementPath = modifiedPath.ToData(),
            IsCompleted = true,
            GameOriginId = context.Game.Id
        };
    }

    /// <summary>
    /// Builds the ordered actions of a failed skid: the completed move, the fall, the optional
    /// skid damage and the optional cliff fall.
    /// </summary>
    public static List<IGameAction> BuildFallActions(
        MoveUnitAction moveAction,
        Mech mech,
        FallContextData skidFallContext,
        FallContextData? cliffFallContext = null)
    {
        var actions = new List<IGameAction>
        {
            moveAction,
            new ApplyFallAction(mech, skidFallContext.ToMechFallCommand())
        };

        var skidCommand = skidFallContext.ToMechSkidCommand();
        if (skidCommand != null)
            actions.Add(new ApplySkidAction(mech, skidCommand.Value));

        if (cliffFallContext != null)
            actions.Add(new ApplyFallAction(mech, cliffFallContext.ToMechFallCommand()));

        return actions;
    }
}

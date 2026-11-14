using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

/// <summary>
/// Runs the piloting-skill rolls a skid calls for: the skid PSR itself, and the cliff fall the
/// skid path may trigger. Extracted from <see cref="SkidInterruptResolver"/> so the resolver only
/// decides what a failed skid means for the move.
/// </summary>
internal sealed class SkidFallResolver
{
    /// <summary>
    /// Rolls the skid PSR. The fall happens before the move is applied, so the fall position is
    /// the hex the unit started the skid turn in.
    /// </summary>
    public FallContextData ResolveSkidFall(
        MovementInterruptContext context,
        Mech mech,
        PathSegmentData segment,
        SkidPathResult skidResult,
        int hexesMoved)
    {
        var skidContext = new SkidCheckRollContext(skidResult.Segments.Count, hexesMoved);
        return context.Game.FallProcessor.ProcessMovementAttempt(
            mech, skidContext, context.Game, context.MoveCommand.MovementType,
            new HexPosition(segment.From));
    }

    /// <summary>
    /// Rolls the cliff fall that follows a failed skid, keeping the facing resolved by the skid
    /// fall so both falls land on the same facing.
    /// </summary>
    public FallContextData ResolveCliffFall(
        MovementInterruptContext context,
        Mech mech,
        SkidPathResult skidResult,
        FallContextData skidFallContext)
    {
        var facingDiceRoll = skidFallContext.FallingDamageData!.FacingDiceRoll;
        var facingAfterFall = skidFallContext.FallingDamageData.FacingAfterFall;

        return context.Game.FallProcessor.ProcessMovementAttempt(
            mech,
            new CliffFallRollContext(skidResult.LevelsFallen, facingDiceRoll, facingAfterFall),
            context.Game,
            context.MoveCommand.MovementType,
            new HexPosition(
                skidResult.Segments[^1].To.Coordinates,
                skidResult.Segments[^1].To.Facing,
                skidResult.Segments[^1].To.Surface));
    }
}

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

/// <summary>
/// Evaluates a single movement segment for a specific hazard type.
/// Returns null if the hazard doesn't apply to this segment.
/// </summary>
public interface IMovementInterruptHandler
{
    MovementInterruptResult? Check(MovementInterruptContext context);
}

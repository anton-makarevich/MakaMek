namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

/// <summary>
/// Interrupts a running 'Mech that turns on hard pavement. The skid path, the skid PSR and the
/// fall/skid consequences live in <see cref="SkidInterruptExtensions"/>; this handler only
/// dispatches to them.
/// </summary>
public class SkidInterruptHandler : IMovementInterruptHandler
{
    public MovementInterruptResult? Check(MovementInterruptContext context) => context.CheckSkidInterrupt();
}

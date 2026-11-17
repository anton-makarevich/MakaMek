namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

/// <summary>
/// Interrupts a running 'Mech that turns on hard pavement. The skid path, the skid PSR and the
/// fall/skid consequences live in <see cref="SkidInterruptResolver"/>; this handler only
/// dispatches to it.
/// </summary>
public class SkidInterruptHandler : IMovementInterruptHandler
{
    private readonly SkidInterruptResolver _resolver;

    public SkidInterruptHandler()
        : this(new SkidInterruptResolver())
    {
    }

    internal SkidInterruptHandler(SkidInterruptResolver resolver)
    {
        _resolver = resolver;
    }

    public MovementInterruptResult? Check(MovementInterruptContext context) => _resolver.Check(context);
}

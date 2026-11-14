using Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

/// <summary>
/// Interrupts a move whose segment enters a collapsing bridge. The collision, PSR and damage steps
/// live in <see cref="BridgeCollapseResolver"/>; this handler only dispatches to it.
/// </summary>
public class BridgeCollapseInterruptHandler : IMovementInterruptHandler
{
    private readonly BridgeCollapseResolver _resolver;

    public BridgeCollapseInterruptHandler(DominoEffectResolver dominoEffectResolver)
        : this(new BridgeCollapseResolver(dominoEffectResolver))
    {
    }

    internal BridgeCollapseInterruptHandler(BridgeCollapseResolver resolver)
    {
        _resolver = resolver;
    }

    public MovementInterruptResult? Check(MovementInterruptContext context) => _resolver.Check(context);
}

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;

/// <summary>
/// How a domino-effect calculation ended.
/// </summary>
public enum DominoEffectOutcome
{
    /// <summary>The destination does not exceed the stacking limit, so nothing is displaced.</summary>
    NotRequired,

    /// <summary>The chain resolved and <see cref="DominoEffectResult.Commands"/> can be applied.</summary>
    Resolved,

    /// <summary>A link is impossible; the whole chain is cancelled (atomic).</summary>
    Blocked,

    /// <summary>The destination is two or more levels lower and occupied: a separate rule path.</summary>
    AccidentalFallFromAbove
}
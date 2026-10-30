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

    /// <summary>
    /// The chain displaced at least one unit and then hit a link that is impossible. The
    /// displacements already accepted stay applied (no rewind), the blocked unit keeps its hex and
    /// now shares it with the unit displaced into it, so both owe a domino PSR.
    /// </summary>
    PartiallyResolved,

    /// <summary>
    /// The very first link is impossible, so nothing has moved: the entry is blocked and the whole
    /// chain is cancelled (atomic).
    /// </summary>
    Blocked,

    /// <summary>The destination is two or more levels lower and occupied: a separate rule path.</summary>
    AccidentalFallFromAbove
}
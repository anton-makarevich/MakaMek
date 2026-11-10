namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

/// <summary>
/// The outcome of a charge at resolution time (Total Warfare, p. 148).
/// </summary>
public enum ChargeOutcome
{
    /// <summary>The charge hit the unit in the committed destination hex.</summary>
    Hit,

    /// <summary>The charge missed; the attacker lands in an adjacent side hex.</summary>
    Miss,

    /// <summary>
    /// The charge automatically failed: the attacker could not perform it, or the target no longer
    /// occupies the destination hex and no valid replacement is there.
    /// </summary>
    AutoFail
}

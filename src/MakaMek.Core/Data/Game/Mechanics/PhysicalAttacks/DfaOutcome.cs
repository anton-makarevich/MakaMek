namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

/// <summary>
/// The outcome of a Death From Above resolution.
/// </summary>
public enum DfaOutcome
{
    /// <summary>The attack hit: the target was damaged and displaced, and the attacker landed.</summary>
    Hit,

    /// <summary>
    /// The attack missed: the attacker fell into the target hex and the target was displaced by
    /// its controller (Total Warfare, p. 150).
    /// </summary>
    Miss,

    /// <summary>
    /// The attack could not resolve (the target left the declared hex or the attacker could not
    /// reach its approach position). The attacker lands in its approach hex and nothing is damaged.
    /// </summary>
    AutoFail,

    /// <summary>
    /// The attack missed and the target had no legal adjacent hex to be displaced into. The
    /// attacker is destroyed (Total Warfare, p. 150).
    /// </summary>
    AttackerDestroyedNoLanding
}

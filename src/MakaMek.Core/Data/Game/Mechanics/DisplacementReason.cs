namespace Sanet.MakaMek.Core.Data.Game.Mechanics;

public enum DisplacementReason
{
    /// <summary>
    /// A unit leaves a hex because another unit has been displaced into it and the stacking limit is
    /// exceeded (Total Warfare p. 57, p. 152). A domino effect never describes the first displacement
    /// an action causes — that one keeps the action's own reason. The domino starts only once a
    /// displacement enters an occupied hex.
    /// </summary>
    DominoEffect,

    /// <summary>A successful push moved the target one hex away.</summary>
    Push,

    /// <summary>The pushing unit advanced into the hex its target left.</summary>
    PushAdvance,

    /// <summary>A successful charge displaced the target one hex in the charge direction.</summary>
    Charge,

    /// <summary>The charging unit advanced into the hex its target left, or into a failed charge's destination.</summary>
    ChargeAdvance,

    /// <summary>A missed charge placed the charging unit in a side hex of its forward arc.</summary>
    ChargeMiss,

    /// <summary>A Death From Above displaced its target one hex along the jump line, on a hit or a miss.</summary>
    DfaTarget,

    /// <summary>
    /// A unit is forced out of the hex where the Death From Above attacker grounds because the
    /// landing exceeds the stacking limit there (Total Warfare p. 57, p. 152). This is the first
    /// link of the domino chain, not the attack itself: the DFA displaces its target with
    /// <see cref="DfaTarget"/>, and the target hex is normally vacated before the attacker lands.
    /// The reason therefore labels a stacking displacement, not a direct effect of the attack.
    /// </summary>
    DfaLandingOccupant
}

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

    /// <summary>A successful Death From Above displaced its target one hex along the jump line.</summary>
    DfaTarget,

    /// <summary>The Death From Above attacker landed in the target's vacated hex.</summary>
    DfaAttackerLanding,

    /// <summary>A missed Death From Above placed the attacker in the target hex before its fall.</summary>
    DfaMissLanding,

    /// <summary>A failed Death From Above placed the attacker in the declared target hex.</summary>
    DfaAutoFailLanding,

    /// <summary>
    /// The unit occupying the Death From Above landing hex is knocked out by the landing itself. The
    /// landing is the attack, so this is the first displacement the attack causes and not a domino
    /// effect; the units displaced after it as the stacking limit cascades keep <see cref="DominoEffect"/>.
    /// </summary>
    DfaLandingOccupant
}

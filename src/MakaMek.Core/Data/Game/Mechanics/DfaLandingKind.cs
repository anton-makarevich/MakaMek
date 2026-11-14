namespace Sanet.MakaMek.Core.Data.Game.Mechanics;

/// <summary>
/// Why a Death From Above attacker grounded. The attacker always completes the jump it declared
/// during the Movement Phase; the values distinguish the attack outcome that ended the pending move.
/// </summary>
public enum DfaLandingKind
{
    /// <summary>The attack hit and the target left the hex, so the attacker completes its jump.</summary>
    AttackerLanding,

    /// <summary>The attack missed; the attacker completes its jump before the two-level fall.</summary>
    MissLanding,

    /// <summary>The attack automatically failed; the attacker still completes its jump.</summary>
    AutoFailLanding
}
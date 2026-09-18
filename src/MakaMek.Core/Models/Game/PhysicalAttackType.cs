namespace Sanet.MakaMek.Core.Models.Game;

/// <summary>Identifies a physical attack declaration.</summary>
public enum PhysicalAttackType
{
    /// <summary>Uses the attacking Mech's arms.</summary>
    Punch,
    /// <summary>Uses the attacking Mech's legs.</summary>
    Kick,
    /// <summary>Attempts to displace an adjacent target.</summary>
    Push,
    /// <summary>Charges a target after a qualifying movement path.</summary>
    Charge,
    /// <summary>Death From Above jump attack.</summary>
    DFA
}

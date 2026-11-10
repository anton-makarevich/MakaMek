namespace Sanet.MakaMek.Core.Data.Game.Mechanics;

public enum DisplacementReason
{
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
    ChargeMiss
}

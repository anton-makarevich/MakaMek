namespace Sanet.MakaMek.Core.Data.Game.Mechanics;

public enum DisplacementReason
{
    DominoEffect,

    /// <summary>A successful push moved the target one hex away.</summary>
    Push,

    /// <summary>The pushing unit advanced into the hex its target left.</summary>
    PushAdvance
}

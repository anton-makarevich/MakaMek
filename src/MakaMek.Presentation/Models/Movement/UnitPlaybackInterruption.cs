namespace Sanet.MakaMek.Presentation.Models.Movement;

/// <summary>
/// Tells unit controls that a unit's authoritative placement changed outside of the animated move
/// path (displacement, skid, fall, stand-up) or that every playback must stop (game change/detach).
/// </summary>
public readonly record struct UnitPlaybackInterruption(Guid UnitId)
{
    /// <summary>Interrupts playback for every unit; used when the game or the view is replaced.</summary>
    public static UnitPlaybackInterruption ResetAll => new(Guid.Empty);

    /// <summary>True when this notification targets all units.</summary>
    public bool IsResetAll => UnitId == Guid.Empty;
}

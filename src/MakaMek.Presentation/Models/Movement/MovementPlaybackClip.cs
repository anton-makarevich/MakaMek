namespace Sanet.MakaMek.Presentation.Models.Movement;

/// <summary>The clip a movement should play while a <see cref="MovementPlaybackTimeline"/> is running.</summary>
public enum MovementPlaybackClip
{
    /// <summary>No sprite clip: the unit translates without a walk/run animation (jump, prone, missing clips).</summary>
    None,

    Walk,

    Run
}
namespace Sanet.MakaMek.Presentation.Models.Movement;

/// <summary>
/// One sampled frame of a movement playback. <see cref="SignedDistance"/> is cumulative: it grows while
/// the unit walks forward and shrinks while it reverses, which is what drives the sprite frame phase.
/// </summary>
public readonly record struct MovementPlaybackSample(
    PlaybackPoint Position,
    double FacingDegrees,
    double SignedDistance,
    MovementPlaybackClip Clip);
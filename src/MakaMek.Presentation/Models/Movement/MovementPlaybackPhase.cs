namespace Sanet.MakaMek.Presentation.Models.Movement;

/// <summary>
/// A phase of the playback: one translation or one turn with its own slot on the clock.
/// </summary>
public sealed record MovementPlaybackPhase(
    double StartMilliseconds,
    double EndMilliseconds,
    PlaybackPoint From,
    PlaybackPoint To,
    double FromFacingDegrees,
    double ToFacingDegrees,
    bool IsTurn,
    bool IsReversed,
    double SignedDistanceAtStart,
    double SignedDistanceAtEnd)
{
    public double DurationMilliseconds => EndMilliseconds - StartMilliseconds;
}
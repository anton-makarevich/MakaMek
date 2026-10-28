using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Presentation.Models.Movement;

/// <summary>
/// Turns a received <c>MoveUnitCommand</c> path into a deterministic timeline driven by a single
/// 0…1 progress value. The timeline is pure presentation data: the unit model remains authoritative
/// and nothing here mutates it.
/// </summary>
/// <remarks>
/// The total duration is fixed, independent of the number of hexes, mirroring the board game where a
/// turn is the same amount of time and the distance travelled expresses the unit's speed. Turns get a
/// small fixed slot each; translation time is distributed in proportion to distance so every leg moves
/// at the same speed. Frame phase is derived from distance, not from elapsed time, so the feet stay
/// planted whatever the path length is.
/// </remarks>
public sealed class MovementPlaybackTimeline
{
    /// <summary>Every command plays for the same amount of time, whatever the path length.</summary>
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(5);

    /// <summary>Clock slot for one 60° turn step.</summary>
    private const double TurnStepMilliseconds = 180;

    /// <summary>Turns may never consume more than this share of the fixed duration.</summary>
    private const double MaxTurnShare = 0.4;

    private const double MinimumPixelDistance = 0.001;
    private const int DegreesPerFacing = 60;

    private MovementPlaybackTimeline(
        double durationMilliseconds,
        MovementPlaybackClip clip,
        IReadOnlyList<MovementPlaybackPhase> phases,
        double totalDistance)
    {
        DurationMilliseconds = durationMilliseconds;
        Clip = clip;
        Phases = phases;
        TotalDistance = totalDistance;
    }

    public double DurationMilliseconds { get; }

    public MovementPlaybackClip Clip { get; }

    public IReadOnlyList<MovementPlaybackPhase> Phases { get; }

    /// <summary>Absolute path length in pixels; turns contribute nothing.</summary>
    public double TotalDistance { get; }

    /// <summary>
    /// Builds a timeline for a movement command, or <c>null</c> when there is nothing to animate
    /// (standing still, an empty path, or a path made only of redundant zero-length segments).
    /// </summary>
    public static MovementPlaybackTimeline? Create(
        IReadOnlyList<PathSegmentData>? segments,
        MovementType movementType,
        TimeSpan? duration = null)
    {
        if (movementType == MovementType.StandingStill || segments is not { Count: > 0 })
        {
            return null;
        }

        var durationMilliseconds = (duration ?? DefaultDuration).TotalMilliseconds;
        if (durationMilliseconds <= 0)
        {
            return null;
        }

        var phases = BuildPhases(segments, durationMilliseconds);
        if (phases.Count == 0)
        {
            return null;
        }

        var clip = movementType switch
        {
            MovementType.Walk => MovementPlaybackClip.Walk,
            MovementType.Run => MovementPlaybackClip.Run,
            _ => MovementPlaybackClip.None
        };

        var totalDistance = phases
            .Where(phase => !phase.IsTurn)
            .Sum(phase => Distance(phase.From, phase.To));

        return new MovementPlaybackTimeline(durationMilliseconds, clip, phases, totalDistance);
    }

    /// <summary>
    /// Samples the timeline at <paramref name="progress"/> (0 at the path start, 1 at its end). Seeking
    /// backwards is supported because the sample depends only on the progress value.
    /// </summary>
    public MovementPlaybackSample Sample(double progress)
    {
        if (progress <= 0) return WithClip(SampleAtPhase(Phases[0], 0));
        if (progress >= 1) return WithClip(SampleAtPhase(Phases[^1], 1));

        var time = progress * DurationMilliseconds;
        for (var index = 0; index < Phases.Count; index++)
        {
            var phase = Phases[index];
            if (time >= phase.EndMilliseconds && index < Phases.Count - 1)
            {
                continue;
            }

            var local = phase.DurationMilliseconds <= 0
                ? 1
                : Math.Clamp((time - phase.StartMilliseconds) / phase.DurationMilliseconds, 0, 1);
            return WithClip(SampleAtPhase(phase, local));
        }

        return WithClip(SampleAtPhase(Phases[^1], 1));
    }

    private MovementPlaybackSample WithClip(MovementPlaybackSample sample) => sample with { Clip = Clip };

    /// <summary>
    /// Maps travelled distance to a frame index. Negative (reversed) distance walks the cycle backwards,
    /// which is why the modulo is normalised into the range 0…frameCount-1.
    /// </summary>
    public static int ResolveFrameIndex(double signedDistance, double distancePerCycle, int frameCount)
    {
        if (frameCount <= 1 || distancePerCycle <= 0) return 0;

        var index = (int)Math.Floor(signedDistance / distancePerCycle * frameCount);
        return ((index % frameCount) + frameCount) % frameCount;
    }

    private static MovementPlaybackSample SampleAtPhase(MovementPlaybackPhase phase, double local)
    {
        if (!phase.IsTurn)
        {
            var x = phase.From.X + (phase.To.X - phase.From.X) * local;
            var y = phase.From.Y + (phase.To.Y - phase.From.Y) * local;
            var distance = phase.SignedDistanceAtStart +
                           (phase.SignedDistanceAtEnd - phase.SignedDistanceAtStart) * local;
            return new MovementPlaybackSample(
                new PlaybackPoint(x, y),
                phase.FromFacingDegrees,
                distance,
                MovementPlaybackClip.None);
        }

        return new MovementPlaybackSample(
            phase.From,
            LerpFacing(phase.FromFacingDegrees, phase.ToFacingDegrees, local),
            phase.SignedDistanceAtEnd,
            MovementPlaybackClip.None);
    }

    private static List<MovementPlaybackPhase> BuildPhases(
        IReadOnlyList<PathSegmentData> segments,
        double durationMilliseconds)
    {
        var items = Classify(segments);
        if (items.Count == 0) return [];

        var totalDistance = items.Where(item => !item.IsTurn).Sum(item => item.Distance);
        var totalTurnSteps = items.Where(item => item.IsTurn).Sum(item => item.TurnSteps);

        double turnTime;
        double translationTime;
        if (totalDistance <= MinimumPixelDistance)
        {
            turnTime = durationMilliseconds;
            translationTime = 0;
        }
        else
        {
            turnTime = Math.Min(totalTurnSteps * TurnStepMilliseconds, durationMilliseconds * MaxTurnShare);
            translationTime = durationMilliseconds - turnTime;
        }

        var phases = new List<MovementPlaybackPhase>(items.Count);
        var time = 0d;
        var signedDistance = 0d;

        foreach (var item in items)
        {
            var slot = item.IsTurn
                ? (totalTurnSteps > 0 ? turnTime * item.TurnSteps / totalTurnSteps : 0)
                : (totalDistance > 0 ? translationTime * item.Distance / totalDistance : 0);

            var fromDistance = signedDistance;
            var toDistance = item.IsTurn
                ? signedDistance
                : signedDistance + (item.IsReversed ? -item.Distance : item.Distance);

            phases.Add(new MovementPlaybackPhase(
                time,
                time + slot,
                item.From,
                item.To,
                item.FromFacingDegrees,
                item.ToFacingDegrees,
                item.IsTurn,
                item.IsReversed,
                fromDistance,
                toDistance));

            time += slot;
            signedDistance = toDistance;
        }

        // Absorb floating-point drift so the last phase lands exactly on the fixed duration.
        if (phases.Count > 0)
        {
            var last = phases[^1];
            phases[^1] = last with { EndMilliseconds = durationMilliseconds };
        }

        return phases;
    }

    private static List<PhaseItem> Classify(IReadOnlyList<PathSegmentData> segments)
    {
        var items = new List<PhaseItem>(segments.Count);

        foreach (var segment in segments)
        {
            var from = ToPoint(segment.From);
            var to = ToPoint(segment.To);
            var distance = Distance(from, to);

            var fromFacing = segment.From.Facing;
            var toFacing = segment.To.Facing;

            if (distance > MinimumPixelDistance)
            {
                items.Add(new PhaseItem(
                    from,
                    to,
                    fromFacing * DegreesPerFacing,
                    // Translation keeps the segment's starting facing; a facing change is a turn segment.
                    fromFacing * DegreesPerFacing,
                    IsTurn: false,
                    TurnSteps: 0,
                    Distance: distance,
                    IsReversed: segment.IsReversed));
                continue;
            }

            var steps = ShortestTurnSteps(fromFacing, toFacing);
            if (steps == 0) continue; // zero length and no facing change: nothing to play

            items.Add(new PhaseItem(
                from,
                to,
                fromFacing * DegreesPerFacing,
                fromFacing * DegreesPerFacing + ShortestTurnDegrees(fromFacing, toFacing),
                IsTurn: true,
                TurnSteps: steps,
                Distance: 0,
                IsReversed: segment.IsReversed));
        }

        return items;
    }

    private static PlaybackPoint ToPoint(HexPositionData position)
    {
        var coordinates = new HexCoordinates(position.Coordinates.Q, position.Coordinates.R);
        return new PlaybackPoint(coordinates.H, coordinates.V);
    }

    private static double Distance(PlaybackPoint a, PlaybackPoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static int ShortestTurnSteps(int fromFacing, int toFacing)
    {
        var diff = Math.Abs(toFacing - fromFacing) % 6;
        return Math.Min(diff, 6 - diff);
    }

    private static double ShortestTurnDegrees(int fromFacing, int toFacing)
    {
        var delta = ((toFacing - fromFacing + 6) % 6);
        if (delta > 3) delta -= 6;
        return delta * DegreesPerFacing;
    }

    private static double LerpFacing(double from, double to, double t)
    {
        var delta = (to - from + 540) % 360 - 180;
        return from + delta * t;
    }

    private readonly record struct PhaseItem(
        PlaybackPoint From,
        PlaybackPoint To,
        double FromFacingDegrees,
        double ToFacingDegrees,
        bool IsTurn,
        int TurnSteps,
        double Distance,
        bool IsReversed);
}

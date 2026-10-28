using Shouldly;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.Models.Movement;

namespace Sanet.MakaMek.Presentation.Tests.Models.Movement;

public class MovementPlaybackTimelineTests
{
    private static PathSegmentData Segment(int fromQ, int fromR, int fromFacing, int toQ, int toR,
        int toFacing, bool isReversed = false) =>
        new()
        {
            From = new HexPositionData
            {
                Coordinates = new HexCoordinateData(fromQ, fromR),
                Facing = fromFacing
            },
            To = new HexPositionData
            {
                Coordinates = new HexCoordinateData(toQ, toR),
                Facing = toFacing
            },
            Costs = [],
            IsReversed = isReversed
        };

    private static (double X, double Y) Pixel(int q, int r)
    {
        var coordinates = new HexCoordinates(q, r);
        return (coordinates.H, coordinates.V);
    }

    [Fact]
    public void Create_ReturnsNull_ForStandingStill()
    {
        var timeline = MovementPlaybackTimeline.Create(
            [Segment(0, 0, 0, 0, 0, 0)], MovementType.StandingStill);

        timeline.ShouldBeNull();
    }

    [Fact]
    public void Create_ReturnsNull_ForEmptyPath()
    {
        MovementPlaybackTimeline.Create([], MovementType.Walk).ShouldBeNull();
    }

    [Fact]
    public void Create_ReturnsNull_ForMissingPath()
    {
        MovementPlaybackTimeline.Create(null, MovementType.Walk).ShouldBeNull();
    }

    [Fact]
    public void Create_ReturnsNull_WhenEverySegmentIsRedundant()
    {
        var timeline = MovementPlaybackTimeline.Create(
        [
            Segment(0, 0, 0, 0, 0, 0),
            Segment(0, 0, 0, 0, 0, 0)
        ], MovementType.Walk);

        timeline.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_ReturnsNull_ForNonPositiveDuration(double durationSeconds)
    {
        var timeline = MovementPlaybackTimeline.Create(
            [Segment(0, 0, 0, 1, 0, 0)],
            MovementType.Walk,
            TimeSpan.FromSeconds(durationSeconds));

        timeline.ShouldBeNull();
    }

    [Fact]
    public void Sample_Endpoints_ReturnPathStartAndDestination()
    {
        var timeline = MovementPlaybackTimeline.Create(
        [
            Segment(0, 0, 0, 1, 0, 0),
            Segment(1, 0, 0, 2, 0, 0)
        ], MovementType.Walk);

        timeline.ShouldNotBeNull();

        var start = timeline.Sample(0);
        var end = timeline.Sample(1);

        var (startX, startY) = Pixel(0, 0);
        var (endX, endY) = Pixel(2, 0);
        start.Position.X.ShouldBe(startX, 0.001);
        start.Position.Y.ShouldBe(startY, 0.001);
        end.Position.X.ShouldBe(endX, 0.001);
        end.Position.Y.ShouldBe(endY, 0.001);
        start.FacingDegrees.ShouldBe(0);
        end.FacingDegrees.ShouldBe(0);
        timeline.Clip.ShouldBe(MovementPlaybackClip.Walk);
    }

    [Fact]
    public void Duration_IsAlwaysFiveSeconds_RegardlessOfPathLength()
    {
        var shortPath = MovementPlaybackTimeline.Create(
            [Segment(0, 0, 0, 1, 0, 0)], MovementType.Walk)!;
        var longPath = MovementPlaybackTimeline.Create(
        [
            Segment(0, 0, 0, 1, 0, 0),
            Segment(1, 0, 0, 2, 0, 0),
            Segment(2, 0, 0, 3, 0, 0),
            Segment(3, 0, 0, 4, 0, 0),
            Segment(4, 0, 0, 5, 0, 0)
        ], MovementType.Run)!;

        shortPath.DurationMilliseconds.ShouldBe(5000);
        longPath.DurationMilliseconds.ShouldBe(5000);
        longPath.Clip.ShouldBe(MovementPlaybackClip.Run);
    }

    [Fact]
    public void Sample_DistributesTranslationTimeInProportionToDistance()
    {
        // A one-hex leg and a two-hex leg, so the ships' legs have clearly unequal lengths.
        var timeline = MovementPlaybackTimeline.Create(
        [
            Segment(0, 0, 0, 1, 0, 0),
            Segment(1, 0, 0, 1, 2, 0)
        ], MovementType.Walk)!;

        var firstLegDistance = Distance(Pixel(0, 0), Pixel(1, 0));
        var secondLegDistance = Distance(Pixel(1, 0), Pixel(1, 2));
        secondLegDistance.ShouldBeGreaterThan(firstLegDistance);

        timeline.TotalDistance.ShouldBe(firstLegDistance + secondLegDistance, 0.001);

        // At a quarter of the total distance the unit must still be on the first (shorter) leg.
        var sample = timeline.Sample(0.25);
        var firstLegEndX = Pixel(1, 0).X;
        sample.Position.X.ShouldBeLessThan(firstLegEndX);

        // At the distance boundary between the legs the sample is the shared hex.
        var boundary = timeline.Sample(firstLegDistance / timeline.TotalDistance);
        boundary.Position.X.ShouldBe(firstLegEndX, 0.001);
    }

    private static double Distance((double X, double Y) a, (double X, double Y) b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    [Fact]
    public void Sample_UsesTheStartingFacingDuringTranslation()
    {
        var timeline = MovementPlaybackTimeline.Create(
            [Segment(0, 0, 1, 1, 0, 1)], MovementType.Walk)!;

        timeline.Sample(0.5).FacingDegrees.ShouldBe(60);
    }

    [Fact]
    public void TurnOnlyPath_SpreadsTheFullDurationAcrossTurns()
    {
        var timeline = MovementPlaybackTimeline.Create(
        [
            Segment(0, 0, 0, 0, 0, 1),
            Segment(0, 0, 1, 0, 0, 2)
        ], MovementType.Walk)!;

        timeline.Phases.Count.ShouldBe(2);
        timeline.Phases.Sum(phase => phase.DurationMilliseconds).ShouldBe(5000, 0.001);
        timeline.Sample(0.25).FacingDegrees.ShouldBe(30, 0.001);
        timeline.Sample(0.5).FacingDegrees.ShouldBe(60, 0.001);
        timeline.Sample(0.75).FacingDegrees.ShouldBe(90, 0.001);
        timeline.Sample(1).FacingDegrees.ShouldBe(120, 0.001);
    }

    [Fact]
    public void Turn_InterpolatesFacingThroughTheShortestDirection()
    {
        // 5 -> 0 is a single +60° step, not a five-step counter-clockwise spin.
        var timeline = MovementPlaybackTimeline.Create(
            [Segment(0, 0, 5, 0, 0, 0)], MovementType.Walk)!;

        timeline.Phases.Count.ShouldBe(1);
        timeline.Sample(0.5).FacingDegrees.ShouldBe(330, 0.001);
        timeline.Sample(1).FacingDegrees.ShouldBe(360, 0.001);
    }

    [Fact]
    public void Turn_InterpolatesFacingBackwards_WhenThatIsTheShortestDirection()
    {
        // 0 -> 5 is a single -60° step; the timeline may end at a negative facing.
        var timeline = MovementPlaybackTimeline.Create(
            [Segment(0, 0, 0, 0, 0, 5)], MovementType.Walk)!;

        timeline.Phases.Count.ShouldBe(1);
        timeline.Sample(0.5).FacingDegrees.ShouldBe(-30, 0.001);
        timeline.Sample(1).FacingDegrees.ShouldBe(-60, 0.001);
    }

    [Fact]
    public void Sample_KeepsTravelledDistanceContinuousAcrossSegments()
    {
        var timeline = MovementPlaybackTimeline.Create(
        [
            Segment(0, 0, 0, 1, 0, 0),
            Segment(1, 0, 0, 2, 0, 0)
        ], MovementType.Walk)!;

        var beforeBoundary = timeline.Sample(0.499);
        var afterBoundary = timeline.Sample(0.501);

        beforeBoundary.SignedDistance.ShouldBeLessThan(afterBoundary.SignedDistance);
        beforeBoundary.Position.X.ShouldBeLessThan(afterBoundary.Position.X);

        // Halfway through a two-leg, equal-length path the travelled distance is half the stride.
        timeline.Sample(0.5).SignedDistance.ShouldBe(timeline.TotalDistance / 2, 0.5);
    }

    [Fact]
    public void ReversedSegment_ProducesNegativeSignedDistance()
    {
        var timeline = MovementPlaybackTimeline.Create(
            [Segment(1, 0, 0, 0, 0, 0, isReversed: true)], MovementType.Walk)!;

        var sample = timeline.Sample(0.5);

        timeline.Phases.ShouldAllBe(phase => phase.IsReversed);
        sample.SignedDistance.ShouldBeLessThan(0);
        timeline.Sample(1).SignedDistance.ShouldBe(-timeline.TotalDistance, 0.001);
    }

    [Fact]
    public void ForwardSegment_IsNotMarkedAsReversed()
    {
        var timeline = MovementPlaybackTimeline.Create(
            [Segment(0, 0, 0, 1, 0, 0)], MovementType.Walk)!;

        timeline.Phases.ShouldAllBe(phase => !phase.IsReversed);
    }

    [Fact]
    public void Sample_SupportsBackwardSeeking()
    {
        var timeline = MovementPlaybackTimeline.Create(
            [Segment(0, 0, 0, 2, 0, 0)], MovementType.Run)!;

        var forward = timeline.Sample(0.7);
        _ = timeline.Sample(1);
        var backward = timeline.Sample(0.7);

        backward.ShouldBe(forward);
    }

    [Fact]
    public void JumpAndProne_UseNoSpriteClipButStillTranslate()
    {
        var jump = MovementPlaybackTimeline.Create(
            [Segment(0, 0, 0, 2, 0, 0)], MovementType.Jump)!;
        var prone = MovementPlaybackTimeline.Create(
            [Segment(0, 0, 0, 1, 0, 0)], MovementType.Prone)!;

        jump.Clip.ShouldBe(MovementPlaybackClip.None);
        prone.Clip.ShouldBe(MovementPlaybackClip.None);
        jump.Sample(1).Position.X.ShouldBe(Pixel(2, 0).X, 0.001);
    }

    [Theory]
    [InlineData(0, 43.878, 10, 0)]
    [InlineData(43.878, 43.878, 10, 0)]
    [InlineData(21.939, 43.878, 10, 5)]
    [InlineData(94.058, 94.058, 10, 0)]
    [InlineData(-21.939, 43.878, 10, 5)]
    [InlineData(-43.878, 43.878, 10, 0)]
    public void ResolveFrameIndex_UsesDistanceAndWrapsInBothDirections(
        double distance, double distancePerCycle, int frameCount, int expected)
    {
        MovementPlaybackTimeline.ResolveFrameIndex(distance, distancePerCycle, frameCount)
            .ShouldBe(expected);
    }

    [Theory]
    [InlineData(10, 0, 10)]
    [InlineData(10, 43.878, 1)]
    [InlineData(10, 43.878, 0)]
    public void ResolveFrameIndex_ReturnsZero_WhenThereIsNothingToPlay(
        double distance, double distancePerCycle, int frameCount)
    {
        MovementPlaybackTimeline.ResolveFrameIndex(distance, distancePerCycle, frameCount).ShouldBe(0);
    }
}

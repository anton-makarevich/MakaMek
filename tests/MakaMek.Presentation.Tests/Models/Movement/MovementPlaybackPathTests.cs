using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.Models.Movement;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.Models.Movement;

public class MovementPlaybackPathTests
{
    [Fact]
    public void Resolve_ReturnsInputSegments_WhenDeclarationIsNull()
    {
        List<PathSegmentData> segments =
        [
            Segment(0, 0, 0, 1, 0, 0),
            Segment(1, 0, 0, 2, 0, 0)
        ];

        var resolved = MovementPlaybackPath.Resolve(segments, MovementType.Walk, null);

        resolved.ShouldBeSameAs(segments);
        var timeline = MovementPlaybackTimeline.Create(resolved, MovementType.Walk)!;
        timeline.Sample(1).Position.X.ShouldBe(Pixel(2, 0).X, 0.001);
        timeline.Sample(1).Position.Y.ShouldBe(Pixel(2, 0).Y, 0.001);
    }

    [Fact]
    public void Resolve_ReturnsInputSegments_ForNonMovementAttack()
    {
        List<PathSegmentData> segments =
        [
            Segment(0, 0, 0, 1, 0, 0),
            Segment(1, 0, 0, 2, 0, 0)
        ];

        var resolved = MovementPlaybackPath.Resolve(
            segments, MovementType.Walk, Declaration(PhysicalAttackType.Punch));

        resolved.ShouldBeSameAs(segments);
    }

    [Fact]
    public void Resolve_EndsChargePlaybackInApproachHex_WithStopFacing()
    {
        List<PathSegmentData> segments =
        [
            // Turn before moving.
            Segment(0, 0, 0, 0, 0, 1),
            Segment(0, 0, 1, 1, 0, 1),
            // Last translation enters the target hex; everything from here is pending.
            Segment(1, 0, 1, 2, 0, 1),
            // Trailing turn in the target hex.
            Segment(2, 0, 1, 2, 0, 2)
        ];

        var resolved = MovementPlaybackPath.Resolve(
            segments, MovementType.Walk, Declaration(PhysicalAttackType.Charge));

        resolved.Count.ShouldBe(2);
        var timeline = MovementPlaybackTimeline.Create(resolved, MovementType.Walk)!;
        var end = timeline.Sample(1);
        end.Position.X.ShouldBe(Pixel(1, 0).X, 0.001);
        end.Position.Y.ShouldBe(Pixel(1, 0).Y, 0.001);
        Normalize(end.FacingDegrees).ShouldBe(60, 0.001);
    }

    [Fact]
    public void Resolve_AddsClosingTurn_ForDfaJumpWithDifferingFacings()
    {
        List<PathSegmentData> segments =
        [
            // The jump translation keeps its From facing until it ends.
            Segment(0, 0, 0, 1, 0, 1),
            // Last translation enters the target hex.
            Segment(1, 0, 1, 2, 0, 1)
        ];

        var resolved = MovementPlaybackPath.Resolve(
            segments, MovementType.Jump, Declaration(PhysicalAttackType.DFA));

        resolved.Count.ShouldBe(2);
        var closing = new PathSegment(resolved[^1]);
        closing.From.Coordinates.ShouldBe(closing.To.Coordinates,
            "the closing segment must be a same-hex turn");
        closing.From.Facing.ShouldNotBe(closing.To.Facing);
        var timeline = MovementPlaybackTimeline.Create(resolved, MovementType.Jump)!;
        var end = timeline.Sample(1);
        end.Position.X.ShouldBe(Pixel(1, 0).X, 0.001);
        end.Position.Y.ShouldBe(Pixel(1, 0).Y, 0.001);
        Normalize(end.FacingDegrees).ShouldBe(60, 0.001);
    }

    [Fact]
    public void Resolve_ReturnsEmpty_ForOneHexChargeWithoutPrecedingSegments()
    {
        List<PathSegmentData> segments =
        [
            Segment(0, 0, 0, 1, 0, 0)
        ];

        var resolved = MovementPlaybackPath.Resolve(
            segments, MovementType.Walk, Declaration(PhysicalAttackType.Charge));

        resolved.ShouldBeEmpty();
        MovementPlaybackTimeline.Create(resolved, MovementType.Walk).ShouldBeNull();
    }

    private static PathSegmentData Segment(int fromQ, int fromR, int fromFacing, int toQ, int toR,
        int toFacing) =>
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
            Costs = []
        };

    private static (double X, double Y) Pixel(int q, int r)
    {
        var coordinates = new HexCoordinates(q, r);
        return (coordinates.H, coordinates.V);
    }

    private static double Normalize(double degrees) => (degrees % 360 + 360) % 360;

    private static PhysicalAttackDeclarationData Declaration(PhysicalAttackType attackType) =>
        new()
        {
            AttackType = attackType,
            AttackerLimbs = [],
            TargetId = Guid.NewGuid()
        };
}

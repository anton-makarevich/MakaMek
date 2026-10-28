using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using MonacoLabs.Presto;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;
using Path = Avalonia.Controls.Shapes.Path;

namespace MakaMek.Avalonia.Tests.Controls;

/// <summary>
/// The declaration badge is motion drawn on the board, so what can be checked headlessly is the
/// contract around it: it is inert, it sits on the shared edge, the attack type picks the icon, and
/// the pulse starts, respects reduced motion and dies with the control.
/// </summary>
public class PhysicalAttackIndicatorMotionTests
{
    [Fact]
    public Task TheBadgeAndEveryPathInItAreInert() => Dispatch(() =>
    {
        using var engine = StartEngine();
        using var indicator = CreateIndicator(PhysicalAttackType.Punch);

        indicator.IsHitTestVisible.ShouldBeFalse("a click has to reach the map under the badge");
        var paths = indicator.Children.OfType<Path>().ToList();
        paths.Count.ShouldBe(3, "the badge is a pulse, a hex and an icon");
        paths.ShouldAllBe(path => !path.IsHitTestVisible);
    });

    [Fact]
    public Task TheBadgeIsAFractionOfAHexThatTurnsAroundItsCentre() => Dispatch(() =>
    {
        using var engine = StartEngine();
        using var indicator = CreateIndicator(PhysicalAttackType.Punch);

        var scale = PhysicalAttackIndicatorControl.SizeFactor;
        scale.ShouldBeGreaterThan(0.2, "the badge has to fit on a shared edge");
        scale.ShouldBeLessThan(0.5);
        indicator.Width.ShouldBe(HexCoordinatesPixelExtensions.HexWidth * scale, 3);
        indicator.Height.ShouldBe(HexCoordinatesPixelExtensions.HexHeight * scale, 3);
        indicator.RenderTransformOrigin.ShouldBe(new RelativePoint(0.5, 0.5, RelativeUnit.Relative));
        indicator.ZIndex.ShouldBe(PhysicalAttackIndicatorControl.LayerIndex);
    });

    [Fact]
    public Task PunchAndKickResolveDifferentIcons() => Dispatch(() =>
    {
        using var engine = StartEngine();
        using var punch = CreateIndicator(PhysicalAttackType.Punch);
        using var kick = CreateIndicator(PhysicalAttackType.Kick);

        var punchGeometry = punch.IconPath.Data;
        var kickGeometry = kick.IconPath.Data;
        punchGeometry.ShouldNotBeNull("the punch icon comes from the icon resources");
        kickGeometry.ShouldNotBeNull("the kick icon comes from the icon resources");
        kickGeometry.ShouldNotBeSameAs(punchGeometry);

        // A typo in the path data usually collapses the shape instead of failing to parse.
        punchGeometry.Bounds.Width.ShouldBeGreaterThan(5);
        punchGeometry.Bounds.Height.ShouldBeGreaterThan(5);
        kickGeometry.Bounds.Width.ShouldBeGreaterThan(5);
        kickGeometry.Bounds.Height.ShouldBeGreaterThan(5);

        PhysicalAttackIndicatorControl.IconResourceKey(PhysicalAttackType.Punch).ShouldBe("PunchIcon");
        PhysicalAttackIndicatorControl.IconResourceKey(PhysicalAttackType.Kick).ShouldBe("KickIcon");
    });

    [Fact]
    public Task TheBadgeSitsOnTheSharedEdgeShiftedToTheRightOfTheAttack() => Dispatch(() =>
    {
        // Attacker at one end of the edge, target at the other. Pixel space grows downwards, so the
        // right of the attack is the side the badge has to take.
        var from = new HexCoordinates(0, 0);
        var to = new HexCoordinates(1, 0);
        var midpoint = Midpoint(from, to);

        var forward = PhysicalAttackIndicatorControl.IndicatorCenter(from, to);
        var backward = PhysicalAttackIndicatorControl.IndicatorCenter(to, from);

        Distance(forward, midpoint).ShouldBe(PhysicalAttackIndicatorControl.EdgeShift, 3);
        Distance(backward, midpoint).ShouldBe(PhysicalAttackIndicatorControl.EdgeShift, 3);
        Distance(forward, backward).ShouldBe(2 * PhysicalAttackIndicatorControl.EdgeShift, 3,
            "mutual declarations have to land on opposite sides of the edge instead of overlapping");
    });

    [Fact]
    public Task TheEntranceMakesTheBadgeVisible() => Dispatch(() =>
    {
        using var engine = StartEngine();
        using var indicator = CreateIndicator(PhysicalAttackType.Punch);

        engine.Advance(1);
        indicator.Opacity.ShouldBeLessThan(1, "the entrance fades the badge in");
        engine.Advance(119);

        indicator.Opacity.ShouldBeGreaterThan(0.9);
    });

    [Fact]
    public Task ThePulseKeepsMovingWhileTheAttackIsDeclared() => Dispatch(() =>
    {
        using var engine = StartEngine();
        using var indicator = CreateIndicator(PhysicalAttackType.Punch);

        engine.Advance(30);
        var firstOpacity = indicator.PulsePath.Opacity;
        var firstScale = PulseScale(indicator.PulsePath);

        // Stay inside the first cycle: the pulse loops, so a longer jump could land on a brighter
        // frame of the next pass.
        engine.Advance(20);
        var secondOpacity = indicator.PulsePath.Opacity;
        var secondScale = PulseScale(indicator.PulsePath);

        secondOpacity.ShouldBeLessThan(firstOpacity);
        secondScale.ShouldBeGreaterThan(firstScale);
        indicator.PulsePath.Opacity.ShouldBeGreaterThan(0, "the pulse loops instead of settling");
    });

    [Fact]
    public Task UnderReducedMotionTheBadgeStillShowsWhileThePulseStops() => Dispatch(() =>
    {
        using var engine = StartEngine();
        Presto.Defaults.ReducedMotion = true;
        try
        {
            using var indicator = CreateIndicator(PhysicalAttackType.Punch);
            engine.Advance(30);

            indicator.Opacity.ShouldBeGreaterThan(0.9, "the reduced entrance still shows the badge");
            indicator.PulsePath.Opacity.ShouldBe(0, "the reduced pulse is a still badge, not a loop");
            indicator.BadgePath.IsVisible.ShouldBeTrue();
            indicator.BadgePath.Opacity.ShouldBeGreaterThan(0, "the hex stays visible without the loop");
            indicator.BadgePath.Fill.ShouldNotBeNull();
            indicator.IconPath.Opacity.ShouldBeGreaterThan(0, "the icon stays visible without the loop");
        }
        finally
        {
            Presto.Defaults.ReducedMotion = false;
        }
    });

    [Fact]
    public Task DisposingTheBadgeStopsThePulse() => Dispatch(() =>
    {
        using var engine = StartEngine();
        var indicator = CreateIndicator(PhysicalAttackType.Punch);

        engine.Advance(30);
        indicator.Dispose();
        var opacityAfterDispose = indicator.PulsePath.Opacity;

        engine.Advance(120);

        indicator.PulsePath.Opacity.ShouldBe(opacityAfterDispose);
        indicator.PulsePath.RenderTransform.ShouldBeNull("the pulse goes back to its resting transform");
    });

    private static PhysicalAttackIndicatorControl CreateIndicator(PhysicalAttackType attackType) =>
        new(new PhysicalAttackIndicatorViewModel
        {
            AttackerId = Guid.NewGuid(),
            TargetId = Guid.NewGuid(),
            AttackType = attackType,
            From = new HexCoordinates(0, 0),
            To = new HexCoordinates(1, 0),
            AttackerTint = "#FF0000",
            PendingLimbs = attackType == PhysicalAttackType.Kick
                ? [PartLocation.LeftLeg]
                : [PartLocation.LeftArm, PartLocation.RightArm]
        });

    private static Point Midpoint(HexCoordinates from, HexCoordinates to) => new(
        (HexCenter(from).X + HexCenter(to).X) / 2,
        (HexCenter(from).Y + HexCenter(to).Y) / 2);

    private static Point HexCenter(HexCoordinates coordinates) => new(
        coordinates.H + HexCoordinatesPixelExtensions.HexWidth * 0.5,
        coordinates.V + HexCoordinatesPixelExtensions.HexHeight * 0.5);

    private static double Distance(Point a, Point b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static double PulseScale(Path path)
    {
        var group = path.RenderTransform.ShouldBeOfType<TransformGroup>();
        return group.Children.OfType<ScaleTransform>().Single().ScaleX;
    }

    /// <summary>
    /// Points the engine at a frame source the test drives by hand. The engine is process wide, so
    /// the previous frame source is put back when the test is done with it.
    /// </summary>
    private static TestEngine StartEngine()
    {
        PrestoAvalonia.EnsureInitialized();
        var frames = new ManualFrameSource();
        var previous = Presto.Engine.FrameSource;
        Presto.Engine.FrameSource = frames;
        return new TestEngine(frames, () => Presto.Engine.FrameSource = previous);
    }

    private sealed class TestEngine(ManualFrameSource frames, Action restore) : IDisposable
    {
        public void Advance(int steps)
        {
            for (var i = 0; i < steps; i++)
            {
                frames.Advance(16);
            }
        }

        public void Dispose() => restore();
    }

    private static Task Dispatch(Action action)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(
            typeof(PhysicalAttackIndicatorMotionTests).Assembly);
        return session.Dispatch(action, CancellationToken.None);
    }
}

using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using MonacoLabs.Presto;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Controls;

/// <summary>
/// The selection ring is motion, so it is not covered by the usual unit tests. What can be checked
/// headlessly is that the motion is well formed: the geometry is a hexagon inside the hex, the
/// entrance shows the ring, the exit hides it again, the pulse loops and the reduced forms neither
/// loop nor leave the ring invisible.
/// </summary>
/// <remarks>
/// The self-drawing stroke itself needs a platform that measures the contour, which the headless
/// platform does not, so only the parts that do not depend on it are asserted here.
/// </remarks>
public class SelectionRingMotionTests
{
    [Fact]
    public Task TheGeometryIsAHexagonInsideTheHex() => Dispatch(() =>
    {
        var bounds = SelectionRingMotion.CreateHexRingGeometry(SelectionRingMotion.Inset).Bounds;

        bounds.Left.ShouldBeGreaterThan(0);
        bounds.Top.ShouldBeGreaterThan(0);
        bounds.Right.ShouldBeLessThan(HexCoordinatesPixelExtensions.HexWidth);
        bounds.Bottom.ShouldBeLessThan(HexCoordinatesPixelExtensions.HexHeight);
        bounds.Width.ShouldBeLessThan(HexCoordinatesPixelExtensions.HexWidth);
        bounds.Height.ShouldBeLessThan(HexCoordinatesPixelExtensions.HexHeight);
    });

    [Fact]
    public Task TheGeometryIsInsetByTheSameAmountOnEverySide() => Dispatch(() =>
    {
        var bounds = SelectionRingMotion.CreateHexRingGeometry(SelectionRingMotion.Inset).Bounds;

        bounds.Left.ShouldBe(HexCoordinatesPixelExtensions.HexWidth / 2 - bounds.Width / 2, 3);
        bounds.Top.ShouldBe(HexCoordinatesPixelExtensions.HexHeight / 2 - bounds.Height / 2, 3);
    });

    [Fact]
    public Task TheRingIsInvisibleAndInertAtRest() => Dispatch(() =>
    {
        var ring = SelectionRingMotion.CreateRing(SelectionRingMotion.Inset, 4, Brushes.Red);

        ring.Opacity.ShouldBe(0);
        ring.IsHitTestVisible.ShouldBeFalse();
        ring.Stretch.ShouldBe(Stretch.None);
        ring.RenderTransformOrigin.ShouldBe(new RelativePoint(0.5, 0.5, RelativeUnit.Relative));
    });

    [Fact]
    public Task TheEntranceShowsTheRingAndTheExitHidesItAgain() => Dispatch(() =>
    {
        using var engine = StartEngine();
        var ring = SelectionRingMotion.CreateRing(SelectionRingMotion.Inset, 4, Brushes.Red);
        var scope = PrestoAvalonia.CreateScope(ring);

        scope.Execute(_ => { ring.Animate(SelectionRingMotion.RingIn); });
        engine.Advance(120);

        ring.Opacity.ShouldBeGreaterThan(0.9);
        ring.RenderTransform.ShouldNotBeNull("the ring scales itself into place");

        scope.Execute(_ => { ring.Animate(SelectionRingMotion.RingOut); });
        engine.Advance(30);

        ring.Opacity.ShouldBeLessThan(0.1);
        scope.Revert();
    });

    [Fact]
    public Task ThePulseLoopsWhileTheUnitIsSelected() => Dispatch(() =>
    {
        using var engine = StartEngine();
        var pulse = SelectionRingMotion.CreateRing(SelectionRingMotion.Inset, 2, Brushes.Red);
        var scope = PrestoAvalonia.CreateScope(pulse);

        scope.Execute(_ => { pulse.Animate(SelectionRingMotion.IdlePulse); });
        engine.Advance(30);

        pulse.Opacity.ShouldBeLessThan(1, "the pulse fades the ring in and out");

        engine.Advance(200);
        pulse.Opacity.ShouldBeGreaterThan(0, "the pulse keeps running instead of settling");
        scope.Revert();
    });

    [Fact]
    public Task UnderReducedMotionThePulseDoesNotLoopAndTheRingStillShows() => Dispatch(() =>
    {
        using var engine = StartEngine();
        Presto.Defaults.ReducedMotion = true;
        try
        {
            var ring = SelectionRingMotion.CreateRing(SelectionRingMotion.Inset, 4, Brushes.Red);
            var scope = PrestoAvalonia.CreateScope(ring);

            scope.Execute(_ => { ring.Animate(SelectionRingMotion.RingIn); });
            engine.Advance(30);

            ring.Opacity.ShouldBeGreaterThan(0.9, "the reduced entrance still fades the ring in");

            var pulse = SelectionRingMotion.CreateRing(SelectionRingMotion.Inset, 2, Brushes.Red);
            scope.Execute(_ => { pulse.Animate(SelectionRingMotion.IdlePulse); });
            engine.Advance(30);

            pulse.Opacity.ShouldBe(0, "the reduced pulse is a still ring, not a loop");
            scope.Revert();
        }
        finally
        {
            Presto.Defaults.ReducedMotion = false;
        }
    });

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
            typeof(SelectionRingMotionTests).Assembly);
        return session.Dispatch(action, CancellationToken.None);
    }
}

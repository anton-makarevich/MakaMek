using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using MonacoLabs.Presto;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Controls;

/// <summary>
/// The hex shaped marks on the board are motion, so they are not covered by the usual unit tests.
/// What can be checked headlessly is that their shared motion vocabulary is well formed: the
/// geometry is a hexagon inside the hex, the entrance shows the outline, the exit hides it again,
/// the pulse loops and the reduced forms neither loop nor leave the mark invisible.
/// </summary>
/// <remarks>
/// The self-drawing stroke itself needs a platform that measures the contour, which the headless
/// platform does not, so only the parts that do not depend on it are asserted here.
/// </remarks>
public class HexShapeMotionTests
{
    [Fact]
    public Task TheGeometryIsAHexagonInsideTheHex() => Dispatch(() =>
    {
        var bounds = HexShapeMotion.CreateHexGeometry(HexShapeMotion.Inset).Bounds;

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
        var bounds = HexShapeMotion.CreateHexGeometry(HexShapeMotion.Inset).Bounds;

        bounds.Left.ShouldBe(HexCoordinatesPixelExtensions.HexWidth / 2 - bounds.Width / 2, 3);
        bounds.Top.ShouldBe(HexCoordinatesPixelExtensions.HexHeight / 2 - bounds.Height / 2, 3);
    });

    [Fact]
    public Task TheOutlineIsInvisibleAndInertAtRest() => Dispatch(() =>
    {
        var hex = HexShapeMotion.CreateHex(HexShapeMotion.Inset, 4, Brushes.Red);

        hex.Opacity.ShouldBe(0);
        hex.IsHitTestVisible.ShouldBeFalse();
        hex.Stretch.ShouldBe(Stretch.None);
        hex.RenderTransformOrigin.ShouldBe(new RelativePoint(0.5, 0.5, RelativeUnit.Relative));
    });

    [Fact]
    public Task TheOutlineIsTheSizeOfTheHexSoItTurnsAroundTheHexCentre() => Dispatch(() =>
    {
        var hex = HexShapeMotion.CreateHex(HexShapeMotion.Inset, 4, Brushes.Red);

        // The hexagon is drawn in the middle of the path, so the path has to be the size of the hex
        // for a relative origin of one half to be the centre of the hexagon.
        hex.Width.ShouldBe(HexCoordinatesPixelExtensions.HexWidth);
        hex.Height.ShouldBe(HexCoordinatesPixelExtensions.HexHeight);
    });

    [Fact]
    public Task TheInsetOfAScaledHexShrinksWithTheHex() => Dispatch(() =>
    {
        // The attack badge is a fraction of a hex. Its inset is measured in unscaled hex pixels, so
        // it shrinks in proportion; measuring it in scaled pixels would make the small badge
        // visibly smaller without changing anything about the large outline.
        const double scale = 0.5;
        const double inset = 6;

        var full = HexShapeMotion.CreateHexGeometry(0, scale).Bounds;
        var insetBounds = HexShapeMotion.CreateHexGeometry(inset, scale).Bounds;

        ((full.Height - insetBounds.Height) / 2).ShouldBe(inset * scale, 3);
        insetBounds.Left.ShouldBe(HexCoordinatesPixelExtensions.HexWidth * scale / 2 - insetBounds.Width / 2, 3);
        insetBounds.Top.ShouldBe(HexCoordinatesPixelExtensions.HexHeight * scale / 2 - insetBounds.Height / 2, 3);
    });

    [Fact]
    public Task TheEntranceShowsTheOutlineAndTheExitHidesItAgain() => Dispatch(() =>
    {
        using var engine = StartEngine();
        var hex = HexShapeMotion.CreateHex(HexShapeMotion.Inset, 4, Brushes.Red);
        var scope = PrestoAvalonia.CreateScope(hex);

        scope.Execute(_ => { hex.Animate(HexShapeMotion.Enter); });
        engine.Advance(120);

        hex.Opacity.ShouldBeGreaterThan(0.9);
        hex.RenderTransform.ShouldNotBeNull("the outline scales itself into place");

        scope.Execute(_ => { hex.Animate(HexShapeMotion.Exit); });
        engine.Advance(30);

        hex.Opacity.ShouldBeLessThan(0.1);
        scope.Revert();
    });

    [Fact]
    public Task ThePulseLoopsWhileTheUnitIsSelected() => Dispatch(() =>
    {
        using var engine = StartEngine();
        var pulse = HexShapeMotion.CreateHex(HexShapeMotion.Inset, 2, Brushes.Red);
        var scope = PrestoAvalonia.CreateScope(pulse);

        scope.Execute(_ => { pulse.Animate(HexShapeMotion.IdlePulse); });
        engine.Advance(30);

        pulse.Opacity.ShouldBeLessThan(1, "the pulse fades the outline in and out");

        engine.Advance(200);
        pulse.Opacity.ShouldBeGreaterThan(0, "the pulse keeps running instead of settling");
        scope.Revert();
    });

    [Fact]
    public Task ThePulseTravelsFarEnoughToBeSeen() => Dispatch(() =>
    {
        using var engine = StartEngine();
        var pulse = HexShapeMotion.CreateHex(HexShapeMotion.Inset, 2, Brushes.Red);
        var scope = PrestoAvalonia.CreateScope(pulse);

        scope.Execute(_ => { pulse.Animate(HexShapeMotion.IdlePulse); });

        // The pulse grows out of the outline, so it has to leave the stroke of the outline behind,
        // otherwise it only ever shows as a slightly brighter outline.
        engine.Advance(30);
        var group = pulse.RenderTransform.ShouldBeOfType<TransformGroup>();
        var scale = group.Children.OfType<ScaleTransform>().Single().ScaleX;
        scale.ShouldBeGreaterThan(1.05);

        scope.Revert();
    });

    [Fact]
    public Task UnderReducedMotionThePulseDoesNotLoopAndTheOutlineStillShows() => Dispatch(() =>
    {
        using var engine = StartEngine();
        Presto.Defaults.ReducedMotion = true;
        try
        {
            var hex = HexShapeMotion.CreateHex(HexShapeMotion.Inset, 4, Brushes.Red);
            var scope = PrestoAvalonia.CreateScope(hex);

            scope.Execute(_ => { hex.Animate(HexShapeMotion.Enter); });
            engine.Advance(30);

            hex.Opacity.ShouldBeGreaterThan(0.9, "the reduced entrance still fades the outline in");

            var pulse = HexShapeMotion.CreateHex(HexShapeMotion.Inset, 2, Brushes.Red);
            scope.Execute(_ => { pulse.Animate(HexShapeMotion.IdlePulse); });
            engine.Advance(30);

            pulse.Opacity.ShouldBe(0, "the reduced pulse is a still outline, not a loop");
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
            typeof(HexShapeMotionTests).Assembly);
        return session.Dispatch(action, CancellationToken.None);
    }
}

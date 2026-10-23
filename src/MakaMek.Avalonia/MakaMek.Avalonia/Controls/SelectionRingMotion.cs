using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using MonacoLabs.Presto;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Avalonia.Controls;

/// <summary>
/// The motion vocabulary of the hexagonal selection ring: the builders that play it and the
/// geometry it strokes.
/// </summary>
/// <remarks>
/// The ring animates its own children, never the control that carries the facing rotation, and
/// every builder has a reduced form so the ring degrades to a short fade under reduced motion.
/// </remarks>
internal static class SelectionRingMotion
{
    /// <summary>
    /// How far the ring geometry is pulled in from the hex border, in pixels.
    /// </summary>
    public const double Inset = 6;

    /// <summary>
    /// The entrance: the stroke draws itself in while the ring springs to its size.
    /// </summary>
    /// <remarks>
    /// <see cref="AnimationBuilder.WhenReduced"/> builds a <em>new</em> builder that keeps the settings but drops the
    /// properties, so the reduced form has to repeat the draw - without it the stroke would stay hidden.
    /// </remarks>
    public static readonly AnimationBuilder RingIn = Presto.Build()
        .Opacity(from: 0, to: 1)
        .Scale(from: 0.82, to: 1)
        .Draw(from: "0 0", to: "0 1")
        .Ease(Eases.Spring(stiffness: 180, damping: 18))
        .WhenReduced(r => r.Opacity(from: 0, to: 1).Draw("0 1").Duration(120).Ease(Eases.None));

    /// <summary>
    /// The exit: the ring fades and grows slightly out of the way.
    /// </summary>
    public static readonly AnimationBuilder RingOut = Presto.Build()
        .Opacity(to: 0)
        .Scale(to: 1.12)
        .Duration(160)
        .Ease(Eases.OutQuad)
        .WhenReduced(r => r.Opacity(to: 0).Duration(80).Ease(Eases.None));

    /// <summary>
    /// The sonar echo: one expanding ring per selection change.
    /// </summary>
    public static readonly AnimationBuilder Echo = Presto.Build()
        .Scale(from: 1, to: 1.22)
        .Opacity(from: 0.55, to: 0)
        .Duration(650)
        .Ease(Eases.OutQuad)
        .WhenReduced(r => r.Opacity(to: 0).Duration(1).Ease(Eases.None));

    /// <summary>
    /// The idle breathing pulse, looping while the unit stays selected.
    /// </summary>
    public static readonly AnimationBuilder IdlePulse = Presto.Build()
        .Scale(from: 1, to: 1.06)
        .Opacity(from: 0.35, to: 0)
        .Duration(1800)
        .Ease(Eases.InOutSine)
        .Loop()
        .WhenReduced(r => r.Loop(false).Opacity(to: 0).Duration(1).Ease(Eases.None));

    /// <summary>
    /// The same breathing pulse, but faster, for a weapon attack target.
    /// </summary>
    public static readonly AnimationBuilder TargetPulse = IdlePulse.With(b => b.Duration(1100));

    /// <summary>
    /// Creates one of the ring paths. The geometry is already in hex-local pixels, so the path is
    /// stretched by nothing and pinned to the top left corner of the unit.
    /// </summary>
    public static Path CreateRing(double inset, double thickness, IBrush brush) => new()
    {
        Data = CreateHexRingGeometry(inset),
        Stroke = brush,
        StrokeThickness = thickness,
        StrokeJoin = PenLineJoin.Round,
        Fill = null,
        Opacity = 0,
        IsHitTestVisible = false,
        Stretch = Stretch.None,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        // The scale pulses around the centre of the hex, not around the top left corner.
        RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative)
    };

    /// <summary>
    /// Builds the hexagon the ring strokes, pulled in by <paramref name="inset"/> pixels.
    /// </summary>
    public static Geometry CreateHexRingGeometry(double inset)
    {
        var corners = HexagonGeometry.GetCorners();
        const double centerX = HexCoordinatesPixelExtensions.HexWidth / 2;
        const double centerY = HexCoordinatesPixelExtensions.HexHeight / 2;
        var scale = 1 - inset / (HexCoordinatesPixelExtensions.HexHeight / 2);

        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(ToPoint(corners[0]), isFilled: false);
        for (var i = 1; i < corners.Length; i++)
        {
            context.LineTo(ToPoint(corners[i]));
        }

        context.EndFigure(true);

        return geometry;

        Point ToPoint((double X, double Y) corner) =>
            new(centerX + (corner.X - centerX) * scale, centerY + (corner.Y - centerY) * scale);
    }
}

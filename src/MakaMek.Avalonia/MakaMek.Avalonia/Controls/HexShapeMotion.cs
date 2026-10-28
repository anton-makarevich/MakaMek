using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using MonacoLabs.Presto;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Avalonia.Controls;

/// <summary>
/// The motion vocabulary shared by the hex shaped marks on the board: the builders that play it and
/// the geometry it strokes.
/// </summary>
/// <remarks>
/// <see cref="SelectionHex"/> and <see cref="PhysicalAttackIndicatorControl"/> are both built from
/// this module, so the selection outline and the attack badges move the same way. A mark animates
/// its own children, never the control that carries the facing rotation, and every builder has a
/// reduced form so the mark degrades to a short fade under reduced motion.
/// </remarks>
internal static class HexShapeMotion
{
    /// <summary>
    /// How far the hexagon is pulled in from the hex border, in pixels.
    /// </summary>
    public const double Inset = 6;

    /// <summary>
    /// The entrance: the stroke draws itself in while the hexagon springs to its size.
    /// </summary>
    /// <remarks>
    /// <see cref="AnimationBuilder.WhenReduced"/> builds a <em>new</em> builder that keeps the settings but drops the
    /// properties, so the reduced form has to repeat the draw - without it the stroke would stay hidden.
    /// </remarks>
    public static readonly AnimationBuilder Enter = Presto.Build()
        .Opacity(from: 0, to: 1)
        .Scale(from: 0.82, to: 1)
        .Draw(from: "0 0", to: "0 1")
        .Ease(Eases.Spring(stiffness: 180, damping: 18))
        .WhenReduced(r => r.Opacity(from: 0, to: 1).Draw("0 1").Duration(120).Ease(Eases.None));

    /// <summary>
    /// The exit: the hexagon fades and grows slightly out of the way.
    /// </summary>
    public static readonly AnimationBuilder Exit = Presto.Build()
        .Opacity(to: 0)
        .Scale(to: 1.12)
        .Duration(160)
        .Ease(Eases.OutQuad)
        .WhenReduced(r => r.Opacity(to: 0).Duration(80).Ease(Eases.None));

    /// <summary>
    /// The sonar echo: one expanding hexagon per selection change.
    /// </summary>
    /// <remarks>
    /// The echo travels well past the outline, so it reads as a ping rather than as a second outline.
    /// </remarks>
    public static readonly AnimationBuilder Echo = Presto.Build()
        .Scale(from: 1, to: 1.34)
        .Opacity(from: 0.8, to: 0)
        .Duration(650)
        .Ease(Eases.OutQuad)
        .WhenReduced(r => r.Opacity(to: 0).Duration(1).Ease(Eases.None));

    /// <summary>
    /// The idle pulse, looping while the unit stays selected.
    /// </summary>
    /// <remarks>
    /// The pulse starts under the outline and peels away from it, so it has to travel far enough and
    /// stay bright enough to be seen against the outline it grows out of.
    /// </remarks>
    public static readonly AnimationBuilder IdlePulse = Presto.Build()
        .Scale(from: 1, to: 1.5)
        .Opacity(from: 0.6, to: 0)
        .Duration(1200)
        .Ease(Eases.OutQuad)
        .Loop()
        .WhenReduced(r => r.Loop(false).Opacity(to: 0).Duration(1).Ease(Eases.None));

    /// <summary>
    /// The same pulse, but faster, for a weapon attack target.
    /// </summary>
    public static readonly AnimationBuilder TargetPulse = IdlePulse.With(b => b.Duration(1100));

    /// <summary>
    /// The entrance of a physical attack indicator: the badge springs to its size. It has no draw
    /// step because the badge is filled, and a filled path would show through the stroke reveal.
    /// </summary>
    public static readonly AnimationBuilder IndicatorEnter = Presto.Build()
        .Opacity(from: 0, to: 1)
        .Scale(from: 0.82, to: 1)
        .Ease(Eases.Spring(stiffness: 180, damping: 18))
        .WhenReduced(r => r.Opacity(from: 0, to: 1).Duration(120).Ease(Eases.None));

    /// <summary>
    /// Creates one of the hexagon paths. The geometry is already in hex-local pixels, so the path is
    /// stretched by nothing and pinned to the top left corner of the unit.
    /// </summary>
    public static Path CreateHex(double inset, double thickness, IBrush brush) => new()
    {
        Data = CreateHexGeometry(inset),
        Stroke = brush,
        StrokeThickness = thickness,
        StrokeJoin = PenLineJoin.Round,
        Fill = null,
        Opacity = 0,
        IsHitTestVisible = false,
        Stretch = Stretch.None,
        // The path is the size of the whole hex, so the hexagon sits centred in it and the scale
        // below turns around the centre of the hex rather than around the top left corner.
        Width = HexCoordinatesPixelExtensions.HexWidth,
        Height = HexCoordinatesPixelExtensions.HexHeight,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative)
    };

    /// <summary>
    /// Creates a hexagon path scaled to <paramref name="scale"/> of a map hex. Unlike
    /// <see cref="CreateHex"/> it is visible at rest, because an indicator is not an entrance.
    /// </summary>
    /// <remarks>
    /// The hexagon is stroked only: the figure is created unfilled, so a brush set on
    /// <see cref="Shape.Fill"/> would have nothing to paint.
    /// </remarks>
    public static Path CreateScaledHex(double scale, double inset, double thickness, IBrush? stroke) => new()
    {
        Data = CreateHexGeometry(inset, scale),
        Stroke = stroke,
        StrokeThickness = thickness,
        StrokeJoin = PenLineJoin.Round,
        Opacity = 1,
        IsHitTestVisible = false,
        Stretch = Stretch.None,
        Width = HexCoordinatesPixelExtensions.HexWidth * scale,
        Height = HexCoordinatesPixelExtensions.HexHeight * scale,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative)
    };

    /// <summary>
    /// Builds the hexagon the outline strokes, pulled in by <paramref name="inset"/> pixels.
    /// </summary>
    public static Geometry CreateHexGeometry(double inset) => CreateHexGeometry(inset, 1);

    /// <summary>
    /// Builds the hexagon at <paramref name="scale"/> of a map hex, pulled in from every edge by
    /// <paramref name="inset"/> pixels of the unscaled hex.
    /// </summary>
    /// <remarks>
    /// The inset scales with the hex, so a mark that is a fraction of a hex keeps its inset in
    /// proportion. Measuring it in scaled pixels instead would pull a small badge in by the same
    /// number of pixels as a full hexagon, which is a different look rather than a fix.
    /// </remarks>
    public static Geometry CreateHexGeometry(double inset, double scale)
    {
        var corners = HexagonGeometry.GetCorners();
        const double hexCenterX = HexCoordinatesPixelExtensions.HexWidth / 2;
        const double hexCenterY = HexCoordinatesPixelExtensions.HexHeight / 2;
        var centerX = hexCenterX * scale;
        var centerY = hexCenterY * scale;
        var total = scale * (1 - inset / (HexCoordinatesPixelExtensions.HexHeight / 2));

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
            new(centerX + (corner.X - hexCenterX) * total, centerY + (corner.Y - hexCenterY) * total);
    }
}
